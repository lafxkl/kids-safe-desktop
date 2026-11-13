using Microsoft.Maui.ApplicationModel;

namespace KidsSafeDesktop.Helpers;

/// <summary>读取应用版本信息（来自 csproj 的 ApplicationDisplayVersion）。</summary>
public static class AppVersion
{
    /// <summary>纯版本号，如 "1.0"。</summary>
    public static string Version => AppInfo.Current.VersionString;

    /// <summary>带应用名的展示文本，如 "儿童安全桌面 v1.0"。</summary>
    public static string Display => $"儿童安全桌面 v{AppInfo.Current.VersionString}";
}
