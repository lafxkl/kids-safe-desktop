using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 启动一个被允许的应用（平台相关）。
/// </summary>
public interface IAppLauncherService
{
    Task LaunchAsync(AppItem app);
}
