namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「听一听」打包音频的资源管理器。
/// 音频以 MauiAsset 打包在 Resources/Raw/Audio 下，首次使用时释放到应用数据目录，
/// 再用本地文件路径交给播放器（<see cref="KidsSafeDesktop.Services.IAudioPlayerService"/>）——
/// 播放器只能读真实文件路径，无法直接读应用包内的资源流。
/// </summary>
public static class KidsAudioAssets
{
    private const string AssetPrefix = "Audio/";
    private const string Folder = "audio";

    /// <summary>资源清单版本号：改动打包音频后请 +1，用于触发重新释放。</summary>
    private const string Version = "1";

    /// <summary>释放后的根目录。</summary>
    public static string RootPath => Path.Combine(FileSystem.AppDataDirectory, Folder);

    private static string VersionFilePath => Path.Combine(RootPath, ".assetversion");

    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>确保音频已释放到可访问目录。</summary>
    public static async Task EnsureExtractedAsync()
    {
        await Gate.WaitAsync();
        try
        {
            var root = RootPath;
            Directory.CreateDirectory(root);

            if (IsUpToDate())
                return;

            foreach (var name in KidsSafeDesktop.Models.KidsAudioData.ResourceFiles)
            {
                var dest = Path.Combine(root, name);
                try
                {
                    await using var src = await FileSystem.OpenAppPackageFileAsync(AssetPrefix + name);
                    await using var dst = File.Create(dest);
                    await src.CopyToAsync(dst);
                }
                catch
                {
                    // 单个文件缺失不影响其余内容播放
                }
            }

            try { await File.WriteAllTextAsync(VersionFilePath, Version); }
            catch { /* 忽略：无法写标记文件时下次会重新释放 */ }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>取得某个打包音频释放后的本地路径。</summary>
    public static string LocalPath(string fileName) => Path.Combine(RootPath, fileName);

    private static bool IsUpToDate()
    {
        try
        {
            if (!File.Exists(VersionFilePath))
                return false;
            if (File.ReadAllText(VersionFilePath).Trim() != Version)
                return false;

            // 抽查：任一音频缺失也要重新释放
            foreach (var name in KidsSafeDesktop.Models.KidsAudioData.ResourceFiles)
                if (!File.Exists(Path.Combine(RootPath, name)))
                    return false;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
