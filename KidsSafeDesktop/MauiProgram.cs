using KidsSafeDesktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using System;

namespace KidsSafeDesktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
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
#elif IOS || MACCATALYST
		builder.Services.AddSingleton<IAppCatalogService, StubAppCatalogService>();
		builder.Services.AddSingleton<IAppLauncherService, StubAppLauncherService>();
		builder.Services.AddSingleton<IKioskService, StubKioskService>();
		builder.Services.AddSingleton<ISoundService, StubSoundService>();
		builder.Services.AddSingleton<IXimalayaService, StubXimalayaService>();
		builder.Services.AddSingleton<IAudioPlayerService, StubAudioPlayerService>();
#endif

		var app = builder.Build();

		// 全局未处理异常记录：上架前排查闪退用（不阻断应用既有行为）
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			try
			{
				KidsSafeDesktop.Helpers.DiagLog.Append("crash.log", e.ExceptionObject?.ToString() ?? "(null)");
			}
			catch { /* 忽略记录失败 */ }
		};

		return app;
	}
}
