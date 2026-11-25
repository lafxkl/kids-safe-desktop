namespace KidsSafeDesktop.Updates;

/// <summary>
/// 三端各自「补丁放哪儿」的唯一出处。别在别处拼路径 —— 落盘器和主程序必须
/// 对同一个目录有同一个理解，否则会出现「下载好了但没人装」。
/// </summary>
public static class UpdatePaths
{
    /// <summary>清单里的平台标识。</summary>
    public static string PlatformKey =>
        OperatingSystem.IsAndroid() ? "android" : "windows";

    /// <summary>本机宿主版本（csproj 的 ApplicationDisplayVersion）。</summary>
    public static string HostVersion => Helpers.AppVersion.Version;

    /// <summary>本机构建号（Android 的 versionCode / Windows 的 ApplicationVersion）。</summary>
    public static int HostCode =>
        int.TryParse(Microsoft.Maui.ApplicationModel.AppInfo.Current.BuildString, out var code) ? code : 0;

    /// <summary>安装目录：apphost 与 dll 所在的地方。</summary>
    public static string InstallDir =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// 补丁工作区。
    /// <list type="bullet">
    ///   <item>Windows：<c>%LOCALAPPDATA%\KidsSafeDesktop\updates</c>，与安装目录分开，
    ///   既不会被「检查文件完整性」误判，卸载时也能整目录删掉。</item>
    ///   <item>Android：<c>files/patches</c>，与 <c>PatchBootstrapper</c> 读取的路径是同一个。</item>
    /// </list>
    /// </summary>
    public static string WorkRoot
    {
        get
        {
            if (OperatingSystem.IsAndroid())
            {
                return Path.Combine(FilesDir, "patches");
            }
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "KidsSafeDesktop", "updates");
        }
    }

    /// <summary>某个版本的暂存目录。</summary>
    public static string VersionDir(string version) => Path.Combine(WorkRoot, version);

    /// <summary>某个版本的下载区（保持相对路径）。</summary>
    public static string PayloadDir(string version) => Path.Combine(VersionDir(version), "payload");

    /// <summary>Windows 落盘器文件名。</summary>
    public const string UpdaterExe = "KidsSafeUpdater.exe";

    public static string UpdaterPath => Path.Combine(InstallDir, UpdaterExe);

    /// <summary>
    /// Android 的私有文件目录。这里刻意不用 <c>FileSystem.AppDataDirectory</c>：
    /// PatchBootstrapper 必须在任何 MAUI 类型被触碰之前就能算出这个路径，
    /// 而它只能依赖 System / Android 命名空间。
    /// </summary>
    private static string FilesDir =>
#if ANDROID
        Android.App.Application.Context.FilesDir!.AbsolutePath;
#else
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif
}
