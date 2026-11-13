using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 儿童桌面启动器里的统一图标项：图标（真实图标或 emoji 兜底）+ 名称。
/// 既承载内置功能页，也承载家长允许的外部应用。
/// </summary>
public class LauncherItem
{
    /// <summary>显示名称（显示在图标下方）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>无真实图标时显示的 emoji 兜底。</summary>
    public string Emoji { get; set; } = "⭐";

    /// <summary>
    /// 图标底色（磁贴主题色）。扁平化设计：直接用纯色平铺，不做渐变、不加投影，
    /// 只靠「圆角 squircle + 纯色色块 + emoji 字形」构成现代扁平图标。
    /// </summary>
    public Color Accent { get; set; } = Colors.Gray;

    /// <summary>运行时加载的真实图标（外部应用）；内置项通常为 null。</summary>
    public ImageSource? Icon { get; set; }

    /// <summary>是否存在真实图标。</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>若为家长允许的外部应用，则非空，走系统启动。</summary>
    public AppItem? App { get; set; }

    /// <summary>内置功能项的点击行为（导航到对应页面）。</summary>
    public Action? Launch { get; set; }

    /// <summary>由 AppItem 派生（用于家长允许的外部应用磁贴）。</summary>
    public static LauncherItem FromApp(AppItem a) => new()
    {
        Name = a.Name,
        Emoji = a.Emoji ?? "⭐",
        Accent = Color.FromArgb(a.AccentColor),
        Icon = a.Icon,
        App = a
    };
}
