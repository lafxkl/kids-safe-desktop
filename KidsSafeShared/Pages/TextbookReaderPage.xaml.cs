using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 课本翻页器：一页一张图，左右翻，可切放大模式看细节。
///
/// 三件小事决定了"顺不顺手"：
/// ① 相邻页预热 —— 翻页时下一页往往已经在本地了；
/// ② 快速连点的竞态 —— 用自增的请求号判断，晚到的下载结果不会盖掉新页；
/// ③ 失败可重试 —— 网络抖一下不至于让孩子以为书坏了。
/// </summary>
public partial class TextbookReaderPage : ContentPage
{
    private readonly Textbook _book;
    private readonly ITextbookService _service;

    private int _page = 1;
    private int _requestId;
    private bool _suppressSlider;
    private bool _zoom;

    public TextbookReaderPage(Textbook book)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _book = book;
        _service = ServiceProvider.Get<ITextbookService>();

        TitleLabel.Text = book.Title;
        PageSlider.Maximum = Math.Max(1, book.Pages);
        UpdateChrome();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (PageImage.Source is null) _ = ShowPageAsync(_page);
    }

    // ── 翻页 ───────────────────────────────────────────────────
    private async Task ShowPageAsync(int page)
    {
        page = Math.Clamp(page, 1, Math.Max(1, _book.Pages));
        _page = page;
        var token = ++_requestId;

        UpdateChrome();
        ErrorPanel.IsVisible = false;
        Busy.IsVisible = true;
        Busy.IsRunning = true;

        var path = await _service.GetPageAsync(_book, page);

        // 用户已经翻到别的页了，这次结果丢掉
        if (token != _requestId) return;

        Busy.IsRunning = false;
        Busy.IsVisible = false;

        if (path is null)
        {
            ErrorText.Text = _service.OnlineEnabled
                ? "这一页没有加载出来\n检查一下网络，或者稍后再试"
                : "这一页需要家长服务器\n请让家长打开在线内容";
            ErrorPanel.IsVisible = true;
            return;
        }

        PageImage.Source = ImageSource.FromFile(path);
        if (_zoom) ZoomImage.Source = PageImage.Source;

        _service.Prefetch(_book, page);
    }

    private void UpdateChrome()
    {
        PageLabel.Text = $"{_book.EditionName} · {_book.Title} · 第 {_page} / {_book.Pages} 页";

        _suppressSlider = true;
        PageSlider.Value = _page;
        _suppressSlider = false;

        PrevButton.IsEnabled = _page > 1;
        NextButton.IsEnabled = _page < _book.Pages;
        PrevButton.Opacity = _page > 1 ? 1 : 0.45;
        NextButton.Opacity = _page < _book.Pages ? 1 : 0.45;
    }

    private Task Go(int delta) => ShowPageAsync(_page + delta);

    // ── 交互 ───────────────────────────────────────────────────
    private async void OnPrev(object? sender, EventArgs e) => await Go(-1);

    private async void OnNext(object? sender, EventArgs e) => await Go(1);

    private async void OnTapLeft(object? sender, TappedEventArgs e) => await Go(-1);

    private async void OnTapRight(object? sender, TappedEventArgs e) => await Go(1);

    private async void OnSwipeLeft(object? sender, SwipedEventArgs e) => await Go(1);

    private async void OnSwipeRight(object? sender, SwipedEventArgs e) => await Go(-1);

    private async void OnSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressSlider) return;
        var target = (int)Math.Round(e.NewValue);
        if (target == _page) return;
        await ShowPageAsync(target);
    }

    private void OnToggleZoom(object? sender, EventArgs e)
    {
        _zoom = !_zoom;
        ZoomButton.Text = _zoom ? "🔍 还原" : "🔍 放大";

        ZoomImage.Source = _zoom ? PageImage.Source : null;
        ZoomScroll.IsVisible = _zoom;
        PageImage.IsVisible = !_zoom;
        TapLayer.IsVisible = !_zoom;        // 放大后要能拖动，先把翻页热区撤掉
        NextButton.IsVisible = true;
    }

    private async void OnRetry(object? sender, EventArgs e)
    {
        ErrorPanel.IsVisible = false;
        await ShowPageAsync(_page);
    }

    private async void OnBack(object? sender, EventArgs e) => await Navigation.PopAsync();
}
