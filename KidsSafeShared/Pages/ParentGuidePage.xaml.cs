using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 0-15 岁成长指南（给家长看的长文，离线内置）。
///
/// 为什么放进 App 而不是只放官网：家长在儿童模式里被锁住时，最想查的正是
/// 「几岁该学什么」，这时跳浏览器既麻烦、也容易被孩子抢回屏幕。内容随安装包走，
/// 没网也能翻。
///
/// 关于「白屏」：这个页面只有 WebView 一层，一旦 WebView2 初始化慢或失败，
/// 界面上就是一片空白，家长只会以为功能坏了。所以这里做三件事——
/// ① 等 WebView 的 handler 真正就绪后再赋 Source；
/// ② 加载成功前始终显示「正在打开…」，不让空白暴露出来；
/// ③ 失败或超时就切到可读的错误说明 + 重试按钮。
/// </summary>
public partial class ParentGuidePage : ContentPage
{
    /// <summary>网页迟迟没有回音时的兜底时长。</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(12);

    private string _homeUrl = string.Empty;
    private bool _loaded;

    /// <summary>加载批次号：重试时 +1，旧批次回调据此自行作废。</summary>
    private int _loadToken;

    /// <summary>本批次是否已成功加载，供超时兜底判断。</summary>
    private bool _succeeded;

    public ParentGuidePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        GuideView.Navigating += OnNavigating;
        GuideView.Navigated += OnNavigated;
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
        await LoadGuideAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached;
    }

    // ===== 加载 =====

    private async Task LoadGuideAsync()
    {
        var token = ++_loadToken;
        _succeeded = false;

        ErrorPanel.IsVisible = false;
        GuideView.IsVisible = true;
        LoadingMask.IsVisible = true;

        try
        {
            // 释放资源有磁盘 IO，放后台；已释放过时几乎立即返回
            _homeUrl = await Task.Run(() => ParentGuideAssets.EnsureExtractedAsync());
            if (token != _loadToken) return;

            // Windows 上 WebView2 的 handler 可能在页面出现后一小会儿才建好，
            // 过早赋 Source 会被丢弃，表现为永远空白。
            await WaitForHandlerAsync();
            if (token != _loadToken) return;

            GuideView.Source = new UrlWebViewSource { Url = _homeUrl };
            StartWatchdog(token);
        }
        catch (Exception ex)
        {
            ShowError(token, $"读取安装包里的指南文件失败。{ex.GetType().Name}");
        }
    }

    /// <summary>等待 WebView 的原生控件就绪，最多约 3 秒。</summary>
    private async Task WaitForHandlerAsync()
    {
        for (var i = 0; i < 60 && GuideView.Handler is null; i++)
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
        GuideView.IsVisible = false;
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
        GuideView.IsVisible = true;

        // 网页首次绘制后回到顶部：WebView 会沿用上一次的滚动位置，
        // 打开就停在半中间甚至最底部，家长会以为内容丢了。布局是渐进的，
        // 只在 Navigated 里置顶一次会被随后的排版覆盖，所以隔几段再压一次。
        _ = ForceScrollToTopAsync();
    }

    private async Task ForceScrollToTopAsync()
    {
        foreach (var delay in new[] { 0, 120, 350, 800, 1500 })
        {
            if (delay > 0) await Task.Delay(delay);

            try
            {
                // 逐帧逼近顶部：scrollTo 立即生效，scrollIntoView 兜住异步排版后的偏移
                GuideView.Eval("window.scrollTo(0,0);document.documentElement.scrollTop=0;document.body.scrollTop=0;");
            }
            catch { /* 网页还没就绪时忽略 */ }
        }
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // 只放行本地文件；其它链接交给系统浏览器，避免在应用内打开陌生网页
        if (string.IsNullOrEmpty(e.Url) ||
            e.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            return;

        // 页内锚点（#xxx）不跳出去
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
        // 网页内有标签切换和锚点，优先让页面自己后退；退到头了再退出本页
        if (GuideView.CanGoBack)
            GuideView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private void OnTop(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_homeUrl)) return;

        // 指南里有「回到顶部」按钮，但长文翻到中间时它不一定在视野里，
        // 顶栏固定留一个，随时能回开头。
        try { GuideView.Eval("window.scrollTo({top:0,behavior:'smooth'});"); } catch { }
    }

    private void OnReload(object sender, EventArgs e)
    {
        if (ErrorPanel.IsVisible)
        {
            _ = LoadGuideAsync();
            return;
        }

        try { GuideView.Reload(); }
        catch { _ = LoadGuideAsync(); }
    }

    private void OnRetry(object sender, EventArgs e) => _ = LoadGuideAsync();

    // ===== 平台相关配置 =====

    /// <summary>
    /// 安卓 WebView 默认不允许 file:// 页面读本地资源，也不开 JS；
    /// 不开的后果就是整页空白（内容全在 JS 里渲染）。这里按其它网页页一致的设置补齐。
    /// </summary>
    private void ConfigurePlatformWebView()
    {
        GuideView.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (GuideView.Handler?.PlatformView is Android.Webkit.WebView awv)
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
                awv.SetBackgroundColor(Android.Graphics.Color.ParseColor("#FDF6EE"));
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
