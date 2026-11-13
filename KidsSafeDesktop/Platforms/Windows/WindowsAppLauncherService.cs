using System.Diagnostics;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// Windows 启动器：http(s) 用系统关联打开，其余作为可执行文件/协议启动。
/// </summary>
public class WindowsAppLauncherService : IAppLauncherService
{
    public async Task LaunchAsync(AppItem app)
    {
        if (string.IsNullOrWhiteSpace(app.LaunchTarget))
            return;

        var target = app.LaunchTarget!;
        try
        {
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                await global::Windows.System.Launcher.LaunchUriAsync(new Uri(target));
            }
            else
            {
                var psi = new ProcessStartInfo(target) { UseShellExecute = true };
                Process.Start(psi);
            }
        }
        catch
        {
            // 路径不存在或被策略阻止时静默失败，由调用方提示
            throw;
        }
    }
}
