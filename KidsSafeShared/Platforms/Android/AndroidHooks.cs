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
}
#endif
