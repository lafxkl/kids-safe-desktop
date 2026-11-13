namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「抑郁症专区」网页资源管理器。
///
/// 公益科普长文（认识与发现 / 预防与治疗 / 数据与求助 三个标签页），
/// 与 <see cref="ChildPsychAssets"/>、<see cref="ParentGuideAssets"/> 同一套做法：
/// MauiAsset 打包 → 释放到应用数据目录 → file:// 交给 WebView。
///
/// 之所以单独一个文件夹而不是塞进儿童心理学页：这一页里有大量热线号码，
/// 家长在着急的时候应该一次点到底，不该先翻两个标签才找到电话。
/// </summary>
public static class DepressionAssets
{
    /// <summary>打包资源名（对应 Resources/Raw/Depression/index.html）。</summary>
    private const string AssetName = "Depression/index.html";

    /// <summary>释放到应用数据目录后的文件夹名。</summary>
    private const string Folder = "depression";

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
