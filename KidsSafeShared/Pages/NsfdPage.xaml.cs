using KidsSafeDesktop.Helpers;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 语音训练（NSFD）：内置离线网页语音测试系统，用 WebView 加载本地已释放的 HTML。
/// 顶部工具栏提供「返回 / 大厅」，并拦截一切外链（只允许 file:// 本地资源）。
/// </summary>
public partial class NsfdPage : ContentPage
{
    private string _homeUrl = string.Empty;
    private bool _loaded;

    public NsfdPage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "语音训练", "🗣️", "nsfd", 2, "做了一次语音训练");
        NavigationPage.SetHasNavigationBar(this, false);

        NsfdView.Navigating += OnNavigating;
        NsfdView.Navigated += OnNavigated;
        ConfigurePlatformWebView();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        LoadingMask.IsVisible = true;
        LoadingMask.IsRunning = true;
        try
        {
            // 释放资源（耗时，放后台），完成后加载大厅
            _homeUrl = await Task.Run(() => NsfdAssets.EnsureExtractedAsync());
            NsfdView.Source = new UrlWebViewSource { Url = _homeUrl };
        }
        finally
        {
            LoadingMask.IsVisible = false;
            LoadingMask.IsRunning = false;
        }
    }

    // ===== 工具栏 =====
    private async void OnBack(object sender, EventArgs e)
    {
        if (NsfdView.CanGoBack)
            NsfdView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private void OnHome(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_homeUrl)) return;
        NsfdView.Source = new UrlWebViewSource { Url = _homeUrl };
    }

    // ===== WebView 生命周期 =====
    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // 只允许本地文件；外链交给系统浏览器，避免在应用内打开陌生网页
        if (e.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return;

        e.Cancel = true;
        _ = OpenExternalAsync(e.Url);
    }

    private async Task OpenExternalAsync(string url)
    {
        try { await Browser.Default.OpenAsync(url); }
        catch { /* 忽略：无法打开浏览器 */ }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // 网页加载完成后刷新护眼遮罩（WebView 可能覆盖在最上层）
        EyeCare.Refresh();
    }

    // ===== 平台相关配置 =====
    private void ConfigurePlatformWebView()
    {
        NsfdView.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (NsfdView.Handler?.PlatformView is Android.Webkit.WebView awv)
            {
                awv.Settings.JavaScriptEnabled = true;
                awv.Settings.DomStorageEnabled = true;
                awv.Settings.AllowFileAccess = true;
                awv.Settings.AllowContentAccess = true;
                awv.Settings.AllowFileAccessFromFileURLs = true;
                awv.Settings.AllowUniversalAccessFromFileURLs = true;
                awv.Settings.MediaPlaybackRequiresUserGesture = false;
            }
#endif
        };
    }

    protected override bool OnBackButtonPressed()
    {
        if (NsfdView.CanGoBack)
        {
            NsfdView.GoBack();
            return true;
        }
        _ = Navigation.PopAsync();
        return true;
    }
}
