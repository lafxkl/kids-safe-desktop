using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// Windows 上可加入白名单的应用目录（家长也可在后台手动添加路径/网址）。
/// 说明：Windows 应用枚举需解析开始菜单快捷方式（COM），这里给出常用安全项，
/// 家长可在后台“允许的应用”之外手动补充 exe 路径或 http(s) 链接。
/// </summary>
public class WindowsAppCatalogService : IAppCatalogService
{
    public Task<List<AppItem>> GetInstalledAppsAsync()
    {
        var list = new List<AppItem>
        {
            new() { Name = "计算器",   LaunchTarget = "calc.exe",     Emoji = "🧮", AccentColor = "#2E7D32" },
            new() { Name = "画图",     LaunchTarget = "mspaint.exe",  Emoji = "🎨", AccentColor = "#AD1457" },
            new() { Name = "记事本",   LaunchTarget = "notepad.exe",  Emoji = "📝", AccentColor = "#1565C0" },
            new() { Name = "照片",     LaunchTarget = "ms-photos:",   Emoji = "🖼️", AccentColor = "#00838F" },
            new() { Name = "儿童学习网", LaunchTarget = "https://kids.nationalgeographic.com", Emoji = "🔤", AccentColor = "#EF6C00" },
            new() { Name = "儿童编程",   LaunchTarget = "https://scratch.mit.edu", Emoji = "💻", AccentColor = "#6A1B9A" },
        };
        return Task.FromResult(list);
    }

    /// <summary>Windows 端暂未提取真实 exe 图标，UI 退化为 emoji。</summary>
    public Task<ImageSource?> GetAppIconAsync(string packageName)
        => Task.FromResult((ImageSource?)null);
}
