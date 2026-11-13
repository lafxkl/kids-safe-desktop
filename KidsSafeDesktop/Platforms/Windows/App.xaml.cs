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
		this.InitializeComponent();

		// 兜底：任务栏隐藏属于「修改 explorer.exe 全局窗口状态」，进程一旦崩溃/被强杀
		// 不会自动回滚。这里做两件事：
		//   1) 启动时若发现上次会话的隐藏标记残留 → 立即恢复（自救，用户无需重启资源管理器）
		//   2) 注册 ProcessExit / 未处理异常钩子 → 任何退出路径都尝试恢复
		try
		{
			KidsSafeDesktop.Platforms.Windows.WindowsKioskService.RecoverTaskbarIfNeeded();
			KidsSafeDesktop.Platforms.Windows.WindowsKioskService.RegisterExitHandlers();
		}
		catch { /* 兜底逻辑本身绝不能影响启动 */ }
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

