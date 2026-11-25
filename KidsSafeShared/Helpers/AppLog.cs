using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace KidsSafeDesktop.Helpers;

/// <summary>日志级别。只写进文件，不做分级开关之外的过滤 —— 孩子端的日志量很小，全留才有诊断价值。</summary>
public enum LogLevel
{
    Trace = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

/// <summary>
/// 运行日志：应用运行期发生的事，按行追加到 <c>runtime.log</c>，**发布版照常记录**。
///
/// 为什么要单独一套，而不是把 nav.log 改名字：
///  · nav.log 只有「页面跳转 + 异常」，出问题时最想知道的恰恰是没写进去的那些
///    （启动卡在哪一步、Kiosk 有没有进、同步是否失败、家长改了配置有没有生效）；
///  · nav.log 每次 <see cref="DiagLog.Append"/> 都会写一段「==== 时间 ====」横幅，一行一个事件反而看不出来；
///  · 需要一个能滚动切分的、有级别的、能按分类过滤的日志。
///
/// 设计约束（都很实际）：
///  · **绝不抛异常**：日志把应用搞崩是最糟的失控方式，所有入口都包 try；
///  · **线程安全**：Kiosk/同步/TTS 回调来自不同线程，用一把锁串起写入；
///  · **不长期持有文件句柄**：每行开关一次文件。量小（一次会话几百行），换来的是
///    家长端在应用运行时也能随时读、随时删，Windows 上更不会因为句柄没放而删不掉；
///  · **自动切分**：超过 <see cref="MaxBytes"/> 就滚成 runtime.1.log，只留一份旧的，防止长期运行把盘写满。
///
/// 落盘位置复用 <see cref="DiagLog.Dir"/>： Windows 在 %LOCALAPPDATA% 下，安卓在
/// Android/data/&lt;包名&gt;/files/logs（外部可见，卸载时自动清除）。
/// </summary>
public static class AppLog
{
    /// <summary>运行日志文件名（在 <see cref="DiagLog.Dir"/> 下）。</summary>
    public const string FileName = "runtime.log";

    /// <summary>切分阈值。到量就滚成 runtime.1.log，只保留一份旧的。</summary>
    private const long MaxBytes = 512 * 1024;

    private static readonly object Gate = new();

    /// <summary>不带 BOM 的 UTF-8：文件被记事本/VS Code 打开时不会开头多一个隐形字符。</summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>关闭后所有写入立即返回。默认开启；发布版同样是开启的 —— 这是本功能的要点。</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>日志文件完整路径。</summary>
    public static string FilePath => DiagLog.PathOf(FileName);

    /// <summary>日志所在目录（家长设置里「诊断日志」页会显示它）。</summary>
    public static string Dir => DiagLog.Dir;

    /// <summary>当前日志大小（字节）。</summary>
    public static long Size
    {
        get
        {
            try
            {
                var fi = new FileInfo(FilePath);
                return fi.Exists ? fi.Length : 0;
            }
            catch { return 0; }
        }
    }

    // ───────────────────────── 写 ─────────────────────────

    /// <summary>一般信息：进入了什么页面、做了什么操作、某个服务起来了。</summary>
    public static void I(string category, string message) => Write(LogLevel.Info, category, message, null);

    /// <summary>警告：不影响使用但值得知道（某个接口失败、降级到本地、权限被拒）。</summary>
    public static void W(string category, string message, Exception? ex = null) => Write(LogLevel.Warn, category, message, ex);

    /// <summary>错误：异常或明确的失败。传了 <paramref name="ex"/> 会附完整堆栈。</summary>
    public static void E(string category, string message, Exception? ex = null) => Write(LogLevel.Error, category, message, ex);

    /// <summary>细粒度追踪，默认不写（把 <see cref="Verbose"/> 打开才有），用于临时摸排。</summary>
    public static void T(string category, string message)
    {
        if (Verbose) Write(LogLevel.Trace, category, message, null);
    }

    /// <summary>是否连 Trace 一起写。默认关；排障时可以临时打开或从家长端下发。</summary>
    public static bool Verbose { get; set; }

    private static void Write(LogLevel level, string category, string message, Exception? ex)
    {
        if (!Enabled) return;

        try
        {
            var sb = new StringBuilder(160);
            sb.Append(DateTime.Now.ToString("MM-dd HH:mm:ss.fff"))
              .Append(' ').Append(Tag(level))
              .Append('[').Append(category).Append("] ")
              .Append(message);

            if (ex is not null)
            {
                sb.Append('\n').Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
                if (!string.IsNullOrWhiteSpace(ex.StackTrace)) sb.Append('\n').Append(ex.StackTrace);
                var inner = ex.InnerException;
                var depth = 0;
                while (inner is not null && depth++ < 3)
                {
                    sb.Append("\n  └─ ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
                    inner = inner.InnerException;
                }
            }

            AppendRaw(sb.ToString());
        }
        catch
        {
            // 日志失败绝不影响使用
        }
    }

    private static void AppendRaw(string line)
    {
        lock (Gate)
        {
            try
            {
                var path = FilePath;

                // 切分：先删旧的 .1，再把当前滚过去
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var bak = DiagLog.PathOf("runtime.1.log");
                    try { if (File.Exists(bak)) File.Delete(bak); } catch { }
                    try { File.Move(path, bak); } catch { }
                }

                File.AppendAllText(path, line + "\n", Utf8NoBom);
            }
            catch
            {
                // 目录被删、磁盘满、权限问题 —— 都不该影响孩子用
            }
        }
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Error => "E ",
        LogLevel.Warn => "W ",
        LogLevel.Trace => "T ",
        _ => "I ",
    };

    // ───────────────────────── 会话 ─────────────────────────

    private static bool _installed;

    /// <summary>
    /// 挂全局异常捕获 + 写会话横幅。幂等，重复调用只生效一次。
    /// 必须在 <c>MauiProgram.CreateMauiApp()</c> 里尽早调用 —— 越早，能记到的启动问题越多。
    /// </summary>
    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        try
        {
            // 会话横幅：把「哪台机器、哪个版本、什么系统」先钉下来，后面所有行才有上下文。
            // 每项单独容错 —— 某个平台不支持某个字段（厂商/型号在个别后端就是会抛）时，
            // 不能把整条横幅一起拖没。
            var sb = new StringBuilder(200);
            sb.Append("─── 会话开始 ─── ");
            sb.Append(Safe(() => AppInfo.Current.PackageName)).Append(' ');
            sb.Append('v').Append(Safe(() => AppInfo.Current.VersionString))
              .Append('(').Append(Safe(() => AppInfo.Current.BuildString)).Append(") ");
            sb.Append(Safe(() => DeviceInfo.Current.Platform.ToString())).Append(' ')
              .Append(Safe(() => DeviceInfo.Current.VersionString));
            sb.Append(" | ").Append(Safe(() => DeviceInfo.Current.Manufacturer)).Append(' ')
              .Append(Safe(() => DeviceInfo.Current.Model));
            sb.Append(" | ").Append(Safe(() => DeviceInfo.Current.Idiom.ToString()));
            sb.Append(" | ").Append(Runtime());
            AppendRaw(sb.ToString());
        }
        catch { }

        // ① 主线程/任意线程未捕获异常：进程通常会随之结束，这是最后能留下的话
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            E("崩溃", "AppDomain 未处理异常（IsTerminating=" + e.IsTerminating + "）", ex);
            try { DiagLog.Append("crash.log", e.ExceptionObject?.ToString() ?? "(null)"); } catch { }
        };

        // ② 被丢弃的 Task 异常：await 没人接、也没人观察，以往完全静默
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            E("崩溃", "未观察的 Task 异常", e.Exception);
            e.SetObserved();     // 保持原有行为：不因为记了日志就改掉「吞掉」的语义
        };

        // ③ 正常退出：留个句号，下次看日志能分清「被杀」还是「退出」
        AppDomain.CurrentDomain.ProcessExit += (_, _) => I("生命周期", "进程退出");

        I("生命周期", "日志已就绪 → " + FilePath);
    }

    private static string Runtime()
    {
        try { return ".NET " + Environment.Version; }
        catch { return ".NET ?"; }
    }

    /// <summary>取一个可能抛异常的环境字段，失败就给 "?"。</summary>
    private static string Safe(Func<string?> get)
    {
        try { return get() ?? "?"; }
        catch { return "?"; }
    }

    // ───────────────────────── 读 / 清 ─────────────────────────

    /// <summary>读最近的日志（默认 64KB）。给家长端「诊断日志」页用。</summary>
    public static string Tail(int maxBytes = 64 * 1024)
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                // 也可能刚切分过，旧的还在
                var bak = DiagLog.PathOf("runtime.1.log");
                return File.Exists(bak) ? ReadBak(bak, maxBytes) : "";
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length <= maxBytes)
            {
                using var sr = new StreamReader(fs, Encoding.UTF8);
                return sr.ReadToEnd();
            }

            fs.Seek(-maxBytes, SeekOrigin.End);
            using var sr2 = new StreamReader(fs, Encoding.UTF8);
            return "…（前文已省略，完整内容见文件）…\n" + sr2.ReadToEnd();
        }
        catch (Exception ex)
        {
            return "(读取运行日志失败: " + ex.Message + ")";
        }
    }

    private static string ReadBak(string path, int maxBytes)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            var text = sr.ReadToEnd();
            return text.Length <= maxBytes ? text : text.Substring(text.Length - maxBytes);
        }
        catch { return ""; }
    }

    /// <summary>清空日志（家长端按钮）。切分出来的旧文件一起删。</summary>
    public static bool Clear()
    {
        lock (Gate)
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                var bak = DiagLog.PathOf("runtime.1.log");
                if (File.Exists(bak)) File.Delete(bak);
                return true;
            }
            catch (Exception ex)
            {
                E("日志", "清空失败", ex);
                return false;
            }
        }
    }

    /// <summary>把当前日志复制一份到可分享的位置（安卓分享 / Windows 打开目录用）。</summary>
    public static string? ExportTo(string targetPath)
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            File.Copy(FilePath, targetPath, overwrite: true);
            return targetPath;
        }
        catch (Exception ex)
        {
            E("日志", "导出失败", ex);
            return null;
        }
    }
}
