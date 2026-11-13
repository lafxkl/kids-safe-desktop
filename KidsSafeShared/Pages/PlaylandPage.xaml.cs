using KidsSafeDesktop.Helpers;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 网页小游戏容器：用 WebView 加载本地已释放的 HTML。
///
/// 合并入口后它不再单独出现在首页，而是由「小游戏乐园」大厅点击某一款游戏进入，
/// 所以构造时要带上网页的相对路径（如 pages/plane_war.html）和显示名。
/// 不传参数时退回加载 Playland/index.html 大厅，保持旧调用仍可用。
/// </summary>
public partial class PlaylandPage : ContentPage
{
    private readonly string _gameFile;
    private string _homeUrl = string.Empty;
    private bool _loaded;

    public PlaylandPage(string gameFile = "index.html", string title = "🎮 小游戏")
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _gameFile = gameFile;
        TitleLabel.Text = title;

        GameView.Navigating += OnNavigating;
        GameView.Navigated += OnNavigated;
        ConfigurePlatformWebView();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        LoadingMask.IsVisible = true;
        try
        {
            // 释放资源（耗时，放后台），完成后加载对应页面
            _homeUrl = await Task.Run(() => PlaylandAssets.EnsureExtractedAsync());
            GameView.Source = new UrlWebViewSource { Url = ResolveUrl(_gameFile) };
        }
        finally
        {
            LoadingMask.IsVisible = false;
        }
    }

    /// <summary>把 pages/xxx.html 拼到已释放目录下；传入 index.html 时直接用大厅地址。</summary>
    private string ResolveUrl(string gameFile)
    {
        if (string.IsNullOrEmpty(gameFile) || gameFile == "index.html") return _homeUrl;

        var slash = _homeUrl.LastIndexOf('/');
        return slash < 0 ? _homeUrl : _homeUrl[..(slash + 1)] + gameFile;
    }

    // ===== 工具栏 =====
    private async void OnBack(object sender, EventArgs e)
    {
        // 网页内部有跳转时先退回上一屏，否则直接回到游戏大厅
        if (GameView.CanGoBack)
            GameView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private void OnReload(object sender, EventArgs e) => GameView.Reload();

    // ===== WebView 生命周期 =====
    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // 只放行本地文件；外链交给系统浏览器，避免在应用内打开陌生网页
        if (e.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return;

        e.Cancel = true;
        _ = OpenExternalAsync(e.Url);
    }

    private async Task OpenExternalAsync(string url)
    {
        try { await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(url); }
        catch { /* 忽略：无法打开浏览器 */ }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // 网页加载完成后按当前设置刷新护眼遮罩（WebView 可能覆盖在最上层）
        EyeCare.Refresh();
    }

    // ===== 平台相关配置 =====
    private void ConfigurePlatformWebView()
    {
        GameView.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (GameView.Handler?.PlatformView is Android.Webkit.WebView awv)
            {
                var s = awv.Settings;
                s.JavaScriptEnabled = true;
                s.DomStorageEnabled = true;
                s.AllowFileAccess = true;
                s.AllowContentAccess = true;
                s.AllowFileAccessFromFileURLs = true;
                s.MediaPlaybackRequiresUserGesture = false;

                // 视口：必须让页面按 device-width 布局，否则 WebView 会用 980px 的桌面视口，
                // 页面按桌面排版后再被缩放，出现「棋盘只露一半、按钮叠在一起」。
                s.UseWideViewPort = true;
                s.LoadWithOverviewMode = false;
                s.SetSupportZoom(false);
                s.BuiltInZoomControls = false;
                s.DisplayZoomControls = false;

                // 字体缩放固定 100%：部分国产 ROM 把系统字体调大后，WebView 会跟着放大文字，
                // 结果固定像素的布局被撑破。网页自己的字号在 CSS 里定义，不需要系统再放大。
                s.TextZoom = 100;

                // 去掉滚动到边界时的蓝色辉光，避免和页面背景色冲突
                awv.OverScrollMode = Android.Views.OverScrollMode.Never;
            }
#endif
        };
    }

    protected override bool OnBackButtonPressed()
    {
        if (GameView.CanGoBack)
        {
            GameView.GoBack();
            return true;
        }
        _ = Navigation.PopAsync();
        return true;
    }
}
