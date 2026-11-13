using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 声音寻宝（视力障碍 · 听觉定位）。
/// 训练逻辑：目标格持续发出固定音高的提示音，孩子凭双耳判断方位并点击对应格子。
/// 无障碍要点：
/// - 全部状态变化都有语音播报（看不见也能独立完成）；
/// - 每格 ≥96dp，远超 WCAG 2.1 SC 2.5.5 的 44×44 / Material 的 48×48 下限；
/// - 答错不扣分、不推进，只提示「再听一听」，避免挫败感中断训练。
/// </summary>
public partial class SoundTreasureGamePage : ContentPage
{
    private const int Rounds = 6;

    /// <summary>九宫格的方位名称（行优先，与 Grid.Row/Column 顺序一致）。</summary>
    private static readonly string[] DirNames =
        { "左上", "上面", "右上", "左边", "中间", "右边", "左下", "下面", "右下" };

    private readonly ISoundService _sound = ServiceProvider.Get<ISoundService>();
    private readonly Random _rnd = new();
    private readonly Border[] _cells = new Border[9];

    private int _round;
    private int _hits;
    private int _target = -1;
    private bool _locked = true;
    private bool _started;
    private IDispatcherTimer? _timer;

    public SoundTreasureGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BuildBoard();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // 从后台切回 / 弹窗关闭也会触发 OnAppearing，不能每次都重开一局
        if (_started) return;
        _started = true;
        BeginRound();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopBeep();
        _locked = true;
    }

    /// <summary>生成 3×3 大格子。用代码生成而不是手写九个 XAML 节点，便于统一调整无障碍属性。</summary>
    private void BuildBoard()
    {
        for (int i = 0; i < 9; i++)
        {
            int idx = i;

            var num = new Label
            {
                Text = (i + 1).ToString(),
                FontSize = 34,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3949AB"),
                HorizontalOptions = LayoutOptions.Center
            };
            var dir = new Label
            {
                Text = DirNames[i],
                FontSize = 13,
                TextColor = Color.FromArgb("#7986CB"),
                HorizontalOptions = LayoutOptions.Center
            };

            var cell = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) },
                Stroke = new SolidColorBrush(Color.FromArgb("#9FA8DA")),
                StrokeThickness = 3,
                BackgroundColor = Colors.White,
                Margin = new Thickness(5),
                MinimumHeightRequest = 96,
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { num, dir }
                }
            };

            SemanticProperties.SetDescription(cell, $"第 {i + 1} 格，{DirNames[i]}");
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnCellAsync(idx);
            cell.GestureRecognizers.Add(tap);

            Grid.SetRow(cell, i / 3);
            Grid.SetColumn(cell, i % 3);
            Board.Children.Add(cell);
            _cells[i] = cell;
        }
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
        ResetCells();

        _target = _rnd.Next(9);
        _locked = true;   // 语音提示期间不接受点击，避免孩子盲点抢先

        await SayShortAsync($"第{_round}题。听，声音在哪一格");
        if (!IsLoaded) return;

        _locked = false;
        StartBeep();
    }

    private async Task OnCellAsync(int idx)
    {
        if (_locked || _target < 0) return;

        if (idx == _target)
        {
            _locked = true;
            StopBeep();
            _hits++;

            _sound.PlayNotes(new (double, int)[] { (523.25, 110), (783.99, 110), (1046.5, 220) });
            A11y.Success();
            _cells[idx].BackgroundColor = Color.FromArgb("#A5D6A7");
            FeedbackLabel.Text = $"✅ 找到了！在{DirNames[idx]}";

            await SayShortAsync($"找到了，在{DirNames[idx]}");
            if (!IsLoaded) return;
            await Task.Delay(500);
            BeginRound();
        }
        else
        {
            _sound.PlayNotes(new (double, int)[] { (196.0, 260) });
            A11y.Fail();
            _ = A11y.FlashAsync(_cells[idx], Color.FromArgb("#FFCDD2"), 260);
            FeedbackLabel.Text = "再听一听，声音在别的地方";
            await SayShortAsync("再听一听");
        }
    }

    private void StartBeep()
    {
        StopBeep();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1200);
        _timer.Tick += (_, _) => BeepNow();
        _timer.Start();
        BeepNow();
    }

    private void BeepNow()
    {
        if (_locked) return;
        _sound.PlayNotes(new (double, int)[] { (880.0, 200) });
    }

    private void StopBeep()
    {
        try
        {
            _timer?.Stop();
            _timer = null;
        }
        catch { }
    }

    private void ResetCells()
    {
        foreach (var c in _cells)
            if (c is not null) c.BackgroundColor = Colors.White;
    }

    /// <summary>
    /// 语音朗读但最多等 2.6 秒。设备没装语音包时 SpeakAsync 会立刻失败返回；
    /// 但个别机型会卡在引擎初始化上，若不加超时整局训练就停在那一步。
    /// </summary>
    private static async Task SayShortAsync(string text)
    {
        try { await Task.WhenAny(A11y.SayAsync(text), Task.Delay(2600)); }
        catch { }
    }

    private async Task FinishAsync()
    {
        StopBeep();
        _locked = true;
        _target = -1;

        ResultLayer.IsVisible = true;
        ResultTitle.Text = _hits >= 5 ? "太棒了，听得很准！"
                        : _hits >= 3 ? "做得不错，继续练！"
                        : "慢慢来，多听几次就好";
        ResultDetail.Text = $"共 {Rounds} 题，答对 {_hits} 题";
        ResultTip.Text = "小提示：闭着眼睛听。左右两只耳朵听到的声音，哪边更响，声音就更靠近哪一边。";

        _sound.PlayWin();
        await SayShortAsync($"完成啦，答对{_hits}题，一共{Rounds}题");
    }

    private void OnReplayAll(object sender, EventArgs e)
    {
        ResultLayer.IsVisible = false;
        _round = 0;
        _hits = 0;
        BeginRound();
    }

    private void OnReplaySound(object sender, EventArgs e)
    {
        if (_locked || _target < 0) return;
        _sound.PlayNotes(new (double, int)[] { (880.0, 260) });
    }

    private async void OnBack(object sender, EventArgs e)
    {
        StopBeep();
        _locked = true;
        await Navigation.PopAsync();
    }
}
