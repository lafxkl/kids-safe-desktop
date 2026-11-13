namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「0-15 岁成长指南」网页资源管理器。
///
/// 这份指南是一份 6 万字级的家长长文（分年龄段 + 分专题两套视图，自带标签切换与返回顶部）。
/// 用网页承载而不是改写成 XAML：内容以后还会补，改 HTML 比改 C# 数据快得多，
/// 排版（卡片、时间轴、表格）也能原样保留。
///
/// 与 <see cref="PlaylandAssets"/> 同一套做法：MauiAsset 打包 → 释放到应用数据目录 →
/// file:// 交给 WebView，避免 Android 上 file:// 不能直接读 APK 内资源。
/// </summary>
public static class ParentGuideAssets
{
    /// <summary>打包资源名（对应 Resources/Raw/ParentGuide/index.html）。</summary>
    private const string AssetName = "ParentGuide/index.html";

    /// <summary>释放到应用数据目录后的文件夹名。</summary>
    private const string Folder = "parentguide";

    /// <summary>内容版本号：改动 index.html 后 +1，用于触发重新释放。</summary>
    private const string Version = "3";

    public static string RootPath => Path.Combine(FileSystem.AppDataDirectory, Folder);

    public static string IndexPath => Path.Combine(RootPath, "index.html");

    private static string VersionFilePath => Path.Combine(RootPath, ".assetversion");

    /// <summary>确保资源已释放，返回可直接给 WebView 的 file:// URL。</summary>
    public static async Task<string> EnsureExtractedAsync()
    {
        Directory.CreateDirectory(RootPath);

        if (IsUpToDate()) return new Uri(IndexPath).AbsoluteUri;

        try
        {
            await using var src = await FileSystem.OpenAppPackageFileAsync(AssetName);
            await using var dst = File.Create(IndexPath);
            await src.CopyToAsync(dst);

            await File.WriteAllTextAsync(VersionFilePath, Version);
        }
        catch
        {
            // 释放失败时若旧文件还在，至少还能打开上一版，不至于白屏
        }

        return new Uri(IndexPath).AbsoluteUri;
    }

    private static bool IsUpToDate()
    {
        try
        {
            return File.Exists(IndexPath)
                && File.Exists(VersionFilePath)
                && File.ReadAllText(VersionFilePath).Trim() == Version;
        }
        catch
        {
            return false;
        }
    }
}
