using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 听音复述（视力障碍 · 听觉序列记忆）。
/// 训练逻辑：依次播放 2～6 个音高，孩子按同样顺序点回。
/// 依据：工作记忆中的「语音回路」——听觉项目按序存储再复现，
/// 其广度与词汇学习、语音意识相关，是盲童识字与外语学习的底层能力。
/// 答错时保持同一串序列重播（便于对比），不会重新随机，避免孩子失去可比较的线索。
/// </summary>
public partial class SoundSequenceGamePage : ContentPage
{
    private const int MaxLevels = 5;        // 关卡 1..5 对应序列长度 2..6
    private static readonly (double Freq, string Name)[] Notes =
    {
        (523.25, "哆"), (659.25, "咪"), (783.99, "嗦")
    };

    private readonly ISoundService _sound = ServiceProvider.Get<ISoundService>();
    private readonly Random _rnd = new();

    private List<int> _seq = new();
    private int _input;
    private int _level;
    private bool _playing = true;
    private bool _finished;
    private bool _started;

    private int CurrentLength => _level + 2;

    public SoundSequenceGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        BeginLevel();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _playing = true;
    }

    private async void BeginLevel()
    {
        if (_level >= MaxLevels)
        {
            await FinishAsync();
            return;
        }

        ProgressLabel.Text = $"第 {_level + 1} / {MaxLevels} 关";
        PromptLabel.Text = $"🎵 记住 {CurrentLength} 个音的顺序";

        _seq = new List<int>();
        for (int i = 0; i < CurrentLength; i++) _seq.Add(_rnd.Next(Notes.Length));

        _input = 0;
        await PlaySequenceAsync();
    }

    /// <summary>播放整串音符。一次性合成整段 WAV，音间距严格一致，便于孩子分辨「先后」。</summary>
    private async Task PlaySequenceAsync()
    {
        _playing = true;
        FeedbackLabel.Text = "🔊 正在播放…";

        await SayShortAsync("请听");
        if (!IsLoaded) return;

        var notes = new List<(double, int)>();
        int total = 0;
        foreach (var n in _seq)
        {
            if (notes.Count > 0) { notes.Add((0.0, 150)); total += 150; }
            notes.Add((Notes[n].Freq, 300));
            total += 300;
        }

        _sound.PlayNotes(notes.ToArray());
        await Task.Delay(total + 350);
        if (!IsLoaded) return;

        _input = 0;
        _playing = false;
        FeedbackLabel.Text = "轮到你了：按刚才的顺序点";
        _ = SayShortAsync("该你了");
    }

    private async void OnNoteClicked(object sender, EventArgs e)
    {
        if (_playing || _finished) return;
        if (_seq.Count == 0 || _input >= _seq.Count) return;
        if (sender is not Button btn) return;
        if (!int.TryParse(btn.CommandParameter?.ToString(), out int idx)) return;
        if (idx < 0 || idx >= Notes.Length) return;

        if (idx == _seq[_input])
        {
            _input++;
            _sound.PlayNotes(new (double, int)[] { (Notes[idx].Freq, 200) });
            A11y.Tap();
            _ = A11y.FlashAsync(btn, Colors.White, 130);
            FeedbackLabel.Text = $"✅ 对！{_input} / {_seq.Count}";

            if (_input >= _seq.Count)
            {
                _playing = true;
                A11y.Success();
                FeedbackLabel.Text = "🎉 全对了！";
                await SayShortAsync(_level + 1 >= MaxLevels
                    ? "全部完成，太厉害了"
                    : $"全部正确，下一关有{CurrentLength + 1}个音");
                if (!IsLoaded) return;

                _level++;
                BeginLevel();
            }
        }
        else
        {
            A11y.Fail();
            _playing = true;
            _sound.PlayNotes(new (double, int)[] { (196.0, 300) });
            FeedbackLabel.Text = "顺序不对，我们再听一次";
            await SayShortAsync("再听一次");
            if (!IsLoaded) return;
            await Task.Delay(300);
            await PlaySequenceAsync();
        }
    }

    private async void OnReplaySequence(object sender, EventArgs e)
    {
        if (_playing || _finished || _seq.Count == 0) return;
        await PlaySequenceAsync();
    }

    private static async Task SayShortAsync(string text)
    {
        try { await Task.WhenAny(A11y.SayAsync(text), Task.Delay(2600)); }
        catch { }
    }

    private async Task FinishAsync()
    {
        _finished = true;
        _playing = true;

        ResultLayer.IsVisible = true;
        ResultTitle.Text = "记性真好！";
        ResultDetail.Text = $"完成了 5 关，最长记住了 {MaxLevels + 1} 个音的先后顺序";
        ResultTip.Text = "小提示：记的时候可以在心里跟着数「一、二、三」，把声音变成有节奏的一串，比死记更容易。";

        _sound.PlayWin();
        await SayShortAsync("全部完成，你记住了六个音的顺序，非常棒");
    }

    private void OnReplayAll(object sender, EventArgs e)
    {
        ResultLayer.IsVisible = false;
        _finished = false;
        _level = 0;
        BeginLevel();
    }

    private async void OnBack(object sender, EventArgs e)
    {
        _playing = true;
        await Navigation.PopAsync();
    }
}
