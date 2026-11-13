using System.Security.Cryptography;

namespace KidsSafeServer;

/// <summary>一个可下载的安装包。</summary>
public sealed record DownloadItem(
    string Name,
    long Size,
    string SizeText,
    string Platform,
    DateTime UpdatedAt,
    string Url,
    string Sha256);

/// <summary>
/// 安装包目录：扫描 wwwroot/downloads 下的 apk / zip / msi / exe，
/// 供下载页（download.html）和 /api/download/list 使用。
///
/// 为什么不把下载地址写死在配置里：发版时只需要把新包丢进目录，
/// 页面和接口会自动出现新文件，不用改代码、也不用重启服务。
/// 若安装包放在别处（对象存储 / 应用市场），再用 Site:DownloadUrl 覆盖主按钮即可。
/// </summary>
public static class DownloadCatalog
{
    private static readonly string[] AllowedExtensions = [".apk", ".aab", ".zip", ".msi", ".exe"];

    /// <summary>SHA-256 缓存：文件没变就不重复算（百兆包算一次要几百毫秒）。</summary>
    private static readonly Dictionary<string, (DateTime Stamp, string Hash)> HashCache = new();

    public static void Ensure(string dir) => Directory.CreateDirectory(dir);

    public static List<DownloadItem> List(string dir)
    {
        var items = new List<DownloadItem>();
        if (!Directory.Exists(dir)) return items;

        foreach (var path in Directory.EnumerateFiles(dir))
        {
            var fi = new FileInfo(path);
            var ext = fi.Extension.ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            items.Add(new DownloadItem(
                Name: fi.Name,
                Size: fi.Length,
                SizeText: HumanSize(fi.Length),
                Platform: ext is ".apk" or ".aab" ? "android" : "windows",
                UpdatedAt: fi.LastWriteTimeUtc,
                Url: "/downloads/" + Uri.EscapeDataString(fi.Name),
                Sha256: HashOf(fi)));
        }

        // 最新的排前面
        return items.OrderByDescending(i => i.UpdatedAt).ToList();
    }

    private static string HashOf(FileInfo fi)
    {
        if (HashCache.TryGetValue(fi.FullName, out var cached) && cached.Stamp == fi.LastWriteTimeUtc)
            return cached.Hash;

        try
        {
            using var stream = fi.OpenRead();
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            HashCache[fi.FullName] = (fi.LastWriteTimeUtc, hash);
            return hash;
        }
        catch
        {
            // 文件被占用或没权限时不影响列表本身
            return "";
        }
    }

    private static string HumanSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1073741824.0:0.00} GB",
        >= 1024 * 1024 => $"{bytes / 1048576.0:0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };
}
