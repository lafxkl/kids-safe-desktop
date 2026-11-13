using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 「宝宝看」播放页。
/// 优先用应用内 WebView 播放 HTML5 视频（跨平台、留在 Kiosk 内、不依赖已崩溃的原生 MediaElement）；
/// 没有视频文件/地址时，自动降级为「视听模式」——海报 + TTS 逐句讲解，完全离线可用。
/// </summary>
public partial class VideoPlayerPage : ContentPage
{
    private readonly ContentItem _item;
    private bool _hasVideo;
    private bool _narrationPlaying;
    private bool _showNarration;
    private CancellationTokenSource? _cts;
    private int _narrIndex;

    public VideoPlayerPage(ContentItem item)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _item = item;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        TitleLabel.Text = $"{_item.Emoji} {_item.Title}";
        PosterLabel.Text = _item.Emoji;
        NameLabel.Text = _item.Title;
        DescLabel.Text = _item.Desc;

        // 解析视频来源：在线地址优先，其次离线打包文件
        var src = ResolveVideoSource();
        _hasVideo = src is not null;

        if (_hasVideo)
        {
            var html = $"<html><body style='margin:0;background:#000;'>" +
                       $"<video src=\"{src}\" controls autoplay style='width:100%;height:100vh;object-fit:contain;'></video>" +
                       $"</body></html>";
            VideoView.Source = new HtmlWebViewSource { Html = html };
            ShowVideo();
        }
        else
        {
            ModeButton.IsVisible = false;
            ShowNarration();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
        try { VideoView.Source = null; } catch { }
    }

    private string? ResolveVideoSource()
    {
        if (!string.IsNullOrEmpty(_item.VideoUrl)) return _item.VideoUrl;
        if (!string.IsNullOrEmpty(_item.VideoFileName))
        {
            try
            {
                var dest = Path.Combine(FileSystem.AppDataDirectory, "video", _item.VideoFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (!File.Exists(dest))
                {
                    using var src = FileSystem.OpenAppPackageFileAsync("Video/" + _item.VideoFileName).Result;
                    using var dst = File.Create(dest);
                    src.CopyTo(dst);
                }
                return dest;
            }
            catch { /* 离线无视频文件则走视听模式 */ }
        }
        return null;
    }

    private void OnToggleMode(object sender, EventArgs e)
    {
        if (_showNarration) ShowVideo(); else ShowNarration();
    }

    private void ShowVideo()
    {
        _showNarration = false;
        VideoView.IsVisible = true;
        NarrationPanel.IsVisible = false;
        ModeButton.Text = "🔊 听讲解";
        ModeButton.IsVisible = true;
    }

    private void ShowNarration()
    {
        _showNarration = true;
        VideoView.IsVisible = false;
        NarrationPanel.IsVisible = true;
        ModeButton.Text = "🎬 看视频";
        ModeButton.IsVisible = _hasVideo;

        // 构建讲解文本
        NarrationList.Children.Clear();
        for (var i = 0; i < _item.NarrationLines.Length; i++)
        {
            var line = new Label
            {
                Text = _item.NarrationLines[i],
                FontSize = 18,
                TextColor = Color.FromArgb("#37474F"),
                Padding = new Thickness(10, 8),
                BackgroundColor = Colors.Transparent
            };
            NarrationList.Children.Add(line);
        }
    }

    private async void OnNarrationToggle(object sender, EventArgs e)
    {
        await NarrationButton.ScaleTo(0.95, 60);
        await NarrationButton.ScaleTo(1.0, 60);

        if (_narrationPlaying)
        {
            _cts?.Cancel();
            _narrationPlaying = false;
            NarrationButton.Text = "▶️ 听讲解";
            return;
        }

        if (_item.NarrationLines.Length == 0)
        {
            NarrationButton.Text = "暂无讲解";
            return;
        }

        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _narrationPlaying = true;
        NarrationButton.Text = "⏸ 暂停";

        _ = Task.Run(async () =>
        {
            for (var i = _narrIndex; i < _item.NarrationLines.Length; i++)
            {
                if (cts.Token.IsCancellationRequested) return;
                var idx = i;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _narrIndex = idx;
                    for (var j = 0; j < NarrationList.Children.Count; j++)
                        if (NarrationList.Children[j] is Label l)
                            l.BackgroundColor = j == idx ? Color.FromArgb("#E3F2FD") : Colors.Transparent;
                });
                await Tts.SpeakAsync(_item.NarrationLines[i], "zh");
            }
            if (!cts.Token.IsCancellationRequested)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _narrationPlaying = false;
                    NarrationButton.Text = "▶️ 听讲解";
                });
        });
    }

    private async void OnBack(object sender, EventArgs e)
    {
        _cts?.Cancel();
        await Navigation.PopAsync();
    }
}
