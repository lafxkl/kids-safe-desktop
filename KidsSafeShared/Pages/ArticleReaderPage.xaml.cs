using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 「亲子阅」阅读页：展示一篇安全/习惯图文，支持「读给小朋友听」的 TTS 朗读。
/// 内容均为随包内置或家长服务器审核下发的儿童友好文本。
/// </summary>
public partial class ArticleReaderPage : ContentPage
{
    private readonly ContentItem _item;
    private bool _reading;
    private CancellationTokenSource? _cts;
    private int _readIndex;

    public ArticleReaderPage(ContentItem item)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _item = item;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        TitleLabel.Text = _item.Title;
        DescLabel.Text = _item.Desc;
        PosterLabel.Text = _item.Emoji;
        HeaderFrame.BackgroundColor = Color.FromArgb(_item.Color);

        // 标签
        TagLayout.Children.Clear();
        foreach (var tag in _item.Tags)
        {
            TagLayout.Children.Add(new Frame
            {
                Padding = new Thickness(10, 4),
                CornerRadius = 14,
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                HasShadow = false,
                Content = new Label
                {
                    Text = "#" + tag,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#2E7D32")
                }
            });
        }

        // 正文
        BodyList.Children.Clear();
        for (var i = 0; i < _item.Body.Length; i++)
        {
            BodyList.Children.Add(new Label
            {
                Text = _item.Body[i],
                FontSize = 18,
                TextColor = Color.FromArgb("#37474F"),
                LineBreakMode = LineBreakMode.WordWrap,
                Padding = new Thickness(4, 2)
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
    }

    private async void OnReadToggle(object sender, EventArgs e)
    {
        await ReadButton.ScaleTo(0.95, 60);
        await ReadButton.ScaleTo(1.0, 60);

        if (_reading)
        {
            _cts?.Cancel();
            _reading = false;
            ReadButton.Text = "🔊 读给小朋友";
            return;
        }

        if (_item.Body.Length == 0) return;

        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _reading = true;
        ReadButton.Text = "⏸ 暂停";

        _ = Task.Run(async () =>
        {
            for (var i = _readIndex; i < _item.Body.Length; i++)
            {
                if (cts.Token.IsCancellationRequested) return;
                _readIndex = i;
                await Tts.SpeakAsync(_item.Body[i], "zh");
            }
            if (!cts.Token.IsCancellationRequested)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _reading = false;
                    ReadButton.Text = "🔊 读给小朋友";
                    _readIndex = 0;
                });
        });
    }

    private async void OnBack(object sender, EventArgs e)
    {
        _cts?.Cancel();
        await Navigation.PopAsync();
    }
}
