using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 故事播放页：逐段 TTS 朗读 + 当前段高亮 + 睡眠定时。
///
/// 为什么按段读而不是整篇一次读：
/// 1) 整篇交给系统 TTS，无法知道「现在读到哪一句」，就没法高亮、没法显示进度；
/// 2) 分段之间可以插入停顿，形成自然的「慢速」效果（系统 TTS 不支持语速参数）；
/// 3) 停止/暂停能在段边界立刻生效，长故事也不会停不下来。
/// </summary>
public partial class StoryPlayerPage : ContentPage
{
    private readonly BedtimeStory _story;
    private readonly List<Label> _paraViews = new();

    private CancellationTokenSource? _cts;
    private int _index;
    private bool _playing;
    private bool _paused;
    private bool _completed;

    /// <summary>每段之间的停顿（毫秒）= 节奏档位的基础值 × 声音风格的停顿倍率。</summary>
    private int _gapMs = 520;
    private int _speed = 1;

    /// <summary>当前声音风格（音高 + 停顿倍率）。默认温柔姐姐。</summary>
    private VoiceStyle _voice = VoiceStyles.Default;

    private IDispatcherTimer? _sleepTimer;
    private DateTime? _sleepAt;

    private static readonly Color ActiveBg = Color.FromArgb("#2E3B7E");
    private static readonly Color IdleText = Color.FromArgb("#C9D0EE");
    private static readonly Color ReadText = Color.FromArgb("#7B84B0");

    public StoryPlayerPage(BedtimeStory story)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _story = story;

        TitleLabel.Text = $"{story.Emoji} {story.Title}";
        TagLabel.Text = $"{story.Category} · {story.Paragraphs.Length} 段 · {story.MinutesText}";

        _voice = VoiceStyles.Current;
        ApplyGap();

        BuildParagraphs();
        BuildSpeedBar();
        BuildVoiceBar();
        BuildTimerBar();
        UpdateFavButton();
        UpdateProgress();
    }

    // ── 构建 ────────────────────────────────────────────────

    private void BuildParagraphs()
    {
        ParaHost.Children.Clear();
        _paraViews.Clear();

        foreach (var text in _story.Paragraphs)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 16.5,
                LineHeight = 1.85,
                TextColor = IdleText,
                Padding = new Thickness(10, 8),
                BackgroundColor = Colors.Transparent
            };
            _paraViews.Add(label);
            ParaHost.Children.Add(label);
        }

        ParaHost.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#2A3568"),
            Margin = new Thickness(0, 12)
        });

        ParaHost.Children.Add(new Label
        {
            Text = "🌱 " + _story.Moral,
            FontSize = 13.5,
            TextColor = Color.FromArgb("#FFD98A"),
            Padding = new Thickness(10, 4)
        });
    }

    private void BuildSpeedBar()
    {
        SpeedBar.Children.Clear();
        var options = new[] { ("🐢 慢速", 0), ("🌿 正常", 1), ("🐇 稍快", 2) };

        foreach (var (text, idx) in options)
        {
            var btn = new Button
            {
                Text = text,
                FontSize = 12.5,
                HeightRequest = 34,
                Padding = new Thickness(12, 0),
                CornerRadius = 17,
                CommandParameter = idx
            };
            btn.Clicked += (_, _) =>
            {
                _speed = idx;
                ApplyGap();
                StyleSpeedBar();
            };
            SpeedBar.Children.Add(btn);
        }
        StyleSpeedBar();
    }

    /// <summary>节奏档位的基础停顿（未乘风格倍率）。</summary>
    private int BaseGap() => _speed switch { 0 => 1100, 1 => 520, _ => 150 };

    /// <summary>把「节奏档位 × 声音风格」合成为实际停顿。</summary>
    private void ApplyGap()
    {
        _gapMs = (int)Math.Round(BaseGap() * _voice.GapScale);
        // 太快会听不清段与段的分界，太慢会让人以为卡住了 —— 都夹在合理区间
        _gapMs = Math.Clamp(_gapMs, 80, 1800);
    }

    private void BuildVoiceBar()
    {
        VoiceBar.Children.Clear();

        foreach (var style in VoiceStyles.All)
        {
            var btn = new Button
            {
                Text = $"{style.Emoji} {style.Name}",
                FontSize = 12.5,
                HeightRequest = 34,
                Padding = new Thickness(12, 0),
                CornerRadius = 17,
                CommandParameter = style.Key
            };
            btn.Clicked += (_, _) =>
            {
                _voice = style;
                VoiceStyles.Set(style);
                ApplyGap();
                StyleVoiceBar();
                VoiceHint.Text = style.Desc;
            };
            VoiceBar.Children.Add(btn);
        }

        StyleVoiceBar();
        VoiceHint.Text = _voice.Desc;
    }

    private void StyleVoiceBar()
    {
        foreach (var child in VoiceBar.Children)
        {
            if (child is not Button b) continue;
            var on = string.Equals(b.CommandParameter as string, _voice.Key, StringComparison.OrdinalIgnoreCase);
            b.BackgroundColor = on ? Color.FromArgb("#FFE9A8") : Color.FromArgb("#1C244E");
            b.TextColor = on ? Color.FromArgb("#3A2C00") : Color.FromArgb("#AAB2D8");
            b.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void StyleSpeedBar()
    {
        foreach (var child in SpeedBar.Children)
        {
            if (child is not Button b) continue;
            var on = (int)(b.CommandParameter ?? 1) == _speed;
            b.BackgroundColor = on ? Color.FromArgb("#8AB4FF") : Color.FromArgb("#1C244E");
            b.TextColor = on ? Color.FromArgb("#0A0E2A") : Color.FromArgb("#AAB2D8");
            b.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void BuildTimerBar()
    {
        TimerBar.Children.Clear();
        var options = new[] { (5, "5 分钟"), (10, "10 分钟"), (15, "15 分钟"), (0, "关闭") };

        foreach (var (minutes, text) in options)
        {
            var btn = new Button
            {
                Text = text,
                FontSize = 12.5,
                HeightRequest = 34,
                Padding = new Thickness(12, 0),
                CornerRadius = 17,
                CommandParameter = minutes
            };
            btn.Clicked += (_, _) => SetSleepTimer(minutes);
            TimerBar.Children.Add(btn);
        }
        StyleTimerBar(0);
    }

    private void StyleTimerBar(int activeMinutes)
    {
        foreach (var child in TimerBar.Children)
        {
            if (child is not Button b) continue;
            var on = (int)(b.CommandParameter ?? 0) == activeMinutes && activeMinutes > 0;
            b.BackgroundColor = on ? Color.FromArgb("#FFD98A") : Color.FromArgb("#1C244E");
            b.TextColor = on ? Color.FromArgb("#3A2C00") : Color.FromArgb("#AAB2D8");
            b.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    // ── 播放 ────────────────────────────────────────────────

    private async void OnPlay(object sender, EventArgs e)
    {
        if (_playing && _paused)
        {
            // 继续：从当前段重新读
            await PlayFromAsync(_index);
            return;
        }

        if (_completed) _index = 0;

        await PlayFromAsync(_index);
    }

    private async Task PlayFromAsync(int start)
    {
        CancelSpeak();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _playing = true;
        _paused = false;
        SetButtons();
        GoodNightLayer.IsVisible = false;

        for (var i = Math.Max(0, start); i < _story.Paragraphs.Length; i++)
        {
            if (token.IsCancellationRequested) break;

            _index = i;
            Highlight(i);
            UpdateProgress();

            try
            {
                await Tts.SpeakAsync(_story.Paragraphs[i], "zh", token, _voice.Pitch);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Story] 朗读第 {i + 1} 段失败：{ex.Message}");
            }

            if (token.IsCancellationRequested) break;

            try { await Task.Delay(_gapMs, token); }
            catch (OperationCanceledException) { break; }
        }

        if (token.IsCancellationRequested) return;

        // 全部读完
        _playing = false;
        _paused = false;
        _completed = true;
        _index = _story.Paragraphs.Length;
        SetButtons();
        await CompleteAsync();
    }

    private void OnPause(object sender, EventArgs e)
    {
        if (!_playing) return;
        _paused = true;
        _playing = false;
        CancelSpeak();
        SetButtons();
        ProgressLabel.Text = $"已暂停 · 第 {Math.Min(_index + 1, _story.Paragraphs.Length)} / {_story.Paragraphs.Length} 段";
    }

    private void OnStop(object sender, EventArgs e)
    {
        CancelSpeak();
        _playing = false;
        _paused = false;
        _index = 0;
        ResetHighlight();
        UpdateProgress();
        SetButtons();
        ProgressLabel.Text = "准备朗读";
    }

    private void CancelSpeak()
    {
        try { _cts?.Cancel(); } catch { /* 已释放 */ }
        _cts?.Dispose();
        _cts = null;
    }

    private void SetButtons()
    {
        PlayBtn.Text = _paused ? "▶ 继续" : "▶ 播放";
        PlayBtn.IsEnabled = !_playing;
        PauseBtn.IsEnabled = _playing;
    }

    private void Highlight(int index)
    {
        for (var i = 0; i < _paraViews.Count; i++)
        {
            var label = _paraViews[i];
            if (i == index)
            {
                label.BackgroundColor = ActiveBg;
                label.TextColor = Colors.White;
            }
            else if (i < index)
            {
                label.BackgroundColor = Colors.Transparent;
                label.TextColor = ReadText;
            }
            else
            {
                label.BackgroundColor = Colors.Transparent;
                label.TextColor = IdleText;
            }
        }

        if (index >= 0 && index < _paraViews.Count)
            _ = TextScroll.ScrollToAsync(_paraViews[index], ScrollToPosition.Center, true);
    }

    private void ResetHighlight()
    {
        foreach (var label in _paraViews)
        {
            label.BackgroundColor = Colors.Transparent;
            label.TextColor = IdleText;
        }
    }

    private void UpdateProgress()
    {
        var total = _story.Paragraphs.Length;
        var done = Math.Clamp(_index, 0, total);

        if (_playing)
            ProgressLabel.Text = $"正在读 · 第 {Math.Min(_index + 1, total)} / {total} 段";
        else if (!_completed && !_paused)
            ProgressLabel.Text = "准备朗读";

        var ratio = total == 0 ? 0 : done / (double)total;
        var usable = (Width > 100 ? Width : 360) - 56;
        ProgressFill.WidthRequest = Math.Max(0, usable * ratio);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 100) UpdateProgress();
    }

    // ── 完成 ────────────────────────────────────────────────

    private async Task CompleteAsync()
    {
        ProgressFill.WidthRequest = Math.Max(0, (Width > 100 ? Width : 360) - 56);
        ProgressLabel.Text = $"读完了 · 共 {_story.Paragraphs.Length} 段";

        // 同一个故事每天只给一次星星，避免反复刷
        var key = $"story_done_{_story.Id}_{DateTime.Today:yyyyMMdd}";
        var already = false;
        try { already = Preferences.Get(key, false); } catch { /* 忽略 */ }

        var stars = 0;
        if (!already)
        {
            stars = 2;
            try { Preferences.Set(key, true); } catch { /* 忽略 */ }
            StarCenter.Award("story", _story.Id, _story.Title, _story.Emoji, stars, "听完一个故事");
        }

        GoodNightSub.Text = stars > 0
            ? $"故事讲完了，该睡觉啦～\n\n⭐ 听完一个故事，+{stars} 颗星星"
            : "故事讲完了，该睡觉啦～";

        await Task.Delay(300);
        GoodNightLayer.IsVisible = true;
        GoodNightLayer.Opacity = 0;
        await GoodNightLayer.FadeTo(1, 700);

        // 读完后自动停掉定时器，避免残留计时
        StopSleepTimer();
    }

    private void OnCloseGoodNight(object sender, EventArgs e)
    {
        GoodNightLayer.IsVisible = false;
        _ = Navigation.PopAsync();
    }

    // ── 睡眠定时 ────────────────────────────────────────────

    private void SetSleepTimer(int minutes)
    {
        StopSleepTimer();

        if (minutes <= 0)
        {
            StyleTimerBar(0);
            TimerLabel.Text = string.Empty;
            return;
        }

        _sleepAt = DateTime.Now.AddMinutes(minutes);
        StyleTimerBar(minutes);

        _sleepTimer = Dispatcher.CreateTimer();
        _sleepTimer.Interval = TimeSpan.FromSeconds(1);
        _sleepTimer.Tick += OnSleepTick;
        _sleepTimer.Start();
        OnSleepTick(null, EventArgs.Empty);
    }

    private void OnSleepTick(object? sender, EventArgs e)
    {
        if (_sleepAt is null) return;

        var left = _sleepAt.Value - DateTime.Now;
        if (left <= TimeSpan.Zero)
        {
            StopSleepTimer();
            CancelSpeak();
            _playing = false;
            _paused = false;
            SetButtons();

            GoodNightSub.Text = "时间到啦，该睡觉了～\n\n晚安，好梦。";
            GoodNightLayer.IsVisible = true;
            return;
        }

        TimerLabel.Text = $"🌙 剩 {left.Minutes}:{left.Seconds:00}";
    }

    private void StopSleepTimer()
    {
        if (_sleepTimer is not null)
        {
            _sleepTimer.Stop();
            _sleepTimer.Tick -= OnSleepTick;
            _sleepTimer = null;
        }
        _sleepAt = null;
        if (TimerLabel is not null) TimerLabel.Text = string.Empty;
        StyleTimerBar(0);
    }

    // ── 收藏 / 返回 ────────────────────────────────────────

    private void UpdateFavButton()
    {
        var on = StoryHousePage.IsFavorite(_story.Id);
        FavBtn.Text = on ? "⭐" : "☆";
        FavBtn.BackgroundColor = on ? Color.FromArgb("#FFD98A") : Color.FromArgb("#1C244E");
        FavBtn.TextColor = on ? Color.FromArgb("#3A2C00") : Color.FromArgb("#FFE9A8");
    }

    private void OnToggleFav(object sender, EventArgs e)
    {
        StoryHousePage.ToggleFavorite(_story.Id);
        UpdateFavButton();
    }

    /// <summary>把故事做成图片卡（家长可以发给孩子在外地打工的爸妈）。</summary>
    private async void OnShareCard(object sender, EventArgs e)
    {
        var subtitle = $"{_story.Category} · {_story.Paragraphs.Length} 段 · {_story.MinutesText}";
        await Navigation.PushAsync(new StoryCardPage(
            _story.Emoji,
            _story.Title,
            subtitle,
            _story.Paragraphs,
            _story.Moral,
            "—— 来自 儿童安全桌面 · 星夜故事屋"));
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelSpeak();
        StopSleepTimer();
        _playing = false;
        _paused = false;
    }
}
