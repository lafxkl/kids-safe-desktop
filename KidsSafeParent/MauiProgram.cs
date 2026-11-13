using KidsSafeDesktop.Services;
using Microsoft.Extensions.Logging;

namespace KidsSafeParent;

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

        // ── 与孩子端共用的业务服务（实现都在 KidsSafeShared） ──────────────
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IScreenTimeService, ScreenTimeService>();
        builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
        builder.Services.AddSingleton<IWeatherService, WeatherService>();
        builder.Services.AddSingleton<IServerSyncService, ServerSyncService>();
        builder.Services.AddSingleton<ILiveSyncService, LiveSyncService>();
        builder.Services.AddSingleton<IContentService, ContentService>();
        builder.Services.AddSingleton<IStoryService, StoryService>();
        builder.Services.AddSingleton<ITextbookService, TextbookService>();
        builder.Services.AddSingleton<IAccountService, AccountService>();
        builder.Services.AddSingleton<IEyeCareService, EyeCareService>();

        // ── 家长端用不到的设备能力，注册空实现 ────────────────────────────
        // 家长端不枚举孩子设备上装了哪些应用、不启动外部应用、不锁定屏幕、
        // 不播放音频、不打开喜马拉雅 —— 这些是孩子端专属，注册桩实现即可，
        // 避免把 500 多行 MainActivity / 荣耀推送 / AAR 一并搬进家长端。
        builder.Services.AddSingleton<IAppCatalogService, StubAppCatalogService>();
        builder.Services.AddSingleton<IAppLauncherService, StubAppLauncherService>();
        builder.Services.AddSingleton<IKioskService, StubKioskService>();
        builder.Services.AddSingleton<ISoundService, StubSoundService>();
        builder.Services.AddSingleton<IAudioPlayerService, StubAudioPlayerService>();
        builder.Services.AddSingleton<IXimalayaService, StubXimalayaService>();

        var app = builder.Build();

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
