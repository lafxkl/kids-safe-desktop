using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 教育孩子 36 计（家庭教育方法，离线内置）。
///
/// 内容按 6 类场景（放手与抗挫 / 自信与认可 / 沟通与情绪 / 规则与纠错 /
/// 学习与榜样 / 品格与生活）各 6 条，共 36 条，网页内可筛选。
///
/// 加载方式与 <see cref="DepressionPage"/> / <see cref="ChildPsychPage"/> 一致：
/// ① 等 WebView 的 handler 就绪再赋 Source；② 成功前显示加载提示；
/// ③ 失败或超时给出可读说明 + 重试；④ 每次打开压回顶部。
/// 本页没有 tel: 链接，外链一律交给系统浏览器。
/// </summary>
public partial class Edu36Page : ContentPage
{
    /// <summary>网页迟迟没有回音时的兜底时长。</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(12);

    private string _homeUrl = string.Empty;
    private bool _loaded;

    /// <summary>加载批次号：重试时 +1，旧批次回调据此自行作废。</summary>
    private int _loadToken;

    /// <summary>本批次是否已成功加载，供超时兜底判断。</summary>
    private bool _succeeded;

    public Edu36Page()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        DocView.Navigating += OnNavigating;
        DocView.Navigated += OnNavigated;
        ConfigurePlatformWebView();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ServiceProvider.Get<IScreenTimeService>().LimitReached += OnLimitReached;
        if (ServiceProvider.Get<IScreenTimeService>().IsLockedOut)
            _ = GoLockoutAsync();

        if (_loaded) return;
        _loaded = true;

        // 先让转场动画跑完再建 WebView，避免 WebView2 初始化和转场抢 UI 线程。
        await NavFx.AfterTransitionAsync();
        await LoadDocAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached;
    }

    // ===== 加载 =====

    private async Task LoadDocAsync()
    {
        var token = ++_loadToken;
        _succeeded = false;

        ErrorPanel.IsVisible = false;
        DocView.IsVisible = true;
        LoadingMask.IsVisible = true;

        try
        {
            _homeUrl = await Task.Run(() => Edu36Assets.EnsureExtractedAsync());
            if (token != _loadToken) return;

            // Windows 上 WebView2 的原生控件可能晚于 OnAppearing 建好，
            // 过早赋 Source 会被丢掉，表现为永久空白。
            await WaitForHandlerAsync();
            if (token != _loadToken) return;

            DocView.Source = new UrlWebViewSource { Url = _homeUrl };
            StartWatchdog(token);
        }
        catch (Exception ex)
        {
            ShowError(token, $"读取安装包里的页面文件失败。{ex.GetType().Name}");
        }
    }

    /// <summary>等待 WebView 的原生控件就绪，最多约 3 秒。</summary>
    private async Task WaitForHandlerAsync()
    {
        for (var i = 0; i < 60 && DocView.Handler is null; i++)
            await Task.Delay(50);
    }

    private async void StartWatchdog(int token)
    {
        await Task.Delay(LoadTimeout);
        if (token != _loadToken || _succeeded) return;

        ShowError(token, "网页在 12 秒内没有回应。可能是系统的网页组件（WebView2）没有启动起来。");
    }

    private void ShowError(int token, string detail)
    {
        if (token != _loadToken) return;

        LoadingMask.IsVisible = false;
        DocView.IsVisible = false;
        ErrorDetail.Text = $"内容就在安装包里，不需要联网。\n{detail}";
        ErrorPanel.IsVisible = true;
    }

    // ===== WebView 事件 =====

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
        {
            ShowError(_loadToken,
                $"系统网页组件返回了 {e.Result}，可能是组件的用户数据目录不可写。");
            return;
        }

        _succeeded = true;
        LoadingMask.IsVisible = false;
        ErrorPanel.IsVisible = false;
        DocView.IsVisible = true;

        // WebView 会沿用上一次的滚动位置；布局是渐进的，
        // 只置顶一次会被随后的排版覆盖，所以隔几段再压一次。
        _ = ForceScrollToTopAsync();
    }

    private async Task ForceScrollToTopAsync()
    {
        foreach (var delay in new[] { 0, 120, 350, 800, 1500, 2600 })
        {
            if (delay > 0) await Task.Delay(delay);

            try
            {
                DocView.Eval("window.scrollTo(0,0);document.documentElement.scrollTop=0;document.body.scrollTop=0;");
            }
            catch { /* 网页还没就绪时忽略 */ }
        }
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Url)) return;

        // 只放行本地文件；其它链接交给系统浏览器，避免在应用内打开陌生网页
        if (e.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            return;

        if (e.Url.StartsWith('#')) return;

        e.Cancel = true;
        _ = OpenExternalAsync(e.Url);
    }

    private async Task OpenExternalAsync(string url)
    {
        try { await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(url); }
        catch { /* 忽略：无法打开浏览器 */ }
    }

    // ===== 工具栏 =====

    private async void OnBack(object sender, EventArgs e)
    {
        if (DocView.CanGoBack)
            DocView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private void OnTop(object sender, EventArgs e)
    {
        try { DocView.Eval("window.scrollTo({top:0,behavior:'smooth'});"); } catch { }
    }

    private void OnRetry(object sender, EventArgs e) => _ = LoadDocAsync();

    // ===== 平台相关配置 =====

    /// <summary>
    /// 安卓 WebView 默认不允许 file:// 页面读本地资源，也不开 JS；
    /// 不开的后果就是整页空白。这里按其它网页页一致的设置补齐。
    /// </summary>
    private void ConfigurePlatformWebView()
    {
        DocView.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (DocView.Handler?.PlatformView is Android.Webkit.WebView awv)
            {
                var s = awv.Settings;
                s.JavaScriptEnabled = true;
                s.DomStorageEnabled = true;
                s.AllowFileAccess = true;
                s.AllowContentAccess = true;
                s.AllowFileAccessFromFileURLs = true;

                s.UseWideViewPort = true;
                s.LoadWithOverviewMode = false;
                s.SetSupportZoom(false);
                s.BuiltInZoomControls = false;
                s.DisplayZoomControls = false;
                s.TextZoom = 100;

                awv.OverScrollMode = Android.Views.OverScrollMode.Never;
                awv.SetBackgroundColor(Android.Graphics.Color.ParseColor("#FBF7F0"));
            }
#endif
        };
    }

    // ===== 时长锁屏 =====

    private async void OnLimitReached(object? sender, EventArgs e) => await GoLockoutAsync();

    private async Task GoLockoutAsync()
    {
        if (Navigation.NavigationStack.LastOrDefault() is LockoutPage) return;
        await Navigation.PushAsync(new LockoutPage());
    }
}
