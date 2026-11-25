#if WINDOWS
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using KidsSafeDesktop.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// Windows 儿童模式：全屏、屏蔽 Win/Alt+Tab/Ctrl+Esc/Alt+F4、隐藏任务栏（尽力而为）。
/// 退出时恢复系统键与任务栏。
/// </summary>
/// <remarks>
/// 重要：隐藏任务栏属于「修改 explorer.exe 的全局窗口状态」。进程一旦异常终止（崩溃、
/// 被任务管理器结束、系统关机），这一步不会自动回滚，任务栏会一直消失，用户只能手动重启
/// 资源管理器。因此这里做了三层兜底：
///   1. 隐藏时写标记文件 → 启动时若发现标记残留，说明上次异常退出，立即恢复（自救）；
///   2. 注册 ProcessExit / UnhandledException / UnobservedTaskException 钩子；
///   3. 恢复接口对外公开（帮助页「恢复任务栏」按钮），用户可随时手动触发。
/// </remarks>
public class WindowsKioskService : IKioskService
{
    private static IntPtr _hookId = IntPtr.Zero;
    private static LowLevelKeyboardProc? _proc;

    /// <summary>任务栏是否由本进程隐藏。避免误改不属于我们的状态。</summary>
    private static bool _taskbarHidden;

    /// <summary>退出钩子只注册一次。</summary>
    private static bool _exitHandlersRegistered;

    /// <summary>已触发正常退出，此后不再因激活事件重新隐藏任务栏。</summary>
    private static bool _exiting;

    /// <summary>已挂接激活事件的窗口，避免重复订阅。</summary>
    private static Microsoft.UI.Xaml.Window? _trackedWindow;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public Task EnterKioskAsync()
    {
        try { MakeFullScreen(); } catch { /* 非致命 */ }
        try { BlockSystemKeys(); } catch { /* 非致命 */ }
        try { TrackWindowActivation(GetWindow()); } catch { /* 非致命 */ }

        // 任务管理器与锁屏入口：写 HKCU 策略（带可逆备份），再起一个前台守护
        // 兜住「安全桌面返回」和「策略没生效」两种情况。详见 WindowsSystemPolicy。
        // 策略受家长面板的「② 屏蔽任务管理器」开关控制：家长关掉过就不要再写。
        try
        {
            if (Microsoft.Maui.Storage.Preferences.Get("block_task_manager", true))
                WindowsSystemPolicy.Apply();
        }
        catch { /* 非致命 */ }
        try
        {
            var hwnd = GetWindowHwnd();
            if (hwnd != IntPtr.Zero) WindowsForegroundGuard.Start(hwnd);
        }
        catch { /* 非致命 */ }

        try
        {
            WriteTaskbarMarker();          // 先落标记，再隐藏：崩溃时也能靠启动自救恢复
            SetTaskbarVisible(false);
            _taskbarHidden = true;
        }
        catch { /* 非致命 */ }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 监听窗口激活状态：只有本应用在前台时才隐藏任务栏；一旦失焦（启动外部应用、
    /// Ctrl+Alt+Del、最小化）或窗口关闭，立即恢复。
    /// 这样即使进程被强制结束，任务栏在绝大多数时刻都已是显示状态，
    /// 把「任务栏消失、必须重启资源管理器」的风险窗口压到最小。
    /// </summary>
    private static void TrackWindowActivation(Microsoft.UI.Xaml.Window? window)
    {
        if (window is null || ReferenceEquals(window, _trackedWindow)) return;
        _trackedWindow = window;

        window.Activated += (_, e) =>
        {
            if (_exiting) return;
            try
            {
                if (e.WindowActivationState == WindowActivationState.Deactivated)
                {
                    SetTaskbarVisible(true);
                    _taskbarHidden = false;
                }
                else
                {
                    WriteTaskbarMarker();
                    SetTaskbarVisible(false);
                    _taskbarHidden = true;
                }
            }
            catch { }
        };

        window.Closed += (_, _) => RestoreTaskbar();
    }

    public Task BringToFrontAsync()
    {
        try { GetWindow()?.Activate(); } catch { }
        return Task.CompletedTask;
    }

    public Task ExitAppAsync()
    {
        _exiting = true;                 // 退出后不再被激活事件重新隐藏

        // 先告诉守护「这次是家长主动退出」：否则守护看到非零退出码会立刻把应用拉回来，
        // 家长会觉得「点了退出又自己弹回来」，根本退不出去。
        try { WindowsStartup.MarkParentExit(); } catch { }

        try { WindowsForegroundGuard.Stop(); } catch { }
        try { WindowsSystemPolicy.Restore(); } catch { }
        RestoreTaskbar();
        try { UnblockSystemKeys(); } catch { }
        Microsoft.Maui.Controls.Application.Current?.Quit();
        return Task.CompletedTask;
    }

    /// <summary>主窗口的 Win32 句柄（抢前台要用）。</summary>
    private static IntPtr GetWindowHwnd()
    {
        try
        {
            var window = GetWindow();
            if (window is null) return IntPtr.Zero;
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        }
        catch { return IntPtr.Zero; }
    }

    // ---- 任务栏恢复（对外公开，供崩溃兜底与帮助页调用）----

    /// <summary>
    /// 恢复系统任务栏。幂等、全程 try/catch，任何情况下都不会抛异常。
    /// 即使本进程并未隐藏过任务栏也可以安全调用（用于用户手动自救）。
    /// </summary>
    public static void RestoreTaskbar()
    {
        try
        {
            SetTaskbarVisible(true);
            _taskbarHidden = false;
        }
        catch { }
        try { DeleteTaskbarMarker(); } catch { }
    }

    /// <summary>
    /// 启动自检：若上次会话写了标记却没清理，说明上次是异常退出，
    /// 这里兜底把任务栏恢复，避免用户必须手动重启资源管理器。
    /// </summary>
    public static void RecoverTaskbarIfNeeded()
    {
        try
        {
            if (!MarkerExists()) return;
            SetTaskbarVisible(true);
            DeleteTaskbarMarker();
        }
        catch { }
    }

    /// <summary>
    /// 注册进程退出与崩溃的兜底钩子。应在应用启动最早阶段调用一次。
    /// </summary>
    public static void RegisterExitHandlers()
    {
        if (_exitHandlersRegistered) return;
        _exitHandlersRegistered = true;

        try
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => SafeRestore();
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                LogUnhandled(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
                SafeRestore();
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogUnhandled(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }
        catch { }

        // WinUI 未处理异常（UI 线程 async void / 绑定 / 布局等）。
        // 儿童 Kiosk 模式的核心诉求是「绝不崩溃」：任何 UI 线程未处理异常
        // （网络超时 / IO / 取消 / 布局重入 / 绑定错误等）都不应冻结或杀死进程。
        // 因此这里无条件标记 e.Handled = true，让应用继续运行；
        // 同时先完整记录堆栈到 unhandled.log、并恢复任务栏，便于事后排查。
        try
        {
            var app = Microsoft.UI.Xaml.Application.Current;
            if (app is not null)
            {
                app.UnhandledException += (sender, e) =>
                {
                    LogUnhandled(e.Exception, "WinUI.UnhandledException");
                    RestoreTaskbar();
                    // 关键：任何 UI 线程未处理异常都不再击穿到 Debugger.Break / 进程退出 (-1)
                    e.Handled = true;
                };
            }
        }
        catch { }
    }

    /// <summary>
    /// 判断异常是否属于「可优雅降级」的类别：网络/超时/IO/取消/鉴权类，
    /// 这些是外部依赖问题，不应让整个 Kiosk 进程崩溃。
    /// </summary>
    private static bool IsRecoverable(Exception? ex)
    {
        if (ex is null) return false;
        if (ex is TaskCanceledException
            or HttpRequestException
            or TimeoutException
            or OperationCanceledException
            or IOException
            or SocketException
            or UnauthorizedAccessException)
        {
            return true;
        }
        if (ex is AggregateException ae)
        {
            foreach (var inner in ae.InnerExceptions)
                if (IsRecoverable(inner)) return true;
        }
        if (ex.InnerException is not null && IsRecoverable(ex.InnerException))
            return true;
        return false;
    }

    /// <summary>
    /// 把未处理异常的完整信息追加写入应用数据目录下的 unhandled.log。
    /// 该文件用于上架前/线上排查：复现后把内容发给开发者即可精准定位。
    /// </summary>
    private static void LogUnhandled(Exception? ex, string source)
    {
        try
        {
            var dir = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
            if (string.IsNullOrEmpty(dir)) return;
            var path = Path.Combine(dir, "unhandled.log");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}");
            sb.AppendLine($"Type:    {ex?.GetType().FullName}");
            sb.AppendLine($"Message: {ex?.Message}");
            sb.AppendLine("StackTrace:");
            sb.AppendLine(ex?.StackTrace);
            if (ex?.InnerException is not null)
                sb.AppendLine("Inner: " + ex.InnerException);
            sb.AppendLine("--------------------------------------------------");
            File.AppendAllText(path, sb.ToString());
        }
        catch { /* 日志写失败不应再抛异常 */ }
    }

    private static void SafeRestore()
    {
        // 策略是持久化的注册表值，进程无论怎么退出都得按备份还原，
        // 否则下次开机会是一台「任务管理器永远打不开」的机器。
        try { WindowsSystemPolicy.Restore(); } catch { }
        try { WindowsForegroundGuard.Stop(); } catch { }

        if (!_taskbarHidden && !MarkerExists()) return;
        RestoreTaskbar();
    }

    // ---- 标记文件（崩溃自救的关键）----

    private const string MarkerFileName = ".taskbar_hidden_by_kids";

    private static string MarkerPath()
    {
        try
        {
            var dir = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
            if (!string.IsNullOrEmpty(dir)) return Path.Combine(dir, MarkerFileName);
        }
        catch { }
        return Path.Combine(Path.GetTempPath(), MarkerFileName);
    }

    private static void WriteTaskbarMarker()
    {
        try { File.WriteAllText(MarkerPath(), DateTime.Now.ToString("o")); } catch { }
    }

    private static void DeleteTaskbarMarker()
    {
        try { File.Delete(MarkerPath()); } catch { }
    }

    private static bool MarkerExists()
    {
        try { return File.Exists(MarkerPath()); } catch { return false; }
    }

    // ---- 全屏 ----

    private static Microsoft.UI.Xaml.Window? GetWindow()
    {
        var win = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        return win?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w ? w : null;
    }

    private static void MakeFullScreen()
    {
        var window = GetWindow();
        if (window is null) return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // 只切到 FullScreen presenter 即可进入全屏——它本身就没有标题栏与边框。
        // 切勿再调用 SetBorderAndTitleBar(false, false)：那会把窗口先置入「无边框过渡态」，
        // 随后 MAUI 在窗口消息（WM_SIZE 等）里再次执行 SetTitleBarVisibility 时，
        // AppWindow.GetFromWindowId 会因句柄失效抛 ArgumentException；该异常发生在
        // Win32 窗口过程（非托管回调）里，属于致命异常，直接导致进程以 -1 (0xffffffff) 退出。
        // （unhandled.log 里 LayoutCycleException 之后紧跟的 ArgumentException 正是此因。）
        //
        // 幂等保护：EnterKioskAsync 在启动流程里被调用两次（初始化 + 根页面就绪后），
        // 若已经是全屏则不再重复 SetPresenter——避免窗口再次进入过渡态而触发句柄竞态。
        if (appWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }

    // ---- 屏蔽系统键 ----

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);
            bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;

            if (vk is 0x5B or 0x5C)          // 左右 Win 键（Win+R / Win+E / Win+X 全部失效）
                return (IntPtr)1;
            if (alt && vk == 0x09)           // Alt+Tab
                return (IntPtr)1;
            if (ctrl && alt && vk == 0x09)   // Ctrl+Alt+Tab（方向键切换窗口）
                return (IntPtr)1;
            if (alt && vk == 0x1B)           // Alt+Esc（直接切到下一个窗口，绕过 Alt+Tab）
                return (IntPtr)1;
            if (alt && vk == 0x73)           // Alt+F4
                return (IntPtr)1;
            if (ctrl && vk == 0x1B)          // Ctrl+Esc（打开开始菜单）
                return (IntPtr)1;
            if (ctrl && shift && vk == 0x1B) // Ctrl+Shift+Esc（任务管理器）
                return (IntPtr)1;

            // Ctrl+Alt+Del 不在这里：它由 winlogon 在「安全注意序列」里直接处理，
            // 根本不进用户态键盘钩子，任何 SetWindowsHookEx 都收不到。
            // 缓解手段是 DisableTaskMgr（进了安全桌面也没任务管理器可点）
            // + 安全桌面返回后抢回前台（WindowsForegroundGuard）。
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static void BlockSystemKeys()
    {
        if (_hookId != IntPtr.Zero) return;
        _proc = HookProc;
        _hookId = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
    }

    private static void UnblockSystemKeys()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _proc = null;
    }

    // ---- 任务栏 ----

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void SetTaskbarVisible(bool visible)
    {
        var cmd = visible ? SW_SHOW : SW_HIDE;

        var h = FindWindowW("Shell_TrayWnd", null);
        if (h != IntPtr.Zero) ShowWindow(h, cmd);

        // 多显示器时的副任务栏
        var h2 = FindWindowW("Shell_SecondaryTrayWnd", null);
        if (h2 != IntPtr.Zero) ShowWindow(h2, cmd);
    }
}
#endif
