#if ANDROID
namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// 共享库与「App 入口」之间的窄钩子。
///
/// 背景：MainActivity 属于各个 App 工程（入口类不可能放进共享库），
/// 但共享库里的平台服务要调用它做三件事：立刻套用沉浸式、临时放行「自动回前台」、
/// 以及退回到本应用时重新拉起自己的主界面。
/// 直接引用 MainActivity 会让共享库依赖具体 App，所以改成：
/// **App 启动时把方法挂到钩子上，共享库只调用钩子**。
///
/// 未注册时（例如家长端不接管系统外壳）调用全是空操作，不会崩。
/// </summary>
public static class AndroidHooks
{
    /// <summary>立刻尝试进入系统屏幕固定（家长中心打开「锁定模式」时调用）。</summary>
    public static Action? ApplyLockTaskNow { get; set; }

    /// <summary>立刻重新应用沉浸式全屏。</summary>
    public static Action? ApplyImmersiveNow { get; set; }

    /// <summary>放行一次退出（不触发「自动回前台」）。</summary>
    public static Action? AllowExit { get; set; }

    /// <summary>在指定时长内暂停「自动回前台」：跳转外部应用 / 权限弹窗 / 语音识别时用。</summary>
    public static Action<TimeSpan>? SuppressAutoReturnFor { get; set; }

    /// <summary>
    /// 本 App 的主 Activity 类型，用于「把儿童桌面带回前台」。
    /// 家长端不注册时退回当前 Activity，语义仍成立（把本应用拉到前台）。
    /// </summary>
    public static Type? MainActivityType { get; set; }

    // ── 常驻守护（前台服务 / 无障碍 / 设备管理员）──────────────────────────
    // 这几项都要碰 Activity 与 Android 特有的服务类，只能由 App 工程实现；
    // 共享库里的家长面板只通过钩子调用，未注册时全是空操作。

    /// <summary>拉起前台常驻服务。</summary>
    public static Action? StartGuardService { get; set; }

    /// <summary>停止前台常驻服务（家长主动关闭守护）。</summary>
    public static Action? StopGuardService { get; set; }

    /// <summary>无障碍守护是否已由家长在系统设置里开启。</summary>
    public static Func<bool>? IsAccessibilityGuardOn { get; set; }

    /// <summary>跳到系统「无障碍」设置页（应用不能代开）。</summary>
    public static Action? OpenAccessibilitySettings { get; set; }

    /// <summary>设备管理员是否已激活（激活后系统卸载界面会先要求取消激活）。</summary>
    public static Func<bool>? IsDeviceAdminOn { get; set; }

    /// <summary>弹系统确认框激活设备管理员。</summary>
    public static Action? RequestDeviceAdmin { get; set; }

    /// <summary>解除设备管理员（家长要卸载前必须先做）。</summary>
    public static Action? DisableDeviceAdmin { get; set; }
}
#endif
