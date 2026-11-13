using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 枚举设备上可加入白名单的已安装应用（家长在后台选择）。
/// </summary>
public interface IAppCatalogService
{
    Task<List<AppItem>> GetInstalledAppsAsync();

    /// <summary>
    /// 按包名获取应用的真实启动图标（Android 有效；其他平台返回 null，UI 退化为 emoji）。
    /// </summary>
    Task<ImageSource?> GetAppIconAsync(string packageName);
}
