using KidsSafeDesktop.Helpers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 节奏光影（聋 / 重听 · 视觉节律与序列记忆）。
/// 训练逻辑：屏幕依次点亮 2×2 色块，孩子按同样顺序点回；全程无声音。
/// 依据：感觉运动同步（sensorimotor synchronization）可以完全经视觉通道建立，
/// 稳定的节律感支持语言韵律、朗读流畅性与注意维持，对听障儿童的语言学习有直接价值。
/// 反馈设计：答对 = 绿色 + 一次长振动；答错 = 红色 + 两次短振动，靠「看 + 手感」就够。
/// </summary>
public partial class RhythmLightGamePage : ContentPage
{
    private const int MaxLevels = 5;   // 序列长度 2..6

    private static readonly Color[] Palette =
    {
        Color.FromArgb("#EF5350"), Color.FromArgb("#FFB300"),
        Color.FromArgb("#66BB6A"), Color.FromArgb("#42A5F5")
    };

    private readonly Random _rnd = new();
    private readonly Border[] _blocks = new Border[4];
    private readonly Label[] _labels = new Label[4];

    private List<int> _seq = new();
    private int _input;
    private int _level;
    private bool _playing = true;
    private bool _started;

    private int CurrentLength => _level + 2;

    public RhythmLightGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BuildBoard();
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

    private void BuildBoard()
    {
        for (int i = 0; i < 4; i++)
        {
            int idx = i;

            var num = new Label
            {
                Text = (i + 1).ToString(),
                FontSize = 40,
                FontAttributes = FontAttributes.Bold,
                TextColor = Palette[i],
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var block = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) },
                Stroke = new SolidColorBrush(Palette[i]),
                StrokeThickness = 5,
                BackgroundColor = Colors.White,
                Margin = new Thickness(7),
                MinimumHeightRequest = 110,
                Content = num
            };

            SemanticProperties.SetDescription(block, $"第 {i + 1} 块");
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnBlockAsync(idx);
            block.GestureRecognizers.Add(tap);

            Grid.SetRow(block, i / 2);
            Grid.SetColumn(block, i % 2);
            Board.Children.Add(block);

            _blocks[i] = block;
            _labels[i] = num;
        }
    }

    private async void BeginLevel()
    {
        if (_level >= MaxLevels)
        {
            await FinishAsync();
            return;
        }

        ProgressLabel.Text = $"第 {_level + 1} / {MaxLevels} 关";
        FeedbackLabel.Text = "👀 看好了，记住亮的顺序";

        _seq = new List<int>();
        for (int i = 0; i < CurrentLength; i++) _seq.Add(_rnd.Next(4));

        _input = 0;
        await PlaySequenceAsync();
    }

    private async Task PlaySequenceAsync()
    {
        _playing = true;
        await Task.Delay(400);
        if (!IsLoaded) return;

        foreach (var i in _seq)
        {
            if (!IsLoaded) return;
            _blocks[i].BackgroundColor = Palette[i];
            _labels[i].TextColor = Colors.White;
            await Task.Delay(420);
            if (!IsLoaded) return;
            _blocks[i].BackgroundColor = Colors.White;
            _labels[i].TextColor = Palette[i];
            await Task.Delay(180);
        }

        if (!IsLoaded) return;
        await Task.Delay(220);
        if (!IsLoaded) return;

        _input = 0;
        _playing = false;
        FeedbackLabel.Text = "该你了：按刚才的顺序点";
    }

    private async Task OnBlockAsync(int idx)
    {
        if (_playing) return;

        if (idx == _seq[_input])
        {
            _input++;
            A11y.Tap();
            _ = FlashAsync(idx, Color.FromArgb("#A5D6A7"), 200);
            FeedbackLabel.Text = $"✅ {_input} / {_seq.Count}";

            if (_input >= _seq.Count)
            {
                _playing = true;
                A11y.Success();
                FeedbackLabel.Text = "🎉 全对！";
                await Task.Delay(650);
                if (!IsLoaded) return;

                _level++;
                BeginLevel();
            }
        }
        else
        {
            _playing = true;
            A11y.Fail();
            _ = FlashAsync(idx, Color.FromArgb("#FF8A80"), 260);
            FeedbackLabel.Text = "❌ 顺序不对，再看一遍";
            await Task.Delay(700);
            if (!IsLoaded) return;

            _input = 0;
            await PlaySequenceAsync();
        }
    }

    /// <summary>把色块短暂染成提示色再复原（听障训练里替代「对/错」音效）。</summary>
    private async Task FlashAsync(int idx, Color color, int ms)
    {
        try
        {
            var origin = _blocks[idx].BackgroundColor;
            _blocks[idx].BackgroundColor = color;
            await Task.Delay(ms);
            _blocks[idx].BackgroundColor = origin;
        }
        catch { }
    }

    private async void OnReplay(object sender, EventArgs e)
    {
        if (_playing || _seq.Count == 0) return;
        _input = 0;
        await PlaySequenceAsync();
    }

    private async Task FinishAsync()
    {
        _playing = true;
        ResultLayer.IsVisible = true;
        ResultTitle.Text = "看得真准！";
        ResultDetail.Text = $"完成 5 关，最长跟上了 {MaxLevels + 1} 个色块的顺序";
        ResultTip.Text = "小提示：把亮的顺序在心里念成「一、二、三、四」，节奏稳了，记起来会轻松很多。";

        await Task.Delay(200);
    }

    private void OnReplayAll(object sender, EventArgs e)
    {
        ResultLayer.IsVisible = false;
        _level = 0;
        BeginLevel();
    }

    private async void OnBack(object sender, EventArgs e)
    {
        _playing = true;
        await Navigation.PopAsync();
    }
}
