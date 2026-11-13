using System.IO.Compression;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「星际科学实验室」资源管理器。
/// 原始网页（单文件 index.html，自包含的星空动画 + 12 个实验卡片）以 MauiAsset 打包在
/// Resources/Raw/ScienceLab.zip 中，运行时解压到应用数据目录，再用 file:// URL 交给 WebView 加载。
/// 这样完全离线运行，无需任何网络；页面内所有 CSS/JS 均为内联，无外部依赖。
/// </summary>
public static class ScienceLabAssets
{
    /// <summary>打包资源名（对应 Resources/Raw/ScienceLab.zip）。</summary>
    private const string AssetName = "ScienceLab.zip";

    /// <summary>释放到应用数据目录后的文件夹名。</summary>
    private const string Folder = "sciencelab";

    /// <summary>资源清单版本号：改动打包内容后请 +1，用于触发重新释放。</summary>
    private const string Version = "2";

    /// <summary>zip 内部顶层目录名（生成时固定为 ScienceLab/）。</summary>
    private const string ZipTop = "ScienceLab/";

    /// <summary>释放后的根目录。</summary>
    public static string RootPath => Path.Combine(FileSystem.AppDataDirectory, Folder);

    /// <summary>入口页面路径。</summary>
    public static string IndexPath => Path.Combine(RootPath, "index.html");

    private static string VersionFilePath => Path.Combine(RootPath, ".assetversion");

    /// <summary>
    /// 确保资源已解压到可访问目录，返回入口页面的 file:// URL。
    /// 已有相同版本时直接复用，避免每次启动重复 IO。
    /// </summary>
    public static async Task<string> EnsureExtractedAsync()
    {
        var root = RootPath;
        Directory.CreateDirectory(root);

        if (!IsUpToDate())
        {
            // 旧版本或缺失，先清掉再解压，保证完整
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { /* 忽略：下一次会重试 */ }
            Directory.CreateDirectory(root);

            await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetName);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                // 目录项或目录穿越项跳过
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                    continue;
                var rel = entry.FullName;
                if (rel.StartsWith(ZipTop, StringComparison.OrdinalIgnoreCase))
                    rel = rel.Substring(ZipTop.Length);
                if (string.IsNullOrEmpty(rel) || rel.EndsWith("/"))
                    continue;

                var dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                try { entry.ExtractToFile(dest, true); }
                catch { /* 单个文件失败不影响其余 */ }
            }

            try { await File.WriteAllTextAsync(VersionFilePath, Version); }
            catch { /* 忽略：无法写标记时下次会重新释放 */ }
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
