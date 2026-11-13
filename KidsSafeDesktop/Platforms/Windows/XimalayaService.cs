using KidsSafeDesktop.Services;
using Microsoft.Maui.Essentials;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// Windows：用系统默认浏览器/关联应用打开喜马拉雅儿童网页。
/// </summary>
public class XimalayaService : IXimalayaService
{
    public async Task OpenAsync(string categoryKey)
    {
        try
        {
            await Browser.Default.OpenAsync("https://www.ximalaya.com/child/", BrowserLaunchMode.SystemPreferred);
        }
        catch { /* 忽略：环境无法打开浏览器 */ }
    }
}
