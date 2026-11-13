using KidsSafeDesktop.Helpers;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 星际科学实验室：内置离线网页（自包含的星空动画 + 12 个趣味实验），
/// 用 WebView 加载本地已释放的 HTML。顶部工具栏提供「返回 / 刷新」，
/// 并拦截一切外链（只允许 file:// 本地资源，避免应用内打开陌生网页）。
/// </summary>
public partial class ScienceLabPage : ContentPage
{
    private string _homeUrl = string.Empty;
    private bool _loaded;

    public ScienceLabPage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "科学实验室", "🚀", "science", 2, "在科学实验室做实验");
        NavigationPage.SetHasNavigationBar(this, false);

        LabView.Navigating += OnNavigating;
        LabView.Navigated += OnNavigated;
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
            // 释放资源（耗时，放后台），完成后加载入口页面
            _homeUrl = await Task.Run(() => ScienceLabAssets.EnsureExtractedAsync());
            LabView.Source = new UrlWebViewSource { Url = _homeUrl };
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
        if (LabView.CanGoBack)
            LabView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private void OnReload(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_homeUrl)) return;
        LabView.Source = new UrlWebViewSource { Url = _homeUrl };
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
        LabView.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (LabView.Handler?.PlatformView is Android.Webkit.WebView awv)
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
        if (LabView.CanGoBack)
        {
            LabView.GoBack();
            return true;
        }
        _ = Navigation.PopAsync();
        return true;
    }
}
