using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 故事阅读页：展示从家长服务器代理取回的故事正文，支持「读给小朋友」TTS 朗读。
/// 内容来自家长自建服务器代理的故事大全，密钥不经过儿童客户端。
/// </summary>
public partial class StoryReaderPage : ContentPage
{
    private readonly IStoryService _story;
    private readonly long _storyId;
    private readonly string _title;
    private List<string> _paragraphs = new();

    private bool _reading;
    private CancellationTokenSource? _cts;
    private int _readIndex;

    public StoryReaderPage(long storyId, string title, string type) : this()
    {
        _storyId = storyId;
        _title = title;
        _paragraphs = new List<string> { type }; // 暂存，加载后替换
    }

    private StoryReaderPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _story = ServiceProvider.Get<IStoryService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TitleLabel.Text = _title;

        Busy.IsVisible = true;
        Busy.IsRunning = true;
        var detail = await _story.GetDetailAsync(_storyId);
        Busy.IsRunning = false;
        Busy.IsVisible = false;

        if (detail is null)
        {
            TitleLabel.Text = _title;
            BodyList.Children.Clear();
            BodyList.Children.Add(new Label
            {
                Text = "😢 故事加载失败了，请返回重试。",
                FontSize = 16,
                TextColor = Color.FromArgb("#8D6E63"),
                HorizontalTextAlignment = TextAlignment.Center
            });
            ReadButton.IsEnabled = false;
            return;
        }

        TypeLabel.Text = "🏷️ " + detail.Type;
        TimeLabel.Text = "⏱️ " + detail.ReadTime;
        RenderBody(detail.Content);
    }

    private void RenderBody(string? content)
    {
        _paragraphs = (content ?? string.Empty)
            .Split('\n')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (_paragraphs.Count == 0)
            _paragraphs.Add("（这个故事还没有正文内容）");

        BodyList.Children.Clear();
        foreach (var para in _paragraphs)
        {
            BodyList.Children.Add(new Label
            {
                Text = para,
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

        if (_paragraphs.Count == 0) return;

        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _reading = true;
        ReadButton.Text = "⏸ 暂停";

        _ = Task.Run(async () =>
        {
            for (var i = _readIndex; i < _paragraphs.Count; i++)
            {
                if (cts.Token.IsCancellationRequested) return;
                _readIndex = i;
                await Tts.SpeakAsync(_paragraphs[i], "zh");
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
