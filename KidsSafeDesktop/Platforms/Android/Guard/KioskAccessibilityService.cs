using System;
using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Provider;
using Android.Views.Accessibility;
using KidsSafeDesktop.Helpers;

namespace KidsSafeDesktop.Droid.Guard;

/// <summary>
/// 无障碍守护：盯「当前前台是哪个包」，孩子一打开设置 / 应用商店 / 安装卸载界面
/// 就把它退掉并把儿童桌面拉回来。
///
/// 这是普通应用（无 root、无设备所有者）能挡住「去设置里关掉家长控制」
/// 的唯一正经办法，也是阳光守护这类产品的核心机制之一。
///
/// 权限边界（家长必须知道）：
///   · 无障碍服务必须由家长在「设置 → 无障碍」里手动打开，应用不能自己开；
///   · 各厂商 ROM 对无障碍服务的保活策略不同，部分机型重启后需要重开；
///   · 无障碍能读到的只是窗口事件（哪个应用到前台），不读输入内容，也不截屏。
/// </summary>
[Service(Name = "com.kidssafe.guard.KioskAccessibilityService",
         Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
         Exported = true)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/kiosk_accessibility_config")]
public class KioskAccessibilityService : AccessibilityService
{
    /// <summary>孩子不该自己打开的包。命中就退回儿童桌面。</summary>
    private static readonly string[] BlockedPackages =
    {
        // 系统设置：改无障碍、改默认桌面、清数据、卸载都在这里
        "com.android.settings",
        // 安装 / 卸载确认框（不同 ROM 的包名不一样，全列上）
        "com.android.packageinstaller",
        "com.google.android.packageinstaller",
        "com.miui.packageinstaller",
        "com.samsung.android.packageinstaller",
        // 注意：这里**不拦**权限弹窗（com.android.*.permissioncontroller）。
        // 我们自己在启动时要申请通知权限、家长要授予「显示在其他应用上层」，
        // 拦了会把自家的授权流程也一起按掉，表现为「点了允许但没生效」。
        // 应用商店
        "com.android.vending",
        "com.huawei.appmarket",
        "com.hihonor.appmarket",
        "com.xiaomi.market",
        "com.heytap.market",
        "com.oppo.market",
        "com.bbk.appstore",
        "com.tencent.android.qqdownloader",
        "com.qihoo.appstore",
        "com.baidu.appsearch",
    };

    private long _lastBlockTicks;

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();

        // 运行时再声明一遍事件范围：xml 配置缺失（比如换壳打包漏了资源）时
        // 也能正常工作，不至于「服务连上了但一个事件都收不到」。
        try
        {
            var info = new AccessibilityServiceInfo
            {
                EventTypes = EventTypes.WindowStateChanged | EventTypes.WindowsChanged,
                FeedbackType = FeedbackFlags.Generic,
                NotificationTimeout = 80,
                Flags = AccessibilityServiceFlags.ReportViewIds
                        | AccessibilityServiceFlags.IncludeNotImportantViews,
            };
            SetServiceInfo(info);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }

        // 无障碍服务被系统拉起，说明家长已经授权：顺手把前台守护也拉起来。
        KioskGuard.Start(this);
        NavFx.Log("无障碍守护已连接");
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        try
        {
            if (e is null)
            {
                return;
            }

            var type = e.EventType;
            if (type != EventTypes.WindowStateChanged && type != EventTypes.WindowsChanged)
            {
                return;
            }

            // 家长正在系统页面里授权（无障碍 / 电池白名单 / 默认桌面 / 设备管理员）时
            // 有一个短暂的放行窗口，否则刚打开设置就被自己弹回来，永远授不了权。
            if (MainActivity.AutoReturnSuppressed)
            {
                return;
            }

            var package = e.PackageName;
            if (string.IsNullOrEmpty(package) || package == PackageName)
            {
                return;
            }
            if (Array.IndexOf(BlockedPackages, package) < 0)
            {
                return;
            }

            // 有的 ROM 会连续发好几个事件，节流一下，避免「连按返回」把自己也退出去。
            var now = Environment.TickCount64;
            if (now - _lastBlockTicks < 800)
            {
                return;
            }
            _lastBlockTicks = now;

            NavFx.Log($"拦截受限页面：{package}");
            PerformGlobalAction(GlobalAction.Back);
            KioskGuard.BringAppToFront(this);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }

    public override void OnInterrupt()
    {
        // 系统要求实现，什么都不用做。
    }

    /// <summary>
    /// 家长是否已在系统设置里启用本无障碍服务。
    /// 读的是 Settings.Secure.enabled_accessibility_services（系统唯一的真相源）。
    /// </summary>
    internal static bool IsEnabled(Context context)
    {
        try
        {
            var enabled = Settings.Secure.GetString(context.ContentResolver,
                Settings.Secure.EnabledAccessibilityServices) ?? string.Empty;

            var exact = context.PackageName + "/com.kidssafe.guard.KioskAccessibilityService";
            foreach (var item in enabled.Split(':'))
            {
                if (string.Equals(item, exact, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            // 少数 ROM 会把类名缩写成 .Droid.Guard.Xxx 或换大小写，宽松兜一次。
            return enabled.Contains("KioskAccessibilityService", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>跳到系统的无障碍设置页，让家长手动开启（应用无法代开）。</summary>
    internal static void OpenSettings(Context context)
    {
        // 给家长留 5 分钟操作时间：无障碍列表在系统设置里，不放行会被本服务自己弹回来。
        Platforms.Android.AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromMinutes(5));
        try
        {
            var intent = new Intent(Settings.ActionAccessibilitySettings);
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            NavFx.Log(ex);
        }
    }
}
