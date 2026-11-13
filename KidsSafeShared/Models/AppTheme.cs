using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一套儿童桌面主题：背景大图 + 顶栏渐变 + 磁贴渐变 + 强调色。
/// 主题可在主页「🎨 换皮肤」中切换并持久化。
/// </summary>
public class AppTheme
{
    public string Id { get; init; } = "sunshine";
    public string Name { get; init; } = "暖阳";
    public string Emoji { get; init; } = "☀️";

    /// <summary>背景大图文件名（位于 Resources/Images）。</summary>
    public string BackgroundFile { get; init; } = "theme_sunshine.png";

    /// <summary>顶栏/标题卡片渐变。</summary>
    public LinearGradientBrush HeaderGradient { get; init; } = new();

    /// <summary>磁贴/按钮渐变。</summary>
    public LinearGradientBrush TileGradient { get; init; } = new();

    /// <summary>强调色（用于小点缀）。</summary>
    public Color Accent { get; init; } = Colors.Orange;

    /// <summary>在背景图上文字应使用的颜色（深色，保证可读）。</summary>
    public Color Ink { get; init; } = Colors.Brown;
}
