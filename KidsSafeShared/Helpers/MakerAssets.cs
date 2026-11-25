namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「小游戏工坊」网页编辑器资源管理器。
///
/// 编辑器本身是一份单文件 H5（Resources/Raw/Maker/index.html），四端共用同一份；
/// 各端只负责：把它释放到可访问目录、用 WebView 打开、把导入的素材喂给页面。
/// 家长做的作品存在页面自己的 localStorage 里，跟着 WebView 的站点数据走，
/// 所以这里必须固定释放到同一个目录，路径一变就等于把所有作品丢了。
/// </summary>
public static class MakerAssets
{
    /// <summary>MauiAsset 逻辑名前缀（对应 Resources/Raw/Maker 目录）。</summary>
    private const string AssetPrefix = "Maker/";

    /// <summary>释放到应用数据目录后的文件夹名。**改名会导致老作品丢失**。</summary>
    private const string Folder = "maker";

    /// <summary>资源清单版本号：改动 index.html 后 +1，用于触发重新释放。</summary>
    private const string Version = "2";

    private static readonly string[] Files = { "index.html" };

    public static string RootPath => Path.Combine(FileSystem.AppDataDirectory, Folder);

    public static string IndexPath => Path.Combine(RootPath, "index.html");

    private static string VersionFilePath => Path.Combine(RootPath, ".assetversion");

    /// <summary>确保编辑器已释放，返回 index.html 的 file:// URL。</summary>
    public static async Task<string> EnsureExtractedAsync()
    {
        var root = RootPath;
        Directory.CreateDirectory(root);

        if (!IsUpToDate())
        {
            foreach (var rel in Files)
            {
                var dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                try
                {
                    await using var src = await FileSystem.OpenAppPackageFileAsync(AssetPrefix + rel);
                    await using var dst = File.Create(dest);
                    await src.CopyToAsync(dst);
                }
                catch
                {
                    // 单文件缺失时页面会白屏，但不该因此让整个应用起不来
                }
            }

            try { await File.WriteAllTextAsync(VersionFilePath, Version); }
            catch { /* 写不了标记就下次重来 */ }
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
