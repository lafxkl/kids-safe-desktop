using System.Text.Json.Serialization;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一个被允许在儿童桌面中显示/启动的应用磁贴。
/// </summary>
public class AppItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Android 包名（仅 Android 使用）</summary>
    public string? PackageName { get; set; }

    /// <summary>Windows 启动目标：exe 路径或 http(s) 链接</summary>
    public string? LaunchTarget { get; set; }

    /// <summary>磁贴主题色（十六进制）</summary>
    public string AccentColor { get; set; } = "#4C8BF5";

    /// <summary>磁贴上的 emoji 图标（真实图标加载失败时的兜底）</summary>
    public string? Emoji { get; set; } = "⭐";

    /// <summary>
    /// 运行时加载的真实应用图标（来自系统）。不持久化——每次启动从系统重新获取。
    /// </summary>
    [JsonIgnore]
    public ImageSource? Icon { get; set; }

    /// <summary>是否存在真实图标（用于 UI 决定显示 Image 还是 Emoji）。</summary>
    [JsonIgnore]
    public bool HasIcon => Icon is not null;

    /// <summary>用于去重/比对（同包名或同启动目标视为同一应用）</summary>
    public bool SameAs(AppItem other)
    {
        if (!string.IsNullOrEmpty(PackageName) && PackageName == other.PackageName) return true;
        if (!string.IsNullOrEmpty(LaunchTarget) && LaunchTarget == other.LaunchTarget) return true;
        return Id == other.Id;
    }
}
