using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 抑郁症专区（公益科普长文，离线内置）。
///
/// 内容分三个标签页：认识与发现（怎么早识别）、预防与治疗（家庭预防 + 2025 版
/// 指南的全病程治疗）、数据与求助（官方统计数据 + 全国心理援助与自杀干预热线）。
///
/// 加载方式与 <see cref="ChildPsychPage"/> / <see cref="ParentGuidePage"/> 保持一致：
/// ① 等 WebView 的 handler 就绪再赋 Source；② 成功前显示加载提示；
/// ③ 失败或超时给出可读说明 + 重试；④ 每次打开压回顶部。
///
/// 与其他两个网页页的一处差别：本页含 tel: 链接（热线号码），
/// 需要在 OnNavigating 里放行并交给系统拨号器，不能按外链一律拦掉。
/// </summary>
public partial class DepressionPage : ContentPage
{
    /// <summary>网页迟迟没有回音时的兜底时长。</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(12);

    private string _homeUrl = string.Empty;
    private bool _loaded;

    /// <summary>加载批次号：重试时 +1，旧批次回调据此自行作废。</summary>
    private int _loadToken;

    /// <summary>本批次是否已成功加载，供超时兜底判断。</summary>
    private bool _succeeded;

    public DepressionPage()
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
            _homeUrl = await Task.Run(() => DepressionAssets.EnsureExtractedAsync());
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
        // 先复位到第一屏：WebView2 会恢复会话历史，上次停在哪一屏、滚到哪，
        // 重开时会原样回来，看起来像内容出错。
        try { DocView.Eval("window.__dpReset && window.__dpReset();"); } catch { }

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

        // 热线号码交给系统拨号器。安卓上 tel: 能直接进拨号盘，
        // Windows 上通常没有关联程序，失败就静默忽略——号码本身在页面上也写得很大。
        if (e.Url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            _ = DialAsync(e.Url);
            return;
        }

        // 只放行本地文件；其它链接交给系统浏览器，避免在应用内打开陌生网页
        if (e.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            return;

        if (e.Url.StartsWith('#')) return;

        e.Cancel = true;
        _ = OpenExternalAsync(e.Url);
    }

    private async Task DialAsync(string telUrl)
    {
        try { await Launcher.Default.TryOpenAsync(telUrl); }
        catch { /* 忽略：没有可用的拨号程序 */ }
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

    /// <summary>跳到「数据与求助」那一屏（内含全部热线号码）。</summary>
    private void OnHelp(object sender, EventArgs e)
    {
        try
        {
            DocView.Eval("var b=document.querySelector('.tab-btn[data-view=\"data\"]');if(b){b.click();}");
        }
        catch { /* 网页还没就绪时忽略 */ }

        // 页面自己会置顶，这里再从原生侧重压几次：
        // 两屏高度差很大，个别 WebView 版本仍会因滚动锚定停在中间。
        _ = ScrollTopAfterSwitchAsync();
    }

    private async Task ScrollTopAfterSwitchAsync()
    {
        foreach (var delay in new[] { 80, 240, 500 })
        {
            await Task.Delay(delay);
            try { DocView.Eval("window.scrollTo(0,0);"); } catch { }
        }
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
                awv.SetBackgroundColor(Android.Graphics.Color.ParseColor("#F6F9FC"));
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
