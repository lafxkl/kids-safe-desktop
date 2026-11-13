using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 诊断日志的落盘位置。
///
/// 关键点（踩过坑）：<c>FileSystem.AppDataDirectory</c> 在安卓上是
/// <c>/data/user/0/&lt;包名&gt;/files</c>（应用内部私有目录），
/// 用手机文件管理器或 USB 连电脑都看不到，用户会以为「目录没创建」。
/// 所以安卓上优先写到 <c>Android/data/&lt;包名&gt;/files/logs</c>（外部专属目录，
/// 卸载时随应用一并删除，不需要任何存储权限），写不进去再退回内部目录。
///
/// 注意：安卓 11+ 用 USB/MTP 浏览 <c>Android/data</c> 也会被系统挡住，
/// 手机自带文件管理器多数可见。真正省事的路子是应用内「家长设置 → 诊断日志」直接看。
/// </summary>
public static class DiagLog
{
    private const long MaxBytes = 256 * 1024;

    private static string? _dir;

    /// <summary>日志所在目录（首次访问时确定，失败则回退内部私有目录）。</summary>
    public static string Dir => _dir ??= ResolveDir();

    /// <summary>日志目录是否落在「外部可见」的位置（安卓上才可能为 false）。</summary>
    public static bool IsExternal { get; private set; }

    private static string ResolveDir()
    {
        try
        {
#if ANDROID
            var ext = global::Android.App.Application.Context.GetExternalFilesDir(null);
            if (ext is not null && !string.IsNullOrEmpty(ext.AbsolutePath))
            {
                var dir = Path.Combine(ext.AbsolutePath, "logs");
                Directory.CreateDirectory(dir);

                // 真写一次再确认，避免某些机型返回路径但无写权限
                var probe = Path.Combine(dir, ".probe");
                File.WriteAllText(probe, "1");
                File.Delete(probe);

                IsExternal = true;
                return dir;
            }
#endif
        }
        catch
        {
            // 外部目录不可用 → 退回内部私有目录
        }

        var fallback = FileSystem.AppDataDirectory;
        try { Directory.CreateDirectory(fallback); } catch { }
        IsExternal = false;
        return fallback;
    }

    public static string PathOf(string fileName) => Path.Combine(Dir, fileName);

    /// <summary>追加一段日志（自动加时间戳；超过 256KB 清空重写）。</summary>
    public static void Append(string fileName, string content)
    {
        try
        {
            var path = PathOf(fileName);
            var fi = new FileInfo(path);
            if (fi.Exists && fi.Length > MaxBytes) fi.Delete();

            File.AppendAllText(path, $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====\n{content}\n\n");
        }
        catch
        {
            // 日志失败绝不影响使用
        }
    }

    /// <summary>读日志尾部（默认最多 64KB，够看明白最近出了什么事）。</summary>
    public static string ReadTail(string fileName, int maxBytes = 64 * 1024)
    {
        try
        {
            var path = PathOf(fileName);
            if (!File.Exists(path)) return "";

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= maxBytes)
            {
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }

            fs.Seek(-maxBytes, SeekOrigin.End);
            using var sr2 = new StreamReader(fs);
            var text = sr2.ReadToEnd();
            return "…（前文已省略）…\n" + text;
        }
        catch (Exception ex)
        {
            return "(读取日志失败: " + ex.Message + ")";
        }
    }

    public static long SizeOf(string fileName)
    {
        try
        {
            var fi = new FileInfo(PathOf(fileName));
            return fi.Exists ? fi.Length : 0;
        }
        catch { return 0; }
    }
}
