#if ANDROID
using System.Runtime.Loader;
using System.Text.Json;

namespace KidsSafeDesktop;

/// <summary>
/// Android 的补丁预加载器。
///
/// 为什么要有这个类、为什么它长得这么「原始」：
/// APK 是签名的只读包，里面的 dll 换不掉。唯一的办法是抢在运行时解析
/// <c>KidsSafeShared</c> 之前，用 <see cref="AssemblyLoadContext.LoadFromAssemblyPath"/>
/// 把外部文件装进默认上下文 —— 之后所有对该程序集的引用都会绑到这份新的。
///
/// 三条铁律：
///   1. 它必须是 <c>MauiProgram.CreateMauiApp()</c> 的第一条语句，
///      并且在它之前不能碰任何 KidsSafeShared 里的类型（含 AppLog）；
///   2. 这个文件只能依赖 System.* 与 Android.*，不能 using 共享库；
///   3. 加载失败必须能「安静地退回去」——加载前先给尝试次数 +1，
///      连续两次启动都失败就永久停用这份补丁，绝不让应用变成打不开的砖。
/// </summary>
internal static class PatchBootstrapper
{
    /// <summary>连续失败几次就停用。2 次足够区分「偶发崩溃」和「补丁有毒」。</summary>
    private const int MaxAttempts = 2;

    /// <summary>当前生效的 active.json 路径，供 <see cref="MarkHealthy"/> 归零尝试次数。</summary>
    private static string? _activeFile;

    public static void Run()
    {
        try
        {
            var root = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "patches");
            var active = Path.Combine(root, "active.json");
            if (!File.Exists(active))
            {
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(active));
            var rootElement = doc.RootElement;

            var attempts = rootElement.TryGetProperty("attempts", out var attemptNode) &&
                           attemptNode.TryGetInt32(out var parsedAttempts)
                ? parsedAttempts
                : 0;
            var version = rootElement.TryGetProperty("version", out var versionNode)
                ? versionNode.GetString() ?? string.Empty
                : string.Empty;
            var directory = rootElement.TryGetProperty("directory", out var dirNode)
                ? dirNode.GetString() ?? version
                : version;

            var files = new List<string>();
            if (rootElement.TryGetProperty("files", out var filesNode) &&
                filesNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in filesNode.EnumerateArray())
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        files.Add(name!);
                    }
                }
            }

            // pending 原样带回去：只有「刚下载」那一刻是 true，
            // 成功启动一次后主程序会把它置 false，这里不能擅自改回去。
            var pending = rootElement.TryGetProperty("pending", out var pendingNode) &&
                          pendingNode.ValueKind == JsonValueKind.True;

            if (string.IsNullOrWhiteSpace(version) || files.Count == 0)
            {
                Disable(root, active, "激活标记内容不完整");
                return;
            }

            if (attempts >= MaxAttempts)
            {
                Disable(root, active, $"连续 {attempts} 次启动失败，已停用补丁 {version}");
                return;
            }

            // 先把次数写回去：如果下面加载完就崩，下次启动能看到这一次也失败了。
            WriteAttempts(active, version, directory, files, attempts + 1, pending);

            foreach (var name in files)
            {
                var candidate = Path.GetFullPath(Path.Combine(root, directory, name));
                var prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
                if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
                {
                    Disable(root, active, "补丁文件路径越界：" + name);
                    return;
                }
                if (!File.Exists(candidate))
                {
                    Disable(root, active, "补丁文件缺失：" + name);
                    return;
                }

                // 这里一旦抛异常，说明这份补丁和当前宿主不兼容（签名变了、依赖缺失、
                // 或者 dll 已经被 APK 里那份抢先加载）。都按「停用」处理，不冒险。
                AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            }

            _activeFile = active;
            Log(root, $"已预加载补丁 {version}：{string.Join(", ", files)}（第 {attempts + 1} 次尝试）");
        }
        catch (Exception ex)
        {
            try
            {
                var root = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "patches");
                var active = Path.Combine(root, "active.json");
                if (File.Exists(active))
                {
                    Disable(root, active, "预加载异常：" + ex.GetType().Name + " " + ex.Message);
                }
            }
            catch (Exception)
            {
                // 连停用都失败：保持原样，下次启动再试
            }
        }
    }

    /// <summary>
    /// 应用已经正常运行到这里，说明这份补丁没有毒 —— 把尝试次数归零。
    /// 由 <c>App.OnStart</c> 调用；不调也不会坏事，只是下次启动仍按「有风险」计入。
    /// </summary>
    public static void MarkHealthy()
    {
        var active = _activeFile;
        if (active is null || !File.Exists(active))
        {
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(active));
            var rootElement = doc.RootElement;
            var version = rootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            var directory = rootElement.TryGetProperty("directory", out var d) ? d.GetString() ?? version : version;
            var files = new List<string>();
            if (rootElement.TryGetProperty("files", out var f) && f.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in f.EnumerateArray())
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) files.Add(name!);
                }
            }
            WriteAttempts(active, version, directory, files, 0, pending: false);
            _activeFile = null;
        }
        catch (Exception)
        {
            // 归零失败无所谓，最多下次多计一次
        }
    }

    private static void WriteAttempts(string active, string version, string directory,
                                      List<string> files, int attempts, bool pending)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version,
            directory,
            files,
            attempts,
            // pending=false 是给主程序看的信号：这份补丁已经成功启动过，不再是「等生效」。
            // 但 active.json 必须一直留着 —— 每次冷启动都要靠它预加载。
            pending
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllBytes(active, json);
    }

    /// <summary>停用：把标记改名成 .broken，主程序就退回 APK 自带版本。</summary>
    private static void Disable(string root, string active, string reason)
    {
        try
        {
            var broken = Path.Combine(root, "active.broken.json");
            if (File.Exists(broken))
            {
                File.Delete(broken);
            }
            File.Move(active, broken);
            Log(root, "已停用补丁：" + reason);
        }
        catch (Exception)
        {
            // 改名失败也要保证不再加载：至少把内容清空，让下次判定为无效
            try { File.WriteAllText(active, "{}"); } catch (Exception) { }
        }
    }

    private static void Log(string root, string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(root, "patch.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 日志写不了不影响启动
        }
    }
}
#endif
