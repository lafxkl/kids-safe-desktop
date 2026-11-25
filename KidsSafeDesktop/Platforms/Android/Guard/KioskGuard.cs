using System;
using Android.App;
using Android.Content;
using Android.OS;
using KidsSafeDesktop.Helpers;

namespace KidsSafeDesktop.Droid.Guard;

/// <summary>
/// 安卓常驻守护的「总开关」——参照阳光守护这类家长控制 App 的运行方式：
///
///   1) <b>前台服务</b>：常驻通知 + <c>START_STICKY</c>，系统内存吃紧时优先保它；
///   2) <b>心跳闹钟</b>：每分钟一次 <c>SetAndAllowWhileIdle</c>，服务被杀就再拉起来；
///   3) <b>开机广播</b>：开机自启，不用家长手动点一次；
///   4) <b>无障碍服务</b>：盯前台包名，孩子打开「设置 / 应用商店 / 安装器 / 卸载界面」
///      就自动退回儿童桌面（这是普通应用能挡住「去设置里关掉家长控制」的唯一手段）；
///   5) <b>设备管理员</b>：激活后系统卸载界面会要求先取消激活，挡住「一键卸载」。
///
/// 为什么不能像 Windows 那样拦截按键：
///   Android 没有给普通应用「全局键盘钩子」这种能力（那等于键盘记录器）。
///   系统级 Home / 多任务手势只能靠「默认桌面 + 立即回前台 + 屏幕固定」，
///   见 AndroidKioskService 与 MainActivity 的说明。
/// </summary>
internal static class KioskGuard
{
    internal const int NotificationId = 0x4B53;
    internal const string ChannelId = "kidssafe_guard";

    /// <summary>心跳闹钟的请求码。固定值，重复设置只会覆盖同一个 PendingIntent。</summary>
    private const int AlarmRequestCode = 0x4B54;

    /// <summary>家长是否允许常驻守护（家长面板「⑥ 后台守护」）。默认开。</summary>
    internal static bool ServiceEnabled =>
        Microsoft.Maui.Storage.Preferences.Get("guard_service", true);

    /// <summary>拉起前台服务。重复调用无害（onStartCommand 会再走一遍）。</summary>
    internal static void Start(Context? context = null)
    {
        var ctx = context ?? Android.App.Application.Context;
        if (!ServiceEnabled)
        {
            return;
        }

        try
        {
            var intent = new Intent(ctx, typeof(KioskGuardService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                // Android 8+ 必须用 StartForegroundService，否则后台起服务直接抛异常。
                ctx.StartForegroundService(intent);
            }
            else
            {
                ctx.StartService(intent);
            }
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    /// <summary>家长主动关闭守护时调用。</summary>
    internal static void Stop(Context? context = null)
    {
        var ctx = context ?? Android.App.Application.Context;
        try
        {
            ctx.StopService(new Intent(ctx, typeof(KioskGuardService)));
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }

        try
        {
            var am = ctx.GetSystemService(Context.AlarmService) as AlarmManager;
            var pending = PendingIntentFor(ctx, AlarmRequestCode);
            if (am is not null && pending is not null)
            {
                am.Cancel(pending);
            }
        }
        catch
        {
            // 忽略：没有闹钟也无所谓
        }
    }

    /// <summary>安排下一次心跳。正常一分钟一次；被杀后重启用短间隔。</summary>
    internal static void ScheduleHeartbeat(Context ctx, TimeSpan delay)
    {
        try
        {
            var am = ctx.GetSystemService(Context.AlarmService) as AlarmManager;
            if (am is null)
            {
                return;
            }

            var at = Java.Lang.JavaSystem.CurrentTimeMillis() + (long)Math.Max(1000, delay.TotalMilliseconds);
            // SetAndAllowWhileIdle 不需要精确闹钟权限（Android 12+ 的
            // SCHEDULE_EXACT_ALARM 属于敏感权限，家长控制没必要为它多要一次授权）。
            // 代价是打盹模式下可能被推迟到几分钟，对「守护」够用。
            var pending = PendingIntentFor(ctx, AlarmRequestCode);
            if (pending is null)
            {
                return;
            }
            am.SetAndAllowWhileIdle(AlarmType.RtcWakeup, at, pending);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    private static PendingIntent? PendingIntentFor(Context ctx, int requestCode)
    {
        var intent = new Intent(ctx, typeof(KioskAlarmReceiver));
        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            // Android 12+ 要求显式声明可变性，否则直接抛 IllegalArgumentException。
            flags |= PendingIntentFlags.Immutable;
        }
        return PendingIntent.GetBroadcast(ctx, requestCode, intent, flags);
    }

    /// <summary>把儿童桌面拉到前台。Android 10+ 需要「显示在其他应用上层」才稳定。</summary>
    internal static void BringAppToFront(Context ctx)
    {
        try
        {
            var intent = new Intent(ctx, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }
}
