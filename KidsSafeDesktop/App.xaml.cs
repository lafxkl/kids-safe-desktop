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

		// 增量更新：应用关闭时把已经下载好的补丁交给落盘器（它等我们退出后才替换 dll）。
		// 放在这里而不是 OnSleep：OnSleep 在最小化/切后台时也会触发，那不是「关掉」。
		window.Destroying += (_, _) =>
		{
			try
			{
				var update = ServiceProvider.Get<KidsSafeDesktop.Updates.IUpdateService>();
				if (update.HasPendingPatch && update.State == KidsSafeDesktop.Updates.UpdateState.Ready)
				{
					_ = update.ApplyAsync(restart: false);
				}
			}
			catch (Exception ex)
			{
				Helpers.AppLog.W("增量更新", "退出时启动落盘器失败", ex);
			}
		};

		return window;
	}

	// 初始化已迁移至 SplashPage.InitializeAsync()，此处仅保留生命周期转发。
	protected override void OnStart()
	{
		base.OnStart();
		Helpers.AppLog.I("生命周期", "OnStart（应用已在前台）");

#if ANDROID
		// 能走到这里说明补丁没有把启动搞崩，把「连续失败计数」归零。
		PatchBootstrapper.MarkHealthy();
#endif
	}

	protected override void OnResume()
	{
		base.OnResume();
		Helpers.AppLog.I("生命周期", "OnResume（从后台回到前台）");
		// 回到前台时按当前时间重算（定时护眼可能跨入/跨出生效时段）
		KidsSafeDesktop.Helpers.EyeCare.Refresh();
		ServiceProvider.Get<IScreenTimeService>().Start();
	}

	protected override void OnSleep()
	{
		Helpers.AppLog.I("生命周期", "OnSleep（进入后台）");
		base.OnSleep();
		ServiceProvider.Get<IScreenTimeService>().Stop();
	}
}
