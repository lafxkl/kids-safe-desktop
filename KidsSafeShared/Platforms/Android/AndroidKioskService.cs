#if ANDROID
using Android.App;
using Android.App.Roles;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// Android 儿童模式（防退出）三件套：
/// 1) <b>默认桌面</b>：声明 CATEGORY_HOME 并引导设为默认桌面——上滑/Home 回到的就是本应用；
/// 2) <b>自动回前台</b>（默认开启）：一旦被切到后台（底部上滑、进多任务），立即把自己拉到最前；
/// 3) <b>屏幕固定</b>（可选开关）：从系统层面彻底屏蔽通知栏、Home 手势与多任务键。
/// 其中 2) 在 Android 10+ 若持「显示在其他应用上层」权限会更加稳定（见 <see cref="RequestOverlayPermission"/>）。
/// 注：完全静默、无需用户确认的锁定需要设备所有者(Device Owner)权限。
/// </summary>
public class AndroidKioskService : IKioskService
{
    public Task EnterKioskAsync()
    {
        try
        {
            // 启动流程有多个调用点，这里幂等地再确保一次沉浸式全屏
            AndroidHooks.ApplyImmersiveNow?.Invoke();
        }
        catch
        {
            // 忽略：非致命
        }

        // 首次启动会先进入「新手引导」，默认桌面的引导在引导页第 4 步里做，
        // 这里跳过，避免系统弹窗打断引导流程。
        if (Preferences.Get("onboarding_done", false))
            PromptDefaultHomeOnce();
        return Task.CompletedTask;
    }

    // ── 默认桌面 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 引导把本应用设为「默认桌面」。
    /// 这是普通应用能做到的、对抗「底部上滑回桌面」的标准手段：
    /// 成为默认桌面后，上滑回到的就是本应用自己的桌面。
    /// </summary>
    public static void RequestDefaultHome(bool force = false)
    {
        try
        {
            var ctx = Platform.CurrentActivity;
            if (ctx is null) return;
            if (!force && Preferences.Get("home_role_asked", false)) return;

            // 授权对话框期间不要把用户“抢”回来
            AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromMinutes(2));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                var rm = ctx.GetSystemService(Context.RoleService) as RoleManager;
                if (rm is not null && rm.IsRoleAvailable(RoleManager.RoleHome))
                {
                    if (rm.IsRoleHeld(RoleManager.RoleHome)) return;   // 已经是默认桌面
                    Preferences.Set("home_role_asked", true);
                    ctx.StartActivityForResult(rm.CreateRequestRoleIntent(RoleManager.RoleHome), 0x101);
                    return;
                }
            }

            // 旧系统或厂商魔改（部分荣耀/华为机型 RoleManager 不可用）：
            // 直接跳到「默认桌面」设置页，由家长手动选择。
            Preferences.Set("home_role_asked", true);
            OpenHomeSettings(ctx);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    /// <summary>当前是否已经是系统默认桌面。</summary>
    public static bool IsDefaultHome()
    {
        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Q) return false;
            var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            var rm = ctx.GetSystemService(Context.RoleService) as RoleManager;
            return rm is not null && rm.IsRoleAvailable(RoleManager.RoleHome) && rm.IsRoleHeld(RoleManager.RoleHome);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>打开系统的「默认桌面 / 主屏幕应用」设置页。</summary>
    public static void OpenHomeSettings(Activity? activity = null)
    {
        var ctx = activity ?? Platform.CurrentActivity;
        if (ctx is null) return;

        AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromMinutes(2));

        try
        {
            var intent = new Intent(global::Android.Provider.Settings.ActionHomeSettings);
            intent.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    private static void PromptDefaultHomeOnce() => RequestDefaultHome(false);

    // ── 「显示在其他应用上层」权限（增强自动回前台） ────────────────────────

    /// <summary>是否已持有「显示在其他应用上层」权限（Android 10+ 后台启动 Activity 的豁免项）。</summary>
    public static bool HasOverlayPermission()
    {
        try
        {
            var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            return global::Android.Provider.Settings.CanDrawOverlays(ctx);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>跳转到系统授权页，让家长授予「显示在其他应用上层」。</summary>
    public static void RequestOverlayPermission()
    {
        var ctx = Platform.CurrentActivity;
        if (ctx is null) return;

        AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromMinutes(2));

        try
        {
            if (HasOverlayPermission()) return;

            var intent = new Intent(global::Android.Provider.Settings.ActionManageOverlayPermission,
                global::Android.Net.Uri.Parse("package:" + ctx.PackageName));
            intent.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    // ── 电池优化白名单（避免后台被厂商省电策略清理） ──────────────────────

    /// <summary>是否已忽略电池优化。</summary>
    public static bool IsIgnoringBatteryOptimizations()
    {
        try
        {
            var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            var pm = ctx.GetSystemService(Context.PowerService) as PowerManager;
            return pm?.IsIgnoringBatteryOptimizations(ctx.PackageName) == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 申请忽略电池优化：荣耀/华为等机型的省电策略会在切后台后很快杀掉进程，
    /// 加入白名单后「自动回前台」才不会因为进程被杀而失效。
    /// </summary>
    public static void RequestIgnoreBatteryOptimizations()
    {
        var ctx = Platform.CurrentActivity;
        if (ctx is null) return;

        AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromMinutes(2));

        try
        {
            if (IsIgnoringBatteryOptimizations()) return;

            var intent = new Intent(global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations,
                global::Android.Net.Uri.Parse("package:" + ctx.PackageName));
            intent.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
            try
            {
                // 部分机型没有直接的申请弹窗，退化为打开电池优化设置列表
                var fallback = new Intent(global::Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                fallback.AddFlags(ActivityFlags.NewTask);
                ctx.StartActivity(fallback);
            }
            catch
            {
                // 忽略
            }
        }
    }

    // ── 其它 ──────────────────────────────────────────────────────────────

    public Task BringToFrontAsync()
    {
        try
        {
            var am = global::Android.App.Application.Context.GetSystemService(Context.ActivityService)
                     as global::Android.App.ActivityManager;
            var tasks = am?.AppTasks;
            if (tasks is not null && tasks.Count > 0)
            {
                tasks[0].MoveToFront();
                return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }

        var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
        var intent = new Intent(ctx, AndroidHooks.MainActivityType ?? ctx.GetType());
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        ctx.StartActivity(intent);
        return Task.CompletedTask;
    }

    public Task ExitAppAsync()
    {
        // 关键：先放行，否则 OnStop 里的「自动回前台」会立刻把应用拉回来，看起来退不出去
        AndroidHooks.AllowExit?.Invoke();

        if (Platform.CurrentActivity is Activity activity)
        {
            try
            {
                // 若处于屏幕固定（lock task）状态，必须先解除，否则无法真正退出
                activity.StopLockTask();
            }
            catch
            {
                // 未固定时会抛异常，忽略
            }

            // 若本应用已是「默认桌面」，Finish 后系统会立刻把桌面（也就是本应用）拉起来，
            // 看起来像「退不出去」——这里给家长一个明确提示。
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    var rm = activity.GetSystemService(Context.RoleService) as RoleManager;
                    if (rm is not null && rm.IsRoleHeld(RoleManager.RoleHome))
                    {
                        global::Android.Widget.Toast.MakeText(activity,
                            "本应用已是默认桌面：如需完全退出，请到 系统设置 → 应用 → 默认应用 更换桌面",
                            global::Android.Widget.ToastLength.Long)?.Show();
                    }
                }
            }
            catch
            {
                // 提示失败忽略
            }

            activity.MoveTaskToBack(true);
            activity.FinishAffinity();
        }
        return Task.CompletedTask;
    }
}
#endif
