using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>朗读文本的一行，负责显示当前朗读位置。</summary>
public class SpeechLine : INotifyPropertyChanged
{
    private bool _isCurrent;

    public string Text { get; set; } = string.Empty;

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(Attributes));
        }
    }

    public Color Background => _isCurrent ? Color.FromArgb("#FFE0B2") : Colors.Transparent;
    public Color TextColor => _isCurrent ? Color.FromArgb("#E65100") : Color.FromArgb("#6D4C41");
    public FontAttributes Attributes => _isCurrent ? FontAttributes.Bold : FontAttributes.None;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 「宝宝听」播放页（参考「熊宝儿歌故事」快应用的播放体验重写）：
/// · 旋转 CD 封面 + 青绿渐变背景；
/// · 歌单抽屉（底部弹出，当前曲高亮，点击切换）；
/// · 上一首/下一首首尾守卫、播完自动连播；
/// · 儿歌/环境音走原生 <see cref="IAudioPlayerService"/>，朗读内容走 TTS。
/// </summary>
public partial class AudioPlayerPage : ContentPage
{
    private readonly KidsAudioGroup _group;
    private readonly IAudioPlayerService _player;
    private KidsAudioItem _item;

    private readonly ObservableCollection<SpeechLine> _lines = new();
    private CancellationTokenSource? _speechCts;
    private int _speechIndex;
    private bool _playing;
    private bool _prepared;
    private bool _seeking;
    private bool _cdSpinning;
    private bool _drawerOpen;

    public AudioPlayerPage(KidsAudioGroup group, KidsAudioItem item)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _group = group;
        _item = item;
        _player = ServiceProvider.Get<IAudioPlayerService>();
        _player.Ended += (_, _) => OnPlaybackEnded();

        BindableLayout.SetItemsSource(LineContainer, _lines);

        _progressTimer = Dispatcher.CreateTimer();
        _progressTimer.Interval = TimeSpan.FromMilliseconds(400);
        _progressTimer.Tick += (_, _) => RefreshProgress();
        _progressTimer.Start();
    }

    private IDispatcherTimer _progressTimer = null!;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_prepared) return;
        _prepared = true;

        await PrepareAsync(_item);
        await PlayAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAll();
        StopCdSpin();
    }

    // ── 装载 ────────────────────────────────────────────────
    private async Task PrepareAsync(KidsAudioItem item)
    {
        _item = item;
        StopAll();

        var isSpeech = item.Kind == KidsAudioKind.Speech;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TitleLabel.Text = $"{_group.Emoji} {item.Title}";
            SongPanel.IsVisible = !isSpeech;
            SpeechPanel.IsVisible = isSpeech;
            ModeLabel.Text = isSpeech
                ? (item.Locale == "en" ? "英文朗读" : "中文朗读")
                : (item.Kind == KidsAudioKind.Ambient ? "循环播放" : "音乐盒");

            CoverLabel.Text = item.Emoji;
            CdCover.BackgroundColor =
                Color.FromArgb(item.Kind == KidsAudioKind.Ambient ? "#5E35B1" : "#004D40");
            NameLabel.Text = item.Title;
            DescLabel.Text = item.Desc;
            LoopLabel.IsVisible = item.Loop;
            HintLabel.Text = item.Hint;
            CenterLabel.Text = string.Empty;
            DurationLabel.Text = "00:00";
            PositionLabel.Text = "00:00";
            SeekSlider.Value = 0;
            SeekSlider.IsEnabled = !isSpeech;
        });

        // 更新全局迷你播放器
        PlaybackHub.Set(item.Emoji, item.Title, false,
            () => _ = Navigation.PushAsync(new AudioPlayerPage(_group, item)));

        if (isSpeech)
        {
            _speechIndex = 0;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _lines.Clear();
                foreach (var t in item.Lines)
                    _lines.Add(new SpeechLine { Text = t });
                Highlight(-1);
            });
            await CheckTtsAsync();
        }
        else
        {
            try
            {
                await KidsAudioAssets.EnsureExtractedAsync();
                var src = !string.IsNullOrEmpty(item.AudioUrl)
                    ? item.AudioUrl!
                    : KidsAudioAssets.LocalPath(item.FileName!);
                await _player.SetSourceAsync(src, item.Loop);
            }
            catch (Exception)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DescLabel.Text = "这个音频没能打开，换一个试试～";
                });
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TtsWarnPanel.IsVisible = false;
                DurationLabel.Text = _player.DurationSeconds > 0 ? FormatTime(_player.DurationSeconds) : "00:00";
            });
        }
    }

    private async Task CheckTtsAsync()
    {
        var (available, detail) = await Tts.ProbeAsync();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TtsWarnPanel.IsVisible = !available;
            TtsWarnLabel.Text = $"当前设备没有可用的语音包，朗读内容可能无法出声。\n{detail}";
        });
    }

    // ── 播放控制 ────────────────────────────────────────────
    private async Task PlayAsync()
    {
        if (_item.Kind == KidsAudioKind.Speech)
            await StartSpeechAsync(_speechIndex);
        else
            _player.Play();

        SetPlaying(true);
    }

    private void PauseAll()
    {
        if (_item.Kind == KidsAudioKind.Speech)
        {
            _speechCts?.Cancel();
            _speechIndex = Math.Clamp(_speechIndex, 0, Math.Max(0, _item.Lines.Length - 1));
        }
        else
        {
            _player.Pause();
        }
        SetPlaying(false);
    }

    private void StopAll()
    {
        _speechCts?.Cancel();
        _speechCts = null;
        try { _player.Stop(); } catch { }
        SetPlaying(false);
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        PlaybackHub.UpdatePlaying(playing);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlayButton.Text = playing ? "⏸" : "▶️";
            PlayButton.BackgroundColor = playing ? Color.FromArgb("#00695C") : Color.FromArgb("#00897B");
        });
        if (playing) StartCdSpin();
        else StopCdSpin();
    }

    // ── 语音朗读 ────────────────────────────────────────────
    private async Task StartSpeechAsync(int startIndex)
    {
        if (_item.Lines.Length == 0) return;

        _speechCts?.Cancel();
        var cts = new CancellationTokenSource();
        _speechCts = cts;
        var token = cts.Token;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Highlight(startIndex);
            CenterLabel.Text = $"第 {startIndex + 1} / {_item.Lines.Length} 句";
        });

        try
        {
            for (var i = startIndex; i < _item.Lines.Length; i++)
            {
                if (token.IsCancellationRequested) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _speechIndex = i;
                    Highlight(i);
                    CenterLabel.Text = $"第 {i + 1} / {_item.Lines.Length} 句";
                    SeekSlider.Value = _item.Lines.Length <= 1 ? 1 : (double)i / (_item.Lines.Length - 1);
                });

                await Tts.SpeakAsync(_item.Lines[i], _item.Locale);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception) { /* 朗读异常不应导致页面崩溃 */ }

        if (token.IsCancellationRequested) return;
        SetPlaying(false);
    }

    private void Highlight(int index)
    {
        if (_lines.Count == 0) return;
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].IsCurrent = i == index;
    }

    // ── 播放进度 ────────────────────────────────────────────
    private void RefreshProgress()
    {
        if (_item is null || _item.Kind == KidsAudioKind.Speech) return;
        if (!_playing || _seeking) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var d = _player.DurationSeconds;
            if (d <= 0) return;
            var p = _player.PositionSeconds;
            SeekSlider.Value = Math.Clamp(p / d, 0, 1);
            PositionLabel.Text = FormatTime(p);
            DurationLabel.Text = FormatTime(d);
        });
    }

    private void OnPlaybackEnded()
    {
        if (_item.Kind == KidsAudioKind.Ambient) return;

        var idx = _group.Items.IndexOf(_item);
        if (idx >= 0 && idx < _group.Items.Count - 1)
        {
            _ = Task.Run(async () =>
            {
                await PrepareAsync(_group.Items[idx + 1]);
                await PlayAsync();
            });
        }
        else
        {
            SetPlaying(false);
        }
    }

    // ── 歌单抽屉 ────────────────────────────────────────────
    private void OnDrawerOpen(object sender, EventArgs e)
    {
        ServiceProvider.Get<ISoundService>().PlayClick();
        BuildDrawerList();
        DrawerHost.IsVisible = true;
        DrawerPanel.TranslationY = 420;
        DrawerPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
        _drawerOpen = true;
    }

    private void OnDrawerClose(object sender, EventArgs e) => CloseDrawer();

    private void CloseDrawer()
    {
        if (!_drawerOpen) return;
        _drawerOpen = false;
        DrawerPanel.TranslateTo(0, 420, 200, Easing.CubicIn)
                   .ContinueWith(_ => MainThread.InvokeOnMainThreadAsync(() => DrawerHost.IsVisible = false));
    }

    private void BuildDrawerList()
    {
        DrawerList.Children.Clear();
        DrawerTitle.Text = $"播放列表（{_group.Items.Count}）";
        var curIdx = _group.Items.IndexOf(_item);

        for (var i = 0; i < _group.Items.Count; i++)
        {
            var it = _group.Items[i];
            var isCur = i == curIdx;
            var row = new Grid
            {
                Padding = new Thickness(10, 8),
                BackgroundColor = isCur ? Color.FromArgb("#E0F2F1") : Colors.Transparent,
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            row.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => OnDrawerPick(i))
            });

            row.Add(new Label
            {
                Text = isCur ? "🔊" : $"{i + 1}.",
                FontSize = 16,
                TextColor = isCur ? Color.FromArgb("#00897B") : Color.FromArgb("#888"),
                VerticalOptions = LayoutOptions.Center
            });
            row.Add(new Label
            {
                Text = it.Title,
                FontSize = 16,
                FontAttributes = isCur ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isCur ? Color.FromArgb("#00695C") : Color.FromArgb("#37474F"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            }, 1);

            DrawerList.Children.Add(row);
        }
    }

    private async void OnDrawerPick(int index)
    {
        CloseDrawer();
        await Task.Delay(220);
        if (index < 0 || index >= _group.Items.Count) return;
        await PrepareAsync(_group.Items[index]);
        await PlayAsync();
    }

    // ── 旋转 CD ─────────────────────────────────────────────
    private void StartCdSpin()
    {
        if (_cdSpinning) return;
        _cdSpinning = true;
        var anim = new Animation(v => CdCover.Rotation = v, 0, 360);
        anim.Commit(this, "cdSpin", 16, 15000, Easing.Linear, null, () => _cdSpinning);
    }

    private void StopCdSpin()
    {
        _cdSpinning = false;
        this.AbortAnimation("cdSpin");
        CdCover.Rotation = 0;
    }

    // ── 界面事件 ────────────────────────────────────────────
    private async void OnBack(object sender, EventArgs e)
    {
        StopAll();
        await Navigation.PopAsync();
    }

    private async void OnPlayPause(object sender, EventArgs e)
    {
        await PlayButton.ScaleTo(0.9, 60);
        await PlayButton.ScaleTo(1.0, 60);

        if (_playing) PauseAll();
        else await PlayAsync();
    }

    private async void OnPrev(object sender, EventArgs e)
    {
        var idx = _group.Items.IndexOf(_item);
        if (idx <= 0) return;
        await PrepareAsync(_group.Items[idx - 1]);
        await PlayAsync();
    }

    private async void OnNext(object sender, EventArgs e)
    {
        var idx = _group.Items.IndexOf(_item);
        if (idx < 0 || idx >= _group.Items.Count - 1) return;
        await PrepareAsync(_group.Items[idx + 1]);
        await PlayAsync();
    }

    private void OnSeekStarted(object sender, EventArgs e) => _seeking = true;

    private void OnSeekCompleted(object sender, EventArgs e)
    {
        _seeking = false;
        if (_item?.Kind == KidsAudioKind.Speech) return;

        try
        {
            var d = _player.DurationSeconds;
            if (d <= 0) return;
            var pos = TimeSpan.FromSeconds(d * SeekSlider.Value);
            _player.SeekTo(pos);
            PositionLabel.Text = FormatTime(pos.TotalSeconds);
        }
        catch { }
    }

    private async void OnOpenHelp(object sender, EventArgs e)
        => await Navigation.PushAsync(new HelpPage());

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
    }
}
