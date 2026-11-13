namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「儿童心理学 · 自闭症关怀」网页资源管理器。
///
/// 这份内容是公益科普长文（心理发展 + 自闭症关怀两个标签页），
/// 与 <see cref="ParentGuideAssets"/> 同一套做法：MauiAsset 打包 → 释放到应用数据目录 →
/// file:// 交给 WebView。之所以用网页承载，是因为这类内容会持续增补，
/// 改 HTML 比改 C# 数据快，卡片、时间轴、表格的排版也能原样保留。
/// </summary>
public static class ChildPsychAssets
{
    /// <summary>打包资源名（对应 Resources/Raw/ChildPsych/index.html）。</summary>
    private const string AssetName = "ChildPsych/index.html";

    /// <summary>释放到应用数据目录后的文件夹名。</summary>
    private const string Folder = "childpsych";

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
