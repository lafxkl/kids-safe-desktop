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
            // 增量更新：只读上次的落盘结果、发现等着装的补丁，不做网络请求（不拖慢启动）
            ("检查更新状态", () => { ServiceProvider.Get<KidsSafeDesktop.Updates.IUpdateService>().Initialize(); return Task.CompletedTask; }),
            ("进入儿童模式", () => ServiceProvider.Get<IKioskService>().EnterKioskAsync())
        };

        for (var i = 0; i < steps.Length; i++)
        {
            /*
             * 单项失败不阻断启动 —— 但**必须留痕**。
             * 这里以前是裸 catch，家长反馈「打开就卡在启动页」「进了桌面但没锁住」时
             * 完全看不出是哪一步倒了，只能靠猜。现在每一步的成败与耗时都进 runtime.log。
             */
            var t0 = DateTime.Now;
            try
            {
                await steps[i].Run();
                KidsSafeDesktop.Helpers.AppLog.I("启动",
                    $"{steps[i].Name} 完成（{(DateTime.Now - t0).TotalMilliseconds:0} ms）");
            }
            catch (Exception ex)
            {
                KidsSafeDesktop.Helpers.AppLog.E("启动", $"{steps[i].Name} 失败（已跳过，不阻断启动）", ex);
            }

            _drawable.Progress = (i + 1d) / steps.Length;
        }

        // 更新检查放后台：失败只是「这次不更新」，绝不能让启动页多等一秒。
        _ = CheckUpdateInBackgroundAsync();

        await FinishAsync();
    }

    /// <summary>
    /// 后台检查并下载增量补丁。任何异常都吞掉 —— 更新失败不能影响孩子使用。
    /// 下载完只是「就绪」，真正落盘在应用关闭时（Windows）或下次启动（Android）。
    /// </summary>
    private static async Task CheckUpdateInBackgroundAsync()
    {
        try
        {
            var update = ServiceProvider.Get<KidsSafeDesktop.Updates.IUpdateService>();

            // 隔几秒再查：启动阶段本来就有同步/长轮询请求，别抢那点带宽
            await Task.Delay(TimeSpan.FromSeconds(8));

            if (await update.CheckAsync())
            {
                await update.DownloadAsync();
            }
        }
        catch (Exception ex)
        {
            KidsSafeDesktop.Helpers.AppLog.W("增量更新", "后台检查失败（已忽略）", ex);
        }
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

            KidsSafeDesktop.Helpers.AppLog.I("启动",
                $"进入根页面 {root.GetType().Name}，从点图标到桌面共 {(DateTime.Now - _start).TotalMilliseconds:0} ms");
        });

        // 根页面就绪后再确认一次全屏 / 任务栏状态
        try
        {
            await ServiceProvider.Get<IKioskService>().EnterKioskAsync();
            KidsSafeDesktop.Helpers.AppLog.I("启动", "儿童模式（Kiosk）已确认");
        }
        catch (Exception ex)
        {
            // 锁不住是最要紧的失效之一：孩子能直接退出到桌面
            KidsSafeDesktop.Helpers.AppLog.E("启动", "进入儿童模式失败，设备可能没有真正锁住", ex);
        }
    }

    protected override bool OnBackButtonPressed() => true; // 启动期间禁止返回
}
