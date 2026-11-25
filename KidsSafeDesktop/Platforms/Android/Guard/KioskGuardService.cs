using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using KidsSafeDesktop.Helpers;

namespace KidsSafeDesktop.Droid.Guard;

/// <summary>
/// 前台常驻服务：儿童桌面的「命」。
///
/// 关键点（每一条都对应一个真实的坑）：
///   · <c>ForegroundServiceType = specialUse</c>：Android 14 起前台服务必须声明类型，
///     家长控制类没有更贴切的类型，只能 specialUse；上架时需要在 Play 控制台填用途。
///   · 通知的重要性必须是 Low：默认重要性会「叮」一声，孩子一晚上被吵醒。
///   · <c>START_STICKY</c>：被系统杀掉后由系统重建（不是所有 ROM 都遵守，
///     所以还有心跳闹钟兜底）。
///   · <c>OnTaskRemoved</c>：在最近任务里划掉卡片时触发，这里立刻安排 2 秒后重启，
///     否则「划掉」就等于关掉了守护。
/// </summary>
[Service(Name = "com.kidssafe.guard.KioskGuardService",
         Exported = false,
         ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class KioskGuardService : Service
{
    public override void OnCreate()
    {
        base.OnCreate();
        // 必须在 5 秒内调用 startForeground，否则系统会抛
        // ForegroundServiceDidNotStartInTimeException 并把进程干掉。
        StartForegroundCompat();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForegroundCompat();
        KioskGuard.ScheduleHeartbeat(this, TimeSpan.FromMinutes(1));
        return StartCommandResult.Sticky;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        // 最近任务里划掉本应用 → 立刻安排重启（2 秒后）。
        KioskGuard.ScheduleHeartbeat(this, TimeSpan.FromSeconds(2));
        base.OnTaskRemoved(rootIntent);
    }

    public override void OnDestroy()
    {
        // 被系统回收 / 被家长关闭后，也要留一个闹钟：家长关闭时 KioskGuard.Stop()
        // 会把这个闹钟取消掉，所以这里不会把「家长主动关闭」又拉回来。
        KioskGuard.ScheduleHeartbeat(this, TimeSpan.FromSeconds(2));
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    private void StartForegroundCompat()
    {
        try
        {
            var notification = BuildNotification();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                StartForeground(KioskGuard.NotificationId, notification, ForegroundService.TypeSpecialUse);
            }
            else
            {
                StartForeground(KioskGuard.NotificationId, notification);
            }
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    private Notification BuildNotification()
    {
        // 通道只需要建一次，重复建是幂等的（同名会覆盖旧设置）。
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var nm = GetSystemService(NotificationService) as NotificationManager;
            if (nm is not null)
            {
                var channel = new NotificationChannel(KioskGuard.ChannelId,
                    "儿童桌面守护", NotificationImportance.Low)
                {
                    Description = "保持儿童桌面常驻，防止被系统清理或误关闭",
                };
                channel.SetShowBadge(false);
                nm.CreateNotificationChannel(channel);
            }
        }

        Notification.Builder builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, KioskGuard.ChannelId)
            : new Notification.Builder(this);

        builder.SetContentTitle("儿童桌面守护中");
        builder.SetContentText("防止儿童桌面被系统清理或误关闭");
        // 用应用自己的图标（ApplicationInfo.Icon 就是清单里 @mipmap/appicon 的 id）。
        // 不写 Resource.Mipmap.appicon：App 工程与共享库各有一份 R 类，
        // 直接写 Resource.xxx 会触发 CS0436「类型冲突」警告。
        builder.SetSmallIcon(ApplicationInfo?.Icon ?? Android.Resource.Drawable.IcDialogInfo);
        builder.SetOngoing(true);
        builder.SetCategory(Notification.CategoryService);
        builder.SetShowWhen(false);

        try
        {
            var flags = PendingIntentFlags.UpdateCurrent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                flags |= PendingIntentFlags.Immutable;
            }
            var content = PendingIntent.GetActivity(this, 0,
                new Intent(this, typeof(MainActivity)), flags);
            if (content is not null)
            {
                builder.SetContentIntent(content);
            }
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }

        return builder.Build()!;
    }
}
