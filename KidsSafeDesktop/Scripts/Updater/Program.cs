using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KidsSafeUpdater;

/// <summary>
/// 增量更新落盘器。由主程序在「补丁就绪」时拉起，参数：
///
/// <code>
///   KidsSafeUpdater.exe --apply &lt;更新目录&gt; --app-dir &lt;安装目录&gt; --pid &lt;主程序PID&gt;
///                       [--restart &lt;主程序exe&gt;]
/// </code>
///
/// 主程序退出后，它做四件事：验哈希 → 备份 → 覆盖 → 重启。
/// 任何一步失败都回滚，且一定会写 result.json —— 主程序下次启动要读它告诉家长结果。
///
/// 这个程序**不能**引用 KidsSafeShared：它是用来替换那套 dll 的，
/// 自己依赖它就会陷入「要更新一个正在被自己使用的文件」的死局。
/// </summary>
internal static class Program
{
    /// <summary>等主程序退出的上限。超过就放弃这次更新（下次再来），绝不硬杀进程。</summary>
    private const int WaitExitTimeoutMs = 60_000;

    private static int Main(string[] argv)
    {
        var options = Options.Parse(argv);
        if (options is null)
        {
            return 2;
        }

        var log = new StringBuilder();
        void Trace(string message)
        {
            log.AppendLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
        }

        Trace($"启动：apply={options.UpdateDir} appDir={options.AppDir} pid={options.ProcessId} restart={options.RestartExe ?? "-"}");

        string? version = null;
        try
        {
            // ① 等主程序退出。它在用着要被替换的 dll，不等就必然共享冲突。
            if (options.ProcessId > 0)
            {
                Trace($"等待进程 {options.ProcessId} 退出…");
                if (!WaitForExit(options.ProcessId, WaitExitTimeoutMs))
                {
                    return Fail(options, log, Trace, null, "主程序一直没退出，本次更新已取消");
                }
                Trace("主程序已退出");
            }

            // ② 读落盘计划
            var planPath = Path.Combine(options.UpdateDir, "plan.json");
            if (!File.Exists(planPath))
            {
                return Fail(options, log, Trace, null, "找不到落盘计划 plan.json");
            }
            var plan = JsonSerializer.Deserialize(
                File.ReadAllBytes(planPath), UpdaterJson.Default.ApplyPlan);
            if (plan is null || plan.Files.Count == 0)
            {
                return Fail(options, log, Trace, null, "落盘计划是空的");
            }
            version = plan.Version;
            Trace($"计划：{plan.Version}（{plan.VersionCode}），{plan.Files.Count} 个文件，删除 {plan.Removed.Count} 个");

            // ③ 目标目录可写？不可写就别折腾（装在 Program Files 时会是这种情况）
            if (!IsWritable(options.AppDir))
            {
                return Fail(options, log, Trace, version,
                    "安装目录不可写（需要管理员权限），请用完整安装包更新");
            }

            // ④ 再验一遍哈希。下载完到落盘之间可能隔了几小时，也可能被人动过。
            var payloadDir = Path.Combine(options.UpdateDir, "payload");
            foreach (var file in plan.Files)
            {
                if (!TryResolve(payloadDir, file.Path, out var source) || !File.Exists(source))
                {
                    return Fail(options, log, Trace, version, "补丁文件缺失：" + file.Path);
                }
                var actual = Sha256(source);
                if (!actual.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(options, log, Trace, version, "补丁文件校验失败：" + file.Path);
                }
            }
            Trace("哈希校验通过");

            // ⑤ 备份 + 覆盖。备份目录留在更新目录里，回滚和事后排查都靠它。
            var backupDir = Path.Combine(options.UpdateDir, "backup");
            Directory.CreateDirectory(backupDir);
            var backups = new List<(string Relative, string Backup)>();
            var created = new List<string>();

            try
            {
                foreach (var file in plan.Files)
                {
                    if (!TryResolve(options.AppDir, file.Path, out var target))
                    {
                        throw new IOException("非法路径：" + file.Path);
                    }
                    var directory = Path.GetDirectoryName(target)!;
                    Directory.CreateDirectory(directory);

                    if (File.Exists(target))
                    {
                        var backup = Path.Combine(backupDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                        File.Copy(target, backup, overwrite: true);
                        backups.Add((file.Path, backup));
                    }
                    else
                    {
                        created.Add(file.Path);
                    }

                    File.Copy(Path.Combine(payloadDir, file.Path.Replace('/', Path.DirectorySeparatorChar)),
                              target, overwrite: true);
                    Trace("已替换 " + file.Path);
                }

                // ⑥ 删除改名/合并后留下的旧文件（同样先备份，能回滚）
                foreach (var relative in plan.Removed)
                {
                    if (!TryResolve(options.AppDir, relative, out var target) || !File.Exists(target))
                    {
                        continue;
                    }
                    var backup = Path.Combine(backupDir, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: true);
                    backups.Add((relative, backup));
                    File.Delete(target);
                    Trace("已删除 " + relative);
                }
            }
            catch (Exception ex)
            {
                Trace("落盘失败：" + ex.Message);
                Rollback(options, backups, created, Trace);
                return Fail(options, log, Trace, version, "落盘失败（已回滚）：" + ex.Message);
            }

            WriteResult(options, new ApplyResult
            {
                Ok = true,
                Version = plan.Version,
                Message = "安装完成",
                RestartRequired = false,
                AppliedAt = DateTimeOffset.Now
            });
            File.Delete(Path.Combine(options.UpdateDir, "ready.flag"));
            Trace("落盘成功");

            // ⑦ 需要就重新拉起来。不重启也可以：家长下次自己打开就是新版。
            if (!string.IsNullOrWhiteSpace(options.RestartExe) && File.Exists(options.RestartExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(options.RestartExe)
                    {
                        WorkingDirectory = options.AppDir,
                        UseShellExecute = true
                    });
                    Trace("已重启主程序");
                }
                catch (Exception ex)
                {
                    Trace("重启失败（不影响已完成的更新）：" + ex.Message);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            return Fail(options, log, Trace, version, "更新器异常：" + ex.Message);
        }
        finally
        {
            TryWriteLog(options, log);
        }
    }

    // ── 回滚 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 把已经动过的文件恢复原状：改过的从备份拷回，新建的删掉。
    /// 回滚本身失败只记日志 —— 到这一步已经没有更好的选择了。
    /// </summary>
    private static void Rollback(Options options, List<(string Relative, string Backup)> backups,
                                 List<string> created, Action<string> trace)
    {
        foreach (var (relative, backup) in backups)
        {
            try
            {
                if (TryResolve(options.AppDir, relative, out var target) && File.Exists(backup))
                {
                    File.Copy(backup, target, overwrite: true);
                    trace("已恢复 " + relative);
                }
            }
            catch (Exception ex)
            {
                trace($"恢复 {relative} 失败：{ex.Message}");
            }
        }

        foreach (var relative in created)
        {
            try
            {
                if (TryResolve(options.AppDir, relative, out var target) && File.Exists(target))
                {
                    File.Delete(target);
                    trace("已删除新建的 " + relative);
                }
            }
            catch (Exception ex)
            {
                trace($"删除 {relative} 失败：{ex.Message}");
            }
        }
    }

    // ── 工具 ────────────────────────────────────────────────────────────────

    private static bool WaitForExit(int processId, int timeoutMs)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.WaitForExit(timeoutMs);
        }
        catch (ArgumentException)
        {
            // 进程已经没了（正常路径）
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, ".kidssafe-write-test");
            using (var stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(0);
            }
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>把相对路径解析到 root 下，越界返回 false（与客户端同一套规则）。</summary>
    private static bool TryResolve(string root, string relative, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
        {
            return false;
        }
        var parts = relative.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p is ".." or "."))
        {
            return false;
        }
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('\\', '/')));
        if (!candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        fullPath = candidate;
        return true;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static int Fail(Options options, StringBuilder log, Action<string> trace,
                            string? version, string message)
    {
        trace("失败：" + message);
        try
        {
            WriteResult(options, new ApplyResult
            {
                Ok = false,
                Version = version ?? string.Empty,
                Message = message,
                RestartRequired = false,
                AppliedAt = DateTimeOffset.Now
            });
            // 删掉 ready.flag：否则主程序每次启动都以为「有补丁等着装」，
            // 实际上已经失败了。下次检查更新会重新下载。
            var flag = Path.Combine(options.UpdateDir, "ready.flag");
            if (File.Exists(flag))
            {
                File.Delete(flag);
            }
        }
        catch (Exception ex)
        {
            trace("写结果失败：" + ex.Message);
        }

        // 失败也要把主程序拉起来 —— 绝不能因为更新失败，让孩子面对一个不启动的桌面。
        if (!string.IsNullOrWhiteSpace(options.RestartExe) && File.Exists(options.RestartExe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(options.RestartExe)
                {
                    WorkingDirectory = options.AppDir,
                    UseShellExecute = true
                });
                trace("已用旧版本重新启动");
            }
            catch (Exception ex)
            {
                trace("重启失败：" + ex.Message);
            }
        }
        return 1;
    }

    private static void WriteResult(Options options, ApplyResult result)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(result, UpdaterJson.Default.ApplyResult);
        File.WriteAllBytes(Path.Combine(options.UpdateDir, "result.json"), json);
    }

    private static void TryWriteLog(Options options, StringBuilder log)
    {
        try
        {
            File.AppendAllText(Path.Combine(options.UpdateDir, "apply.log"), log.ToString());
        }
        catch (Exception)
        {
            // 日志写不了不影响结果
        }
    }

    // ── 参数 ────────────────────────────────────────────────────────────────

    private sealed class Options
    {
        public string UpdateDir { get; private init; } = string.Empty;
        public string AppDir { get; private init; } = string.Empty;
        public int ProcessId { get; private init; }
        public string? RestartExe { get; private init; }

        public static Options? Parse(string[] argv)
        {
            string? update = null, appDir = null, restart = null;
            var pid = 0;

            for (var i = 0; i < argv.Length; i++)
            {
                var key = argv[i];
                string? Next() => i + 1 < argv.Length ? argv[++i] : null;

                switch (key)
                {
                    case "--apply": update = Next(); break;
                    case "--app-dir": appDir = Next(); break;
                    case "--pid": _ = int.TryParse(Next(), out pid); break;
                    case "--restart": restart = Next(); break;
                }
            }

            if (string.IsNullOrWhiteSpace(update) || string.IsNullOrWhiteSpace(appDir) ||
                !Directory.Exists(update) || !Directory.Exists(appDir))
            {
                return null;
            }

            return new Options
            {
                UpdateDir = Path.GetFullPath(update),
                AppDir = Path.GetFullPath(appDir),
                ProcessId = pid,
                RestartExe = string.IsNullOrWhiteSpace(restart) ? null : restart
            };
        }
    }
}

/// <summary>与主程序 ApplyPlan 对应的格式（两边字段名必须一致）。</summary>
internal sealed class ApplyPlan
{
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("versionCode")] public int VersionCode { get; set; }
    [JsonPropertyName("files")] public List<ApplyPlanFile> Files { get; set; } = new();
    [JsonPropertyName("removed")] public List<string> Removed { get; set; } = new();
}

internal sealed class ApplyPlanFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

/// <summary>与主程序 ApplyResult 对应。</summary>
internal sealed class ApplyResult
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("restartRequired")] public bool RestartRequired { get; set; }
    [JsonPropertyName("appliedAt")] public DateTimeOffset AppliedAt { get; set; }
}

/// <summary>
/// 源生成的 JSON 上下文。裁剪后不能用运行时反射序列化，必须显式登记要读写的类型。
/// 加新字段不用改这里（同一个类型），加新类型才要补一行 JsonSerializable。
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(ApplyPlan))]
[JsonSerializable(typeof(ApplyResult))]
internal partial class UpdaterJson : JsonSerializerContext
{
}
