using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 听声辨物（视力障碍 · 音色 / 音高辨别）。
/// 训练逻辑：连听三个音，其中两个相同、一个不同，指出「不一样」的那个。
/// 依据：音高辨别是听觉分类的基础；低视力儿童无法靠视觉扫视比较物体时，
/// 需要用「声音质地」建立归类能力，这与概念形成、物体恒常性直接相关。
/// 难度按音程差递减（八度 → 五度 → 二度），保证孩子的答对率维持在「跳一跳够得着」的区间。
/// </summary>
public partial class SoundOddOneGamePage : ContentPage
{
    private const int Rounds = 6;

    /// <summary>每题的 (基准音, 不同音) 组合，按音程差从大到小排列（前易后难）。</summary>
    private static readonly (double Base, double Odd)[] Pairs =
    {
        (440.00, 880.00),   // 八度，最容易分辨
        (523.25, 783.99),   // 五度
        (659.25, 880.00),   // 大六度
        (440.00, 493.88),   // 小二度，最难
    };

    private readonly ISoundService _sound = ServiceProvider.Get<ISoundService>();
    private readonly Random _rnd = new();

    private int _round;
    private int _hits;
    private int _oddIndex = -1;
    private double _baseFreq = 440;
    private double _oddFreq = 880;
    private bool _locked = true;
    private bool _started;

    public SoundOddOneGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        BeginRound();
    }

    private async void BeginRound()
    {
        if (_round >= Rounds)
        {
            await FinishAsync();
            return;
        }

        _round++;
        ProgressLabel.Text = $"第 {_round} / {Rounds} 题";
        FeedbackLabel.Text = "🔊 正在播放…";

        // 前 3 题取音程差大的两组，后 3 题取较难的两组
        var pair = _round <= 3
            ? Pairs[_rnd.Next(2)]
            : Pairs[2 + _rnd.Next(2)];
        _baseFreq = pair.Base;
        _oddFreq = pair.Odd;
        _oddIndex = _rnd.Next(3);

        _locked = true;
        await SayShortAsync($"第{_round}题，听三个声音");
        if (!IsLoaded) return;

        await PlayThreeAsync();
        if (!IsLoaded) return;

        _locked = false;
        FeedbackLabel.Text = "哪一个不一样？点一下它";
    }

    private async Task PlayThreeAsync()
    {
        var notes = new List<(double, int)>();
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) notes.Add((0.0, 250));
            notes.Add((i == _oddIndex ? _oddFreq : _baseFreq, 400));
        }

        _sound.PlayNotes(notes.ToArray());
        await Task.Delay(3 * 400 + 2 * 250 + 300);
    }

    private async void OnPick(object sender, EventArgs e)
    {
        if (_locked) return;
        if (sender is not Button btn) return;
        if (!int.TryParse(btn.CommandParameter?.ToString(), out int idx)) return;

        if (idx == _oddIndex)
        {
            _locked = true;
            _hits++;
            A11y.Success();
            _sound.PlayNotes(new (double, int)[] { (783.99, 110), (1046.5, 200) });
            _ = A11y.FlashAsync(btn, Color.FromArgb("#A5D6A7"), 300);
            FeedbackLabel.Text = $"✅ 对了，第 {idx + 1} 个不一样";
            await SayShortAsync($"对了，第{idx + 1}个不一样");
            if (!IsLoaded) return;
            await Task.Delay(500);
            BeginRound();
        }
        else
        {
            A11y.Fail();
            _sound.PlayNotes(new (double, int)[] { (196.0, 280) });
            _ = A11y.FlashAsync(btn, Color.FromArgb("#FFCDD2"), 280);
            FeedbackLabel.Text = "再听一次，注意那个高一点（或低一点）的声音";
            await SayShortAsync("再听一次");
        }
    }

    private async void OnReplay(object sender, EventArgs e)
    {
        if (_locked) return;
        _locked = true;
        await PlayThreeAsync();
        if (!IsLoaded) return;
        _locked = false;
    }

    private static async Task SayShortAsync(string text)
    {
        try { await Task.WhenAny(A11y.SayAsync(text), Task.Delay(2600)); }
        catch { }
    }

    private async Task FinishAsync()
    {
        _locked = true;
        ResultLayer.IsVisible = true;
        ResultTitle.Text = _hits >= 5 ? "耳朵很灵！" : _hits >= 3 ? "不错，继续练" : "慢慢来，多听几遍";
        ResultDetail.Text = $"共 {Rounds} 题，答对 {_hits} 题";
        ResultTip.Text = "小提示：先在心里数「一、二、三」，把三个声音都听完再选，不要听到一半就下结论。";

        _sound.PlayWin();
        await SayShortAsync($"完成啦，答对{_hits}题");
    }

    private void OnReplayAll(object sender, EventArgs e)
    {
        ResultLayer.IsVisible = false;
        _round = 0;
        _hits = 0;
        BeginRound();
    }

    private async void OnBack(object sender, EventArgs e)
    {
        _locked = true;
        await Navigation.PopAsync();
    }
}
