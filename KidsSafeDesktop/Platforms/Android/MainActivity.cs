using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Core.View;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;

namespace KidsSafeDesktop;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// 声明为「桌面(Home)」应用：在系统启动器选择 / 按 Home 键时可作为儿童桌面
[IntentFilter(new[] { Android.Content.Intent.ActionMain }, Categories = new[] { Android.Content.Intent.CategoryHome, Android.Content.Intent.CategoryDefault })]
public class MainActivity : MauiAppCompatActivity
{
    private BackGuardCallback? _backGuard;
    private IDispatcherTimer? _immersiveTimer;
    private bool _lockTaskTried;

    /// <summary>
    /// 拦截系统栏 insets：把「状态栏/导航栏/刘海」的 inset 统一置 0 后再往下分发。
    /// 这是「顶部白条」的根治手段——Android 15/16 强制 edge-to-edge 后，窗口确实是全屏的，
    /// 但系统仍会把状态栏高度作为 insets 下发，MAUI / AppCompat 的容器一旦把它转成
    /// padding，内容就整体下移，顶部露出页面白底（≈39dp 的一条白色空白带）。
    /// 只要在 DecorView 这一层把 insets 清零，下游任何容器都不会再加内边距。
    /// 输入法（IME）insets 保持原样，不影响家长设置里的输入框。
    /// </summary>
    private static readonly ZeroSystemBarInsetsListener InsetsListener = new();

    private bool _insetsHooked;
    private bool _insetsLogged;

    // ── 防退出：离开后自动回前台 ───────────────────────────────────────────
    // 底部上滑（Home 手势）、上滑悬停进多任务，都属于**系统级手势**，普通应用没有
    // 任何 API 能直接“拦下”。业界通行的做法是「离开就立刻把自己拉回前台」：
    //   OnStop 时用 ActivityManager.AppTasks[0].MoveToFront() 把本任务移到最前，
    //   失败则退回 StartActivity(NEW_TASK|REORDER_TO_FRONT)。
    // Android 10+ 对“后台启动 Activity”有限制，持有「显示在其他应用上层」
    // (SYSTEM_ALERT_WINDOW) 权限可稳定豁免，因此家长面板提供了该权限的授权入口。
    private static bool _allowExit;                       // 家长点了「退出」
    private static DateTime _suppressUntil = DateTime.MinValue;   // 临时放行（授权/外部应用）
    private static long _lastReturnTicks;
    private bool _wasResumed;                             // 是否曾经真正显示在前台

    /// <summary>家长在家长入口点了「退出」：此后不再自动回前台。</summary>
    public static void AllowExit() => _allowExit = true;

    /// <summary>
    /// 当前是否处于「临时放行」窗口（去系统设置授权 / 打开家长放行的外部应用）。
    /// 无障碍守护也要看这个标记：否则家长一打开设置就被自己弹回来，永远授不了权。
    /// </summary>
    public static bool AutoReturnSuppressed => DateTime.UtcNow < _suppressUntil;

    /// <summary>临时放行一段时间（例如去系统设置授权、或启动家长放行的外部应用）。</summary>
    public static void SuppressAutoReturnFor(TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;
        if (until > _suppressUntil) _suppressUntil = until;
    }

    /// <summary>解除临时放行（回到前台后调用，重新武装防退出）。</summary>
    public static void ClearSuppressAutoReturn() => _suppressUntil = DateTime.MinValue;

    /// <summary>重置锁定标记，让下一次 OnResume 重新尝试进入屏幕固定。</summary>
    public static void ResetLockTaskAttempt()
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity a)
            a._lockTaskTried = false;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 每次真正新建 Activity 时重新武装防护（家长上次「退出」的放行标记不能跨进程复用）
        _allowExit = false;
        ClearSuppressAutoReturn();

        ApplyImmersive();

        // 把「立刻进入屏幕固定」挂到共享库的钩子上：
        // 家长中心（在 KidsSafeShared 里）打开锁定模式时要调用它，
        // 但共享库不能反过来引用本 App 的 MainActivity，所以用钩子解耦。
        Platforms.Android.AndroidHooks.ApplyLockTaskNow = ApplyLockTaskNow;
        Platforms.Android.AndroidHooks.ApplyImmersiveNow = ApplyImmersiveNow;
        Platforms.Android.AndroidHooks.AllowExit = AllowExit;
        Platforms.Android.AndroidHooks.SuppressAutoReturnFor = SuppressAutoReturnFor;
        Platforms.Android.AndroidHooks.MainActivityType = typeof(MainActivity);

        // ── 常驻守护（阳光守护式运行方案）────────────────────────────────
        // 服务、无障碍、设备管理员都实现在本工程（Platforms/Android/Guard），
        // 共享库不能引用 App 工程，所以和上面几项一样走钩子。
        Platforms.Android.AndroidHooks.StartGuardService = () => Droid.Guard.KioskGuard.Start(this);
        Platforms.Android.AndroidHooks.StopGuardService = () => Droid.Guard.KioskGuard.Stop(this);
        Platforms.Android.AndroidHooks.IsAccessibilityGuardOn =
            () => Droid.Guard.KioskAccessibilityService.IsEnabled(this);
        Platforms.Android.AndroidHooks.OpenAccessibilitySettings =
            () => Droid.Guard.KioskAccessibilityService.OpenSettings(this);
        Platforms.Android.AndroidHooks.IsDeviceAdminOn =
            () => Droid.Guard.KioskDeviceAdmin.IsActive(this);
        Platforms.Android.AndroidHooks.RequestDeviceAdmin =
            () => Droid.Guard.KioskDeviceAdmin.Request(this);
        Platforms.Android.AndroidHooks.DisableDeviceAdmin =
            () => Droid.Guard.KioskDeviceAdmin.Disable(this);

        // 冷启动就把前台服务拉起来：一次启动失败（比如通知权限没给）不影响后续，
        // KioskGuard.Start 内部已经 try/catch。
        Droid.Guard.KioskGuard.Start(this);

        // Android 13+ 前台服务的通知要运行时授权；不授权服务照跑但通知不显示，
        // 家长会以为「守护没生效」，所以首启问一次。
        TryRequestNotificationPermission();

        HonorPush.Start();

        // 注册「返回守卫」：全屏手势下从左右边缘内滑就是返回键，极易误触退出。
        // 这里把返回统一接管——页面栈里还有上一页就回退，已在首页则什么都不做（绝不退出）。
        _backGuard = new BackGuardCallback();
        OnBackPressedDispatcher.AddCallback(this, _backGuard);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _wasResumed = true;
        ApplyImmersive();
        StartImmersiveTimer();
        ClearSuppressAutoReturn();              // 回到前台 → 解除临时放行，重新武装
        TryStartLockTask();
    }

    protected override void OnPause()
    {
        StopImmersiveTimer();
        base.OnPause();
    }

    /// <summary>
    /// 语音识别（系统识别界面）返回时把结果交回给 <see cref="Droid.VoiceInput"/>。
    /// 只有语音识别用 StartActivityForResult，其它一律照常转发。
    /// </summary>
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        Droid.VoiceInput.HandleResult(requestCode, resultCode, data);
    }

    protected override void OnDestroy()
    {
        // Activity 被回收时，等待识别的页面会永远等下去，这里兜底解除
        Droid.VoiceInput.Cancel();
        base.OnDestroy();
    }

    /// <summary>
    /// 应用被切到后台（Home 手势 / 多任务 / 上滑回桌面）时触发。
    /// 这是「防退出」的主入口：延迟 500ms 把自己拉回前台，形成“滑出去又自己回来”的效果。
    /// </summary>
    protected override void OnStop()
    {
        base.OnStop();
        ScheduleAutoReturn();
    }

    /// <summary>
    /// 系统栏在重新获得焦点后可能被系统重新显示（例如弹出对话框、输入法、切前后台），
    /// 因此每次拿到焦点都重新隐藏一次。
    /// </summary>
    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) ApplyImmersive();
    }

    /// <summary>
    /// 老版本（未启用 AndroidX 返回调度器）的返回键入口，同样不退出。
    /// 注意：绝不调用 base.OnBackPressed()，否则 MAUI 在根页面会 Finish() 掉 Activity。
    /// </summary>
    public override void OnBackPressed()
    {
        if (BackGuardCallback.TryGoBack()) return;
        // 已在首页：吃掉返回，什么都不做
    }

    /// <summary>供 AndroidKioskService 等外部调用，立刻重新应用沉浸式全屏。</summary>
    public static void ApplyImmersiveNow()
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not MainActivity a) return;

        a.ApplyImmersive();

        // 切换根页面（启动页 → 首页）之后，新页面的原生视图是稍后才挂上来的，
        // 挂上来时可能又按旧的 insets 算了一次留白，所以再补两次。
        foreach (var delay in new[] { 350, 1000, 2200 })
        {
            _ = System.Threading.Tasks.Task.Delay(delay).ContinueWith(_ =>
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity cur)
                        cur.ApplyImmersive();
                });
            }, System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously);
        }
    }

    /// <summary>家长在设置里打开「锁定模式」后立即尝试进入屏幕固定（无需重启应用）。</summary>
    public static void ApplyLockTaskNow()
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not MainActivity a) return;
        if (!Microsoft.Maui.Storage.Preferences.Get("kiosk_lock_task", false)) return;

        a._lockTaskTried = false;
        a.TryStartLockTask();
    }

    /// <summary>
    /// Android 13+ 的 POST_NOTIFICATIONS：前台服务的常驻通知需要它。
    /// 不申请也能跑，但通知栏里看不到「守护中」，家长会以为功能没生效。
    /// 只问一次；拒绝后不再骚扰（系统也只允许问有限次数）。
    /// </summary>
    private void TryRequestNotificationPermission()
    {
        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            {
                return;
            }
            if (CheckSelfPermission("android.permission.POST_NOTIFICATIONS") == Permission.Granted)
            {
                return;
            }
            RequestPermissions(new[] { "android.permission.POST_NOTIFICATIONS" }, 0x201);
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }

    // ── 防退出实现 ────────────────────────────────────────────────────────

    private void ScheduleAutoReturn()
    {
        try
        {
            if (_allowExit) return;                       // 家长已允许退出
            if (IsFinishing || IsDestroyed) return;       // 真正在退出
            if (DateTime.UtcNow < _suppressUntil) return; // 临时放行（授权页/外部应用）
            if (!_wasResumed) return;                     // 从未在前台（避免启动期误触发）

            // 家长关掉了「防退出」开关
            if (!Microsoft.Maui.Storage.Preferences.Get("anti_exit_guard", true)) return;

            // 屏幕已熄灭（按了电源键）时不要抢焦点
            if (!IsDeviceInteractive()) return;

            // 已经在屏幕固定状态，系统本身就不会放行
            if (IsInLockTask()) return;

            var now = System.Environment.TickCount64;
            if (now - System.Threading.Interlocked.Read(ref _lastReturnTicks) < 1200) return;
            System.Threading.Interlocked.Exchange(ref _lastReturnTicks, now);

            // 延迟 500ms：给系统完成窗口切换，太早拉回会被系统忽略
            _ = System.Threading.Tasks.Task.Delay(500)
                .ContinueWith(_ => BringSelfToFront(),
                    System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously);
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }

    /// <summary>把自己（本任务）拉回前台。</summary>
    private void BringSelfToFront()
    {
        // 首选：把自己的任务移到最前（不会新建实例，最平滑）
        try
        {
            var am = GetSystemService(Android.Content.Context.ActivityService) as ActivityManager;
            var tasks = am?.AppTasks;
            if (tasks is not null && tasks.Count > 0)
                tasks[0].MoveToFront();
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }

        // 兜底：部分厂商 ROM 会忽略 MoveToFront，这里再用 NEW_TASK 拉一次
        // （SingleTask + ReorderToFront 会复用已有实例，不会重复创建页面）
        try
        {
            var ctx = ApplicationContext ?? this;
            var intent = new Intent(ctx, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }

    private bool IsInLockTask()
    {
        try
        {
            var am = GetSystemService(Android.Content.Context.ActivityService) as ActivityManager;
            return am is not null && am.LockTaskModeState != Android.App.LockTaskMode.None;
        }
        catch
        {
            return false;
        }
    }

    private bool IsDeviceInteractive()
    {
        try
        {
            var pm = GetSystemService(Android.Content.Context.PowerService) as PowerManager;
            return pm is null || pm.IsInteractive;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 沉浸式真全屏（适配 Android 15/16 的强制 edge-to-edge）：
    /// - SetDecorFitsSystemWindows(false)：内容铺满整屏（含系统栏区域）；
    /// - FLAG_LAYOUT_IN_SCREEN：窗口按「整块屏幕」布局，不再从状态栏下方开始；
    /// - 允许绘制到刘海/挖孔区域（否则顶部会留下一条与刘海等高的空白）；
    /// - Hide(SystemBars)：同时隐藏状态栏与底部导航栏；
    /// - 拦截并清零 system bars insets：targetSdk 35+ 系统会把状态栏高度当「安全区」下发，
    ///   容器一旦据此留白，顶部就会出现一条白色空白带（用户反馈的「顶部大白条」）。
    /// </summary>
    private void ApplyImmersive()
    {
        try
        {
            var window = Window;
            if (window is null) return;

            // ① 铺满整屏。API 30+ 用原生方法（AppCompat 的兼容层在 Android 15+ 已是空实现）
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                window.SetDecorFitsSystemWindows(false);
            WindowCompat.SetDecorFitsSystemWindows(window, false);

            // ② 窗口按整块屏幕布局（含状态栏区域），否则内容仍会从状态栏下方开始
            window.AddFlags(WindowManagerFlags.LayoutInScreen);

            // ③ 画进刘海 / 挖孔区域
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                var lp = window.Attributes;
                if (lp is not null)
                {
                    lp.LayoutInDisplayCutoutMode = Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
                    window.Attributes = lp;
                }
            }

            // ④ 系统栏透明（避免系统给状态栏铺一层不透明底色）
            window.SetStatusBarColor(Android.Graphics.Color.Transparent);
            window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

            // ⑤ 隐藏状态栏与底部导航栏；边缘滑动可临时唤出，随后自动收回
            var controller = WindowCompat.GetInsetsController(window, window.DecorView);
            controller.Hide(WindowInsetsCompat.Type.SystemBars());
            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;

            // ⑥ 清零系统栏内边距 + 拦截 insets（白条根因）
            var decor = window.DecorView;
            if (decor is not null)
            {
                if (!_insetsHooked)
                {
                    _insetsHooked = true;
                    ViewCompat.SetOnApplyWindowInsetsListener(decor, InsetsListener);
                }

                decor.SetPadding(0, 0, 0, 0);
                decor.FindViewById(Android.Resource.Id.Content)?.SetPadding(0, 0, 0, 0);

                // 有些 ROM / 组件会把状态栏高度塞在更内层的容器上，这里沿视图树清一遍
                ClearInsetsPadding(decor, 0);

                if (!_insetsLogged)
                {
                    _insetsLogged = true;
                    try
                    {
                        var insets = ViewCompat.GetRootWindowInsets(decor);
                        var top = insets?.GetInsets(WindowInsetsCompat.Type.SystemBars()).Top ?? -1;
                        Helpers.NavFx.Log(
                            $"[全屏] sdk={Build.VERSION.SdkInt} 状态栏insetTop={top} " +
                            $"decorTop={decor.Top} decorPad={decor.PaddingTop} " +
                            $"cutout={Build.VERSION.SdkInt >= BuildVersionCodes.P}");
                    }
                    catch { }
                }
            }
        }
        catch
        {
            // 沉浸式失败不影响使用，静默忽略
        }
    }

    /// <summary>沿视图树把「被系统栏 insets 撑出来的内边距」清零（只在非零时改，避免反复重排）。</summary>
    private static void ClearInsetsPadding(Android.Views.View view, int depth)
    {
        if (depth > 6) return;

        try
        {
            if (view.PaddingTop != 0 || view.PaddingBottom != 0 ||
                view.PaddingLeft != 0 || view.PaddingRight != 0)
                view.SetPadding(0, 0, 0, 0);
        }
        catch
        {
            // 个别系统控件不允许改 padding，忽略
        }

        if (view is not ViewGroup group) return;
        for (var i = 0; i < group.ChildCount; i++)
        {
            var child = group.GetChildAt(i);
            if (child is not null) ClearInsetsPadding(child, depth + 1);
        }
    }

    /// <summary>
    /// 常驻看门狗：每 1.2 秒检查一次系统栏是否被系统/用户重新显示出来（例如从顶部下拉、
    /// 弹窗关闭、来电结束后），一旦露出来立刻再隐藏——这就是「拉下来又会自己收回去」。
    /// </summary>
    private void StartImmersiveTimer()
    {
        if (_immersiveTimer is not null) return;

        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        _immersiveTimer = dispatcher.CreateTimer();
        _immersiveTimer.Interval = TimeSpan.FromMilliseconds(1200);
        _immersiveTimer.IsRepeating = true;
        _immersiveTimer.Tick += (_, _) =>
        {
            // 系统栏露出来 → 重新隐藏；或者容器又被系统塞回了状态栏高度的内边距
            // （例如弹窗关闭、输入法收起、系统重新分发 insets）→ 重新清零。
            if (SystemBarsVisible() || DecorPadded()) ApplyImmersive();
        };
        _immersiveTimer.Start();
    }

    /// <summary>DecorView 上是否被重新加上了内边距（顶部白条的直接表现）。</summary>
    private bool DecorPadded()
    {
        try
        {
            var decor = Window?.DecorView;
            return decor is not null && (decor.PaddingTop > 0 || decor.PaddingLeft > 0 || decor.PaddingRight > 0);
        }
        catch
        {
            return false;
        }
    }

    private void StopImmersiveTimer()
    {
        try { _immersiveTimer?.Stop(); } catch { }
        _immersiveTimer = null;
    }

    /// <summary>判断系统栏当前是否可见（可见才需要重新隐藏，避免无谓刷新造成闪烁）。</summary>
    private bool SystemBarsVisible()
    {
        try
        {
            var window = Window;
            if (window is null) return true;

            var insets = ViewCompat.GetRootWindowInsets(window.DecorView);
            if (insets is null) return true;   // 拿不到就按「需要重新应用」处理

            return insets.IsVisible(WindowInsetsCompat.Type.SystemBars());
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 屏幕固定（Screen Pinning / lock task）：普通应用唯一能**从系统层面**阻止
    /// 「底部上滑回桌面」「下拉通知栏」「按 Home 键」的官方手段。
    /// 默认**不开启**：一旦固定，家长在设置里放行的外部应用将无法启动（系统会阻止跳转），
    /// 因此做成显式开关（Preferences 键 <c>kiosk_lock_task</c>），由家长按需打开。
    /// 开启后首次会弹一次系统确认框（"固定此应用"）。
    /// </summary>
    private void TryStartLockTask()
    {
        if (!Microsoft.Maui.Storage.Preferences.Get("kiosk_lock_task", false)) return;
        if (_lockTaskTried) return;
        _lockTaskTried = true;

        try
        {
            if (IsInLockTask()) return;   // 已经在固定模式

            StartLockTask();
        }
        catch (Exception ex)
        {
            // 系统未开启「屏幕固定」等情况下会失败，属正常降级
            Helpers.NavFx.Log(ex);
        }
    }
}

/// <summary>
/// 把「状态栏 / 导航栏 / 刘海」的 insets 清零后再向下分发。
/// 目的：让下游任何容器（AppCompat 的 ContentView、MAUI 的根视图）都拿不到
/// 状态栏高度，从而不会把它转成 padding 形成顶部白条。输入法 insets 原样保留。
/// </summary>
internal sealed class ZeroSystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
{
    public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View v, WindowInsetsCompat insets)
    {
        if (insets is null) return null!;

        try
        {
            if (v.PaddingTop != 0 || v.PaddingBottom != 0 ||
                v.PaddingLeft != 0 || v.PaddingRight != 0)
                v.SetPadding(0, 0, 0, 0);

            var zero = AndroidX.Core.Graphics.Insets.Of(0, 0, 0, 0);
            var builder = new WindowInsetsCompat.Builder(insets);
            builder.SetInsets(WindowInsetsCompat.Type.SystemBars(), zero);
            builder.SetInsets(WindowInsetsCompat.Type.DisplayCutout(), zero);
            return builder.Build()!;
        }
        catch
        {
            return insets;
        }
    }
}

/// <summary>
/// 返回守卫：接管 Android 的返回手势/返回键。
/// - 页面栈中还有上一页 → 正常回退一层；
/// - 已经回到首页 → 吞掉返回事件，应用不退出（儿童模式）。
/// </summary>
internal sealed class BackGuardCallback : OnBackPressedCallback
{
    public BackGuardCallback() : base(true) { }

    public override void HandleOnBackPressed()
    {
        TryGoBack();
    }

    /// <summary>尝试回退一层；已在根页面则返回 false（调用方据此决定不退出）。</summary>
    internal static bool TryGoBack()
    {
        try
        {
            var root = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
            if (root is NavigationPage nav)
            {
                // 先关掉模态页（家长设置等），再回退普通页面
                if (nav.Navigation.ModalStack.Count > 0)
                {
                    _ = nav.Navigation.PopModalAsync();
                    return true;
                }

                if (nav.Navigation.NavigationStack.Count > 1)
                {
                    _ = nav.PopAsync();
                    return true;
                }
            }
        }
        catch
        {
            // 导航栈状态异常时也一律不退出
        }

        return false;
    }
}
