namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「教育孩子 36 计」网页资源管理器。
///
/// 人民日报推荐的 36 条教育方法，按 6 类场景整理，离线内置。
/// 与 <see cref="DepressionAssets"/>、<see cref="ChildPsychAssets"/>、
/// <see cref="ParentGuideAssets"/> 同一套做法：
/// MauiAsset 打包 → 释放到应用数据目录 → file:// 交给 WebView。
///
/// 不放进关爱中心的成长指南里，是因为这一页的使用场景不同：
/// 家长是遇到具体问题来"查一条"，需要能按分类快速筛，而不是通读长文。
/// </summary>
public static class Edu36Assets
{
    /// <summary>打包资源名（对应 Resources/Raw/edu36/index.html）。</summary>
    private const string AssetName = "edu36/index.html";

    /// <summary>释放到应用数据目录后的文件夹名。</summary>
    private const string Folder = "edu36";

    /// <summary>内容版本号：改动 index.html 后 +1，用于触发重新释放。</summary>
    private const string Version = "1";

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
