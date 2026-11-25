using System;
using Android.App;
using Android.Content;

namespace KidsSafeDesktop.Droid.Guard;

/// <summary>
/// 开机自启：系统启动完成后拉起前台守护，并把儿童桌面摆到屏幕前。
///
/// 权限与限制：
///   · 需要 RECEIVE_BOOT_COMPLETED（已在清单里）；
///   · Android 8+ 允许从 BOOT_COMPLETED 起前台服务；
///   · 华为 / 小米 / OPPO 等还有一层「自启动」开关，属于厂商私有权限，
///     代码申请不到，只能在引导页提示家长去「手机管家 → 自启动管理」里放行。
/// </summary>
[BroadcastReceiver(Name = "com.kidssafe.guard.KioskBootReceiver", Exported = true, Enabled = true)]
[IntentFilter(new[]
{
    Intent.ActionBootCompleted,
    "android.intent.action.LOCKED_BOOT_COMPLETED",
    Intent.ActionMyPackageReplaced,
    "android.intent.action.QUICKBOOT_POWERON",
})]
public class KioskBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            KioskGuard.Start(context);
            KioskGuard.ScheduleHeartbeat(context, TimeSpan.FromSeconds(15));
            KioskGuard.BringAppToFront(context);
            Helpers.NavFx.Log($"开机/更新广播：{intent?.Action}，已拉起守护");
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }
}

/// <summary>
/// 心跳闹钟的接收者。只负责「续命」，**不抢前台**——
/// 每分钟把儿童桌面拽到最前会打断家长放行的外部应用（动画片、网课）。
/// </summary>
[BroadcastReceiver(Name = "com.kidssafe.guard.KioskAlarmReceiver", Exported = false)]
public class KioskAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null)
        {
            KioskGuard.Start(context);
        }
    }
}
