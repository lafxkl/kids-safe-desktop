using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 启动页：播放守护主题动画（标语「守护儿童网络安全」），同时异步完成数据初始化。
/// 动画与加载都就绪后切换到首页，避免加载过快导致动画一闪而过。
/// </summary>
public partial class SplashPage : ContentPage
{
    private readonly SplashDrawable _drawable = new();
    private readonly DateTime _start = DateTime.Now;

    private IDispatcherTimer? _timer;
    private bool _started;
    private bool _finished;

    /// <summary>动画最短播放时长。</summary>
    private static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(2.6);

    public SplashPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        SplashView.Drawable = _drawable;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartAnimation();
        _ = InitializeAsync();
    }

    private void StartAnimation()
    {
        if (_started) return;
        _started = true;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33); // ≈30fps
        _timer.Tick += (_, _) =>
        {
            _drawable.Time = (DateTime.Now - _start).TotalSeconds;
            SplashView.Invalidate();
        };
        _timer.Start();
    }

    /// <summary>原 App.OnStart 中的初始化搬到此处，让进度条反映真实加载过程。</summary>
    private async Task InitializeAsync()
    {
        var steps = new (string Name, Func<Task> Run)[]
        {
            ("读取本地设置", () => ServiceProvider.Get<ISettingsService>().LoadAsync()),
            ("准备护眼滤镜", () => { KidsSafeDesktop.Helpers.EyeCare.Refresh(); return Task.CompletedTask; }),
            ("检查账号状态", () => ServiceProvider.Get<IAccountService>().EnsureAccountAsync()),
            ("启动时长守护", () => { ServiceProvider.Get<IScreenTimeService>().Start(); return Task.CompletedTask; }),
            ("连接家长服务器", () => { ServiceProvider.Get<ILiveSyncService>().Start(); return Task.CompletedTask; }),
            ("进入儿童模式", () => ServiceProvider.Get<IKioskService>().EnterKioskAsync())
        };

        for (var i = 0; i < steps.Length; i++)
        {
            try { await steps[i].Run(); }
            catch { /* 单项失败不阻断启动 */ }

            _drawable.Progress = (i + 1d) / steps.Length;
        }

        await FinishAsync();
    }

    private async Task FinishAsync()
    {
        if (_finished) return;
        _finished = true;

        var elapsed = DateTime.Now - _start;
        if (elapsed < MinDuration)
            await Task.Delay(MinDuration - elapsed);

        _timer?.Stop();

        try { await this.FadeTo(0, 240); } catch { }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // 首次启动（或家长手动重看引导）先进设置向导，否则直接进儿童桌面
            var onboarded = Microsoft.Maui.Storage.Preferences.Get("onboarding_done", false);
            Page root = onboarded ? new HomePage() : new OnboardingPage();

#if DEBUG
            // 调试直达：KS_START_PAGE=parentguide 时启动后直接打开成长指南，
            // 免去「家长中心 → 关爱中心 → 成长指南」的点击路径，便于排查网页渲染问题。
            if (Environment.GetEnvironmentVariable("KS_START_PAGE") is { Length: > 0 } startPage)
            {
                root = startPage.ToLowerInvariant() switch
                {
                    "parentguide" => new ParentGuidePage(),
                    "childpsych" => new ChildPsychPage(),
                    "depression" => new DepressionPage(),
                    "edu36" => new Edu36Page(),
                    "carecenter" => new CareCenterPage(),
                    "games" => new GamesHubPage(),
                    "playland" => new PlaylandPage("pages/plane_war.html", "🚀 雷霆战机"),
                    "sciencelab" => new ScienceLabPage(),
                    "nsfd" => new NsfdPage(),
                    _ => root
                };
            }
#endif

            var page = new NavigationPage(root);
            var app = Application.Current;
            if (app is not null && app.Windows.Count > 0)
                app.Windows[0].Page = page;
        });

        // 根页面就绪后再确认一次全屏 / 任务栏状态
        try { await ServiceProvider.Get<IKioskService>().EnterKioskAsync(); }
        catch { }
    }

    protected override bool OnBackButtonPressed() => true; // 启动期间禁止返回
}
