using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KidsSafeDesktop.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		// ⚠️ 必须放在 InitializeComponent 之前：守护模式（--kiosk-guard）不建任何窗口，
		// 只是拉起主程序并盯着它。走到 XAML 初始化就晚了——会先闪一个空窗口，
		// 而且在没有窗口的情况下退出容易留下 WinUI 的收尾异常。
		var args = Environment.GetCommandLineArgs();
		if (KidsSafeDesktop.Platforms.Windows.WindowsStartup.IsGuardMode(args))
		{
			var code = 1;
			try { code = KidsSafeDesktop.Platforms.Windows.WindowsStartup.RunGuard(); }
			catch (Exception ex) { KidsSafeDesktop.Helpers.AppLog.E("守护", "守护循环异常退出", ex); }
			Environment.Exit(code);
		}

		this.InitializeComponent();

		// 兜底：任务栏隐藏属于「修改 explorer.exe 全局窗口状态」，进程一旦崩溃/被强杀
		// 不会自动回滚。这里做两件事：
		//   1) 启动时若发现上次会话的隐藏标记残留 → 立即恢复（自救，用户无需重启资源管理器）
		//   2) 注册 ProcessExit / 未处理异常钩子 → 任何退出路径都尝试恢复
		// 第 3 件：DisableTaskMgr 这类策略键是**持久**的，更必须按备份还原。
		try
		{
			KidsSafeDesktop.Platforms.Windows.WindowsKioskService.RecoverTaskbarIfNeeded();
			KidsSafeDesktop.Platforms.Windows.WindowsSystemPolicy.RecoverIfNeeded();
			// 上次家长「退出」的标记如果已经放了很久（当时没有守护在跑），
			// 会让下一次守护跳过启动；这里清掉过期的，保留刚写的（守护还要用）。
			KidsSafeDesktop.Platforms.Windows.WindowsStartup.CleanStaleParentExitMarker(
				TimeSpan.FromMinutes(5));
			KidsSafeDesktop.Platforms.Windows.WindowsKioskService.RegisterExitHandlers();
		}
		catch { /* 兜底逻辑本身绝不能影响启动 */ }

		/*
		 * WinUI 的界面线程异常走的是 Microsoft.UI.Xaml.Application.UnhandledException，
		 * 不经过 .NET 的 AppDomain.UnhandledException —— 桌面端「点一下没反应然后静默退出」
		 * 基本都是从这里溜走的。记一笔再放行（Handled 保持 false，不改变原有崩溃行为）。
		 */
		this.UnhandledException += (_, e) =>
		{
			KidsSafeDesktop.Helpers.AppLog.E("崩溃", "WinUI 界面线程未处理异常", e.Exception);
		};

		KidsSafeDesktop.Helpers.AppLog.I("启动", "WinUI Application 构造完成（PC）");
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

