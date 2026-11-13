using KidsSafeDesktop.Pages;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// 合规闸门：未同意隐私政策时，首屏是同意页而不是启动页。
		// 同意之前不读取设置、不申请推送 token、不连接服务器、不进入 Kiosk —— 这些都在 SplashPage 里。
		MainPage = PrivacyConsent.HasAgreed
			? new SplashPage()
			: new NavigationPage(new PrivacyConsentPage());
	}

	/// <summary>窗口创建后挂载护眼遮罩（窗口 Handler 就绪时自动生效）。</summary>
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);
		KidsSafeDesktop.Helpers.EyeCare.Attach(window);
		return window;
	}

	// 初始化已迁移至 SplashPage.InitializeAsync()，此处仅保留生命周期转发。
	protected override void OnStart()
	{
		base.OnStart();
	}

	protected override void OnResume()
	{
		base.OnResume();
		// 回到前台时按当前时间重算（定时护眼可能跨入/跨出生效时段）
		KidsSafeDesktop.Helpers.EyeCare.Refresh();
		ServiceProvider.Get<IScreenTimeService>().Start();
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		ServiceProvider.Get<IScreenTimeService>().Stop();
	}
}
