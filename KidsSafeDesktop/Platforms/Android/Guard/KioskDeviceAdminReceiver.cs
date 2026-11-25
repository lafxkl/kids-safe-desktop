using System;
using Android.App;
using Android.App.Admin;
using Android.Content;
using KidsSafeDesktop.Helpers;

namespace KidsSafeDesktop.Droid.Guard;

/// <summary>
/// 设备管理员：唯一能挡住「一键卸载」的普通应用手段。
///
/// 激活后，系统卸载界面会先要求「取消激活」，而取消激活要进系统设置——
/// 那一步已经被无障碍守护挡住，形成一个闭环。家长要卸载时，
/// 在家长面板点「解除设备管理员」即可（见 <see cref="KioskDeviceAdmin.Disable"/>）。
///
/// 刻意**不申请**擦除数据 / 重置密码 / 锁屏这些高危策略：
/// 家长控制类应用一旦拿到这些能力，被滥用时的破坏力远大于收益，
/// 上架审核也会因此被重点问询。
/// </summary>
[BroadcastReceiver(Name = "com.kidssafe.guard.KioskDeviceAdminReceiver",
                   Permission = "android.permission.BIND_DEVICE_ADMIN",
                   Exported = true)]
[MetaData("android.app.device_admin", Resource = "@xml/kiosk_device_admin")]
[IntentFilter(new[] { "android.app.action.DEVICE_ADMIN_ENABLED" })]
public class KioskDeviceAdminReceiver : DeviceAdminReceiver
{
}

/// <summary>设备管理员的激活 / 查询 / 解除。</summary>
internal static class KioskDeviceAdmin
{
    /// <summary>Java 侧类名：必须与 [BroadcastReceiver(Name=...)] 一致。</summary>
    private const string ReceiverName = "com.kidssafe.guard.KioskDeviceAdminReceiver";

    private static ComponentName Component(Context context)
        => new ComponentName(context, ReceiverName);

    internal static bool IsActive(Context context)
    {
        try
        {
            var dpm = context.GetSystemService(Context.DevicePolicyService) as DevicePolicyManager;
            return dpm is not null && dpm.IsAdminActive(Component(context));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>弹系统确认框，让家长激活（应用不能代激活）。</summary>
    internal static void Request(Context context)
    {
        try
        {
            if (IsActive(context))
            {
                return;
            }

            // 激活页在系统设置里，同样需要临时放行无障碍拦截。
            Platforms.Android.AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromMinutes(3));

            var intent = new Intent(DevicePolicyManager.ActionAddDeviceAdmin);
            intent.PutExtra(DevicePolicyManager.ExtraDeviceAdmin, Component(context));
            intent.PutExtra(DevicePolicyManager.ExtraAddExplanation,
                "启用后可以防止儿童桌面被误卸载；家长可随时在家长面板里解除。");
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    /// <summary>解除激活（家长要卸载前必须先做这一步）。</summary>
    internal static void Disable(Context context)
    {
        try
        {
            var dpm = context.GetSystemService(Context.DevicePolicyService) as DevicePolicyManager;
            dpm?.RemoveActiveAdmin(Component(context));
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }
}
