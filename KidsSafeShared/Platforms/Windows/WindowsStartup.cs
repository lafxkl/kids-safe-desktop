#if WINDOWS
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// Windows 开机自启 + 守护进程（对应安卓那边「阳光守护」的双进程互拉）。
///
/// 为什么不是一个 Run 键直接起主程序：
///   自启只管「开机时起来」，管不了「起来以后被孩子结束掉」。
///   任务管理器、Alt+F4、注销、崩溃，任何一种都能让自启失效——
///   而这恰好是孩子最容易发现的一条路。
///
/// 所以 Run 键起的是**守护进程模式**（同一个 exe 加 <c>--kiosk-guard</c>）：
///   · 守护进程自己不建窗口、不进 MAUI；
///   · 它拉起真正的主程序，等它退出；
///   · 退出码 0（家长在面板点「退出」）→ 守护一起结束，下次开机再来；
///   · 非 0（被结束 / 崩溃）→ 3 秒后重新拉起，连续 5 次「起来就死」才放弃，
///     避免一个坏版本把机器拖进无限重启循环。
///
/// 家长在面板里点「退出」时会写 <c>.guard_off</c> 标记，守护看到标记就退出，
/// 这样「家长退出儿童模式」不会被守护立刻拉回来。
/// </summary>
public static class WindowsStartup
{
    /// <summary>HKCU Run 下的值名。同一个用户名下只能有一个。</summary>
    private const string RunValueName = "KidsSafeDesktop";

    /// <summary>守护模式命令行开关。安装程序写进 Run 键的也是它。</summary>
    public const string GuardArgument = "--kiosk-guard";

    /// <summary>守护/标记文件目录。刻意不用 MAUI 的 AppDataDirectory：
    /// 守护进程在 MAUI 启动之前就要读写，不能依赖 Essentials 初始化。</summary>
    public static string StateDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KidsSafeDesktop");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }

    private static string ParentExitMarker => Path.Combine(StateDir, ".guard_off");
    private static string GuardLogPath => Path.Combine(StateDir, "guard.log");

    /// <summary>当前进程的 exe 路径。dotnet run 调试时是 dotnet.exe，真机是 apphost。</summary>
    public static string ExePath
    {
        get
        {
            var p = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(p) && p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return p;
            try { return Process.GetCurrentProcess().MainModule?.FileName ?? p ?? string.Empty; }
            catch { return p ?? string.Empty; }
        }
    }

    // ── 自启开关（家长面板与安装程序都用这一份）──────────────────────────

    /// <summary>自启是否已开启（且指向当前这份 exe）。</summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(RunValueName) as string;
            if (string.IsNullOrWhiteSpace(value)) return false;

            // 程序被挪了位置（换目录、换盘）时，旧值会指向不存在的 exe：
            // 这种「看起来开了其实起不来」的状态必须判成未开启，让家长重开一次。
            var exe = ExePath;
            if (string.IsNullOrEmpty(exe)) return true;
            return value.Contains(exe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>开启/关闭开机自启。返回是否已经落到期望状态。</summary>
    public static bool SetAutoStart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null) return false;

            if (!on)
            {
                key.DeleteValue(RunValueName, false);
                return !IsAutoStartEnabled();
            }

            var exe = ExePath;
            if (string.IsNullOrEmpty(exe)) return false;

            // 一定要带 --kiosk-guard：直接起主程序的话，被结束就再也不回来了。
            key.SetValue(RunValueName, $"\"{exe}\" {GuardArgument}", RegistryValueKind.String);
            return IsAutoStartEnabled();
        }
        catch { return false; }
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // ── 守护模式 ──────────────────────────────────────────────────────────

    /// <summary>命令行里是否带守护开关。</summary>
    public static bool IsGuardMode(string[] args)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, GuardArgument, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>家长主动退出：告诉守护「别再拉我起来」。</summary>
    public static void MarkParentExit()
    {
        try { File.WriteAllText(ParentExitMarker, DateTime.Now.ToString("o")); } catch { }
    }

    /// <summary>
    /// 清掉「过期」的退出标记。
    ///
    /// 守护在家长退出后会立刻消费这个标记，正常情况它活不过几秒。但如果当时
    /// 没有守护在跑（家长关掉了自启），标记会一直留着；以后家长重新打开自启，
    /// 守护启动时看到它就跳过启动主程序一次——表现是「开了自启，登录后没进儿童桌面」。
    /// 所以在正常启动时顺手把超过 5 分钟的旧标记删掉。
    /// </summary>
    public static void CleanStaleParentExitMarker(TimeSpan olderThan)
    {
        try
        {
            if (!File.Exists(ParentExitMarker)) return;
            var age = DateTime.Now - File.GetLastWriteTime(ParentExitMarker);
            if (age > olderThan) File.Delete(ParentExitMarker);
        }
        catch { }
    }

    /// <summary>
    /// 守护主循环。**阻塞**调用，只在 <see cref="IsGuardMode"/> 为真时进入。
    /// 返回值即进程退出码（0 = 正常收工）。
    /// </summary>
    public static int RunGuard()
    {
        // 单实例：Run 键被重复写、或者用户手动又点了一次快捷方式时，
        // 不能出现两个守护各拉一个主程序（屏幕上会叠两个窗口）。
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(true, @"Local\KidsSafeDesktop.KioskGuard", out var createdNew);
            if (!createdNew)
            {
                Log("已有一个守护在运行，本次直接退出");
                return 0;
            }
        }
        catch { /* 拿不到互斥体也继续跑，总比不守护好 */ }

        try
        {
            // 上次家长点过「退出」→ 这次别再拉起来。标记只消费一次，
            // 下次开机（新登录会话）守护照常启动主程序。
            if (File.Exists(ParentExitMarker))
            {
                TryDelete(ParentExitMarker);
                Log("检测到上次家长主动退出，本次守护不启动主程序");
                return 0;
            }

            var exe = ExePath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                Log($"找不到主程序：{exe}");
                return 1;
            }

            // `dotnet run` / 调试器下 ProcessPath 是 dotnet.exe：照着它重启只会
            // 起一个不带参数的 dotnet 宿主（闪一下就没），守护等于空转。
            if (string.Equals(Path.GetFileName(exe), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                Log("当前是 dotnet 宿主，守护模式不适用，直接退出");
                return 0;
            }

            var fastFailures = 0;
            while (true)
            {
                Process? child = null;
                var startedAt = DateTime.UtcNow;

                try
                {
                    child = Process.Start(new ProcessStartInfo(exe)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(exe) ?? StateDir,
                    });
                }
                catch (Exception ex)
                {
                    Log("启动主程序失败：" + ex.Message);
                }

                if (child is null)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    continue;
                }

                Log($"已启动主程序 pid={child.Id}");
                child.WaitForExit();

                if (File.Exists(ParentExitMarker))
                {
                    TryDelete(ParentExitMarker);
                    Log("家长主动退出，守护结束");
                    return 0;
                }

                if (child.ExitCode == 0)
                {
                    Log("主程序正常退出，守护结束");
                    return 0;
                }

                // 「起来就死」说明是坏版本/缺依赖，重试只会让机器反复闪屏。
                var lived = DateTime.UtcNow - startedAt;
                fastFailures = lived < TimeSpan.FromSeconds(20) ? fastFailures + 1 : 0;
                if (fastFailures >= 5)
                {
                    Log($"连续 {fastFailures} 次启动后立刻退出，守护放弃（检查 runtime.log）");
                    return 1;
                }

                var delay = Math.Min(30, 3 * Math.Max(1, fastFailures));
                Log($"主程序退出码 {child.ExitCode}（存活 {lived.TotalSeconds:F0}s），{delay}s 后重启");
                Thread.Sleep(TimeSpan.FromSeconds(delay));
            }
        }
        finally
        {
            try { mutex?.ReleaseMutex(); } catch { }
            try { mutex?.Dispose(); } catch { }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(GuardLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
#endif
