#if WINDOWS
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// 用「用户策略 + 可逆备份」的方式管住任务管理器与锁屏入口。
///
/// 为什么必须可逆：
///   DisableTaskMgr 这类值是**持久**的，写进去以后重启也还在。进程被孩子结束、
///   或者我们自己的代码崩了，这些键不会自己消失，用户会得到一台「任务管理器永远
///   打不开」的机器，而且找不到原因。所以每次写入前先把原值抄到
///   <c>%LOCALAPPDATA%\KidsSafeDesktop\policy_backup.txt</c>，退出/崩溃后下次启动
///   按备份逐条还原（原来是「不存在」的就删掉，原来有值的写回原值）。
///
/// 能拦到什么程度（诚实说明，别指望更多）：
///   · Ctrl+Shift+Esc —— 全局键盘钩子吃掉 + DisableTaskMgr 双保险；
///   · Win+R → taskmgr —— Win 键被吃，且就算敲出来也会被策略挡；
///   · 任务栏右键 → 任务管理器 —— 任务栏已隐藏，且策略挡；
///   · Ctrl+Alt+Del → 任务管理器 —— **拦不住**（SAS 由 winlogon 直接处理，
///     任何用户态钩子都收不到）。这里的缓解手段是：禁用任务管理器本身，
///     并在从安全桌面返回后立刻把儿童桌面抢回前台（见 WindowsForegroundGuard）。
/// </summary>
public static class WindowsSystemPolicy
{
    private const string PolicyKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";

    /// <summary>我们要管的值。加新项直接往这里加，备份/还原会自动跟上。</summary>
    private static readonly (string Name, int Value)[] Policies =
    {
        ("DisableTaskMgr", 1),          // 任务管理器被禁用
        ("DisableLockWorkstation", 1),  // Win+L / Ctrl+Alt+Del 里的「锁定」失效
        ("DisableChangePassword", 1),   // Ctrl+Alt+Del 里的「更改密码」失效
    };

    private const string AbsentToken = "__absent__";

    private static string BackupPath => Path.Combine(WindowsStartup.StateDir, "policy_backup.txt");

    /// <summary>当前是否已经有备份（= 我们改过策略且还没还原）。</summary>
    public static bool IsApplied => File.Exists(BackupPath);

    /// <summary>应用策略。幂等；重复调用不会把「我们自己的值」当成原值备份。</summary>
    public static void Apply()
    {
        try
        {
            var backup = LoadBackup();
            using var key = Registry.CurrentUser.CreateSubKey(PolicyKeyPath, true);
            if (key is null) return;

            foreach (var (name, value) in Policies)
            {
                if (!backup.ContainsKey(name))
                {
                    var existing = key.GetValue(name);
                    backup[name] = existing is null
                        ? AbsentToken
                        : Convert.ToInt32(existing).ToString();
                }
                key.SetValue(name, value, RegistryValueKind.DWord);
            }

            SaveBackup(backup);
            BroadcastPolicyChange();
        }
        catch
        {
            // 策略写失败不是致命问题：键盘钩子仍然在拦 Ctrl+Shift+Esc。
        }
    }

    /// <summary>按备份还原。幂等；没有备份时什么都不做（绝不误删别人的策略）。</summary>
    public static void Restore()
    {
        try
        {
            var backup = LoadBackup();
            if (backup.Count == 0)
            {
                TryDeleteBackup();
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey(PolicyKeyPath, true);
            if (key is not null)
            {
                foreach (var (name, raw) in backup)
                {
                    if (raw == AbsentToken)
                    {
                        key.DeleteValue(name, false);
                    }
                    else if (int.TryParse(raw, out var original))
                    {
                        key.SetValue(name, original, RegistryValueKind.DWord);
                    }
                }
            }

            TryDeleteBackup();
            BroadcastPolicyChange();
        }
        catch { }
    }

    /// <summary>
    /// 启动自检：备份还在，说明上次是异常退出（崩溃/被强杀），策略还留在注册表里。
    /// 先还原，再由 kiosk 正常流程重新 Apply，避免「一开机任务管理器就是坏的」。
    /// </summary>
    public static void RecoverIfNeeded()
    {
        try
        {
            if (!IsApplied) return;
            Restore();
        }
        catch { }
    }

    // ── 备份文件 ──────────────────────────────────────────────────────────

    private static Dictionary<string, string> LoadBackup()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(BackupPath)) return map;
            foreach (var line in File.ReadAllLines(BackupPath))
            {
                var tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                map[line[..tab]] = line[(tab + 1)..];
            }
        }
        catch { }
        return map;
    }

    private static void SaveBackup(Dictionary<string, string> map)
    {
        try
        {
            var lines = map.Select(kv => kv.Key + "\t" + kv.Value);
            File.WriteAllLines(BackupPath, lines);
        }
        catch { }
    }

    private static void TryDeleteBackup()
    {
        try { File.Delete(BackupPath); } catch { }
    }

    // ── 通知系统策略已变（explorer / 新进程会重新读策略）──────────────────

    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam,
                                                    string lParam, int flags, int timeout, out IntPtr result);

    private static void BroadcastPolicyChange()
    {
        try
        {
            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero,
                               "Policy", SMTO_ABORTIFHUNG, 1000, out _);
        }
        catch { }
    }
}

/// <summary>
/// 前台守护：处理两件键盘钩子管不到的事。
///
/// 1) <b>安全桌面返回</b>：孩子按 Ctrl+Alt+Del（或 Win+L 在策略生效前）会切到
///    安全桌面，我们的钩子和窗口全部失效。回到普通桌面后，Windows 会把焦点
///    还给「上一个窗口」——未必是我们。这里轮询输入桌面是否可用，一旦从
///    「锁屏/安全桌面」回到正常桌面，立刻把儿童桌面抢回前台。
/// 2) <b>任务管理器兜底</b>：策略被组策略覆盖、或系统版本不认 DisableTaskMgr 时，
///    直接盯进程：发现 taskmgr 起来就结束它。1 秒级轮询，开销可以忽略。
/// </summary>
public sealed class WindowsForegroundGuard : IDisposable
{
    private static WindowsForegroundGuard? _current;

    private readonly IntPtr _hwnd;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _thread;

    private WindowsForegroundGuard(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>启动（重复调用会替换旧实例）。hwnd 为主窗口句柄。</summary>
    public static void Start(IntPtr hwnd)
    {
        Stop();
        if (hwnd == IntPtr.Zero) return;
        var guard = new WindowsForegroundGuard(hwnd);
        guard._thread = new Thread(guard.Loop)
        {
            IsBackground = true,
            Name = "KidsSafeForegroundGuard",
        };
        guard._thread.Start();
        _current = guard;
    }

    /// <summary>停止。退出 kiosk 前必须调用，否则退出后还会被抢焦点。</summary>
    public static void Stop()
    {
        var guard = _current;
        _current = null;
        guard?.Dispose();
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _thread?.Join(1500); } catch { }
        try { _cts.Dispose(); } catch { }
    }

    private void Loop()
    {
        var wasLocked = IsInputDesktopUnavailable();
        var tick = 0;

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(1000);
                tick++;

                // ② 任务管理器兜底（每秒一次足够：人眼看不到这么快的窗口）
                if (Microsoft.Maui.Storage.Preferences.Get("block_task_manager", true))
                    KillTaskManager();

                // ① 安全桌面 → 普通桌面
                var locked = IsInputDesktopUnavailable();
                if (wasLocked && !locked)
                {
                    // 桌面切换动画还没结束就抢焦点会被系统拒绝，等半秒。
                    Thread.Sleep(500);
                    ForceForeground(_hwnd);
                }
                wasLocked = locked;
            }
            catch
            {
                // 守护线程绝不能因为一次异常整个退出
                if (tick % 60 == 0) Thread.Sleep(1000);
            }
        }
    }

    private static void KillTaskManager()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("taskmgr"))
            {
                try { p.Kill(true); } catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    // ── 输入桌面是否被安全桌面占据（锁屏 / Ctrl+Alt+Del / UAC）─────────────

    private const uint DESKTOP_SWITCHDESKTOP = 0x0100;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(int dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    private static bool IsInputDesktopUnavailable()
    {
        var h = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
        if (h == IntPtr.Zero) return true;   // 打不开 = 正在安全桌面上
        CloseDesktop(h);
        return false;
    }

    // ── 抢前台 ────────────────────────────────────────────────────────────

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private static void ForceForeground(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return;
            if (GetForegroundWindow() == hwnd) return;

            ShowWindow(hwnd, SW_RESTORE);

            // 前台锁（foreground lock）会让「后台进程调用 SetForegroundWindow」直接
            // 变成任务栏闪烁。把自己的输入队列临时挂到当前前台线程上，才算同一条
            // 输入队列里的调用，系统才认。
            var foreground = GetForegroundWindow();
            var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
            var self = GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != self
                           && AttachThreadInput(self, foregroundThread, true);
            try
            {
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached) AttachThreadInput(self, foregroundThread, false);
            }
        }
        catch { }
    }
}
#endif
