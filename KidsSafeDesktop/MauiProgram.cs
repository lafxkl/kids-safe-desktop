using KidsSafeDesktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using System;

namespace KidsSafeDesktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
#if ANDROID
		// 必须是第一句：在任何 KidsSafeShared 类型被触碰之前，把增量补丁预加载进来。
		// 一旦前面先加载了 APK 里那份 dll，覆盖就再也换不掉了（详见 PatchBootstrapper 注释）。
		PatchBootstrapper.Run();
#endif

		// 运行日志必须最先挂上：后面任何一步炸了，都得能在 runtime.log 里看到。
		// 发布版同样开启（不加 #if DEBUG）—— 用户设备上出问题，这是他唯一能发给我们的现场。
		KidsSafeDesktop.Helpers.AppLog.Install();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// 共享服务
		builder.Services.AddSingleton<ISettingsService, SettingsService>();
		builder.Services.AddSingleton<IScreenTimeService, ScreenTimeService>();
		builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
		builder.Services.AddSingleton<IWeatherService, WeatherService>();
		builder.Services.AddSingleton<IServerSyncService, ServerSyncService>();
		// 实时下发通道（长轮询）：家长改配置，孩子设备秒级生效，不依赖厂商推送
		builder.Services.AddSingleton<ILiveSyncService, LiveSyncService>();
		builder.Services.AddSingleton<IContentService, ContentService>();
		builder.Services.AddSingleton<IStoryService, StoryService>();
		// 电子课本：目录与页图都来自家长服务器，页图按页缓存到本地
		builder.Services.AddSingleton<ITextbookService, TextbookService>();
		builder.Services.AddSingleton<IAccountService, AccountService>();
		builder.Services.AddSingleton<IEyeCareService, EyeCareService>();
		// 增量更新：只下变化的 dll，Windows 关机时落盘、Android 下次启动预加载。
		// iOS 只发 App Store：审核指南 2.5.2 不允许运行期下发可执行代码，
		// 所以这里换成空实现 —— 界面照旧，只是「检查更新」永远是最新。
#if IOS
		builder.Services.AddSingleton<KidsSafeDesktop.Updates.IUpdateService,
									  KidsSafeDesktop.Services.NoopUpdateService>();
#else
		builder.Services.AddSingleton<KidsSafeDesktop.Updates.IUpdateService,
									  KidsSafeDesktop.Updates.UpdateService>();
#endif
		// 用户反馈：意见反馈 / 满意度 / 问卷，统一上报官网（断网先存本机，联网自动补传）
		builder.Services.AddSingleton<IFeedbackService, FeedbackService>();

		// 平台服务
#if ANDROID
		builder.Services.AddSingleton<IAppCatalogService, Platforms.Android.AndroidAppCatalogService>();
		builder.Services.AddSingleton<IAppLauncherService, Platforms.Android.AndroidAppLauncherService>();
		builder.Services.AddSingleton<IKioskService, Platforms.Android.AndroidKioskService>();
		builder.Services.AddSingleton<ISoundService, Platforms.Android.AndroidSoundService>();
		builder.Services.AddSingleton<IAudioPlayerService, Platforms.Android.AndroidAudioPlayerService>();
		builder.Services.AddSingleton<IXimalayaService, Platforms.Android.XimalayaService>();
#elif WINDOWS
		builder.Services.AddSingleton<IAppCatalogService, Platforms.Windows.WindowsAppCatalogService>();
		builder.Services.AddSingleton<IAppLauncherService, Platforms.Windows.WindowsAppLauncherService>();
		builder.Services.AddSingleton<IKioskService, Platforms.Windows.WindowsKioskService>();
		builder.Services.AddSingleton<ISoundService, Platforms.Windows.WindowsSoundService>();
		builder.Services.AddSingleton<IAudioPlayerService, Platforms.Windows.WindowsAudioPlayerService>();
		builder.Services.AddSingleton<IXimalayaService, Platforms.Windows.XimalayaService>();
#elif IOS
		// iOS 是「内容版」：应用白名单、桌面替换、锁机、第三方音频站这些依赖
		// 系统级权限/私有能力的部分，iOS 不开放（也不允许），保持桩实现，
		// 对应的入口由界面按 IAppCatalogService 返回空列表自动隐藏。
		// 音频播放与音效是内容版的核心体验，必须用真实实现（不再是静音占位）。
		builder.Services.AddSingleton<IAppCatalogService, StubAppCatalogService>();
		builder.Services.AddSingleton<IAppLauncherService, StubAppLauncherService>();
		builder.Services.AddSingleton<IKioskService, StubKioskService>();
		builder.Services.AddSingleton<ISoundService, Platforms.iOS.IosSoundService>();
		builder.Services.AddSingleton<IAudioPlayerService, Platforms.iOS.IosAudioPlayerService>();
		builder.Services.AddSingleton<IXimalayaService, StubXimalayaService>();
#elif MACCATALYST
		builder.Services.AddSingleton<IAppCatalogService, StubAppCatalogService>();
		builder.Services.AddSingleton<IAppLauncherService, StubAppLauncherService>();
		builder.Services.AddSingleton<IKioskService, StubKioskService>();
		builder.Services.AddSingleton<ISoundService, StubSoundService>();
		builder.Services.AddSingleton<IXimalayaService, StubXimalayaService>();
		builder.Services.AddSingleton<IAudioPlayerService, StubAudioPlayerService>();
#endif

		var app = builder.Build();

		// 全局未处理异常记录：上架前排查闪退用（不阻断应用既有行为）
		// AppLog.Install() 里已经挂了同样一件事（并会同时写 crash.log），这里保留是为了
		// 兼容历史上就看 crash.log 的习惯，两处都写不冲突。
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			try
			{
				KidsSafeDesktop.Helpers.DiagLog.Append("crash.log", e.ExceptionObject?.ToString() ?? "(null)");
			}
			catch { /* 忽略记录失败 */ }
		};

		KidsSafeDesktop.Helpers.AppLog.I("启动", "服务注册完成，MauiApp 已构建");

		return app;
	}
}
