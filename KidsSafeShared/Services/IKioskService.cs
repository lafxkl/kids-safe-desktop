namespace KidsSafeDesktop.Services;

/// <summary>
/// 儿童模式锁定（kiosk）相关平台能力。
/// </summary>
public interface IKioskService
{
    /// <summary>进入儿童锁定模式（全屏、屏蔽系统键等，尽力而为）。</summary>
    Task EnterKioskAsync();

    /// <summary>把儿童桌面带回前台（例如在启动其它应用后返回）。</summary>
    Task BringToFrontAsync();

    /// <summary>退出儿童模式（家长鉴权后调用）。</summary>
    Task ExitAppAsync();
}
