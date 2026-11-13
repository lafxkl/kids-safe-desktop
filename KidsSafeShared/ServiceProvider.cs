using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;

namespace KidsSafeDesktop;

/// <summary>
/// 便捷访问 DI 中注册的平台服务。
/// </summary>
public static class ServiceProvider
{
    public static TService Get<TService>() where TService : class
    {
        var sp = IPlatformApplication.Current!.Services;
        return (TService)sp.GetRequiredService(typeof(TService))!;
    }

    public static TService? GetOrDefault<TService>() where TService : class
    {
        var sp = IPlatformApplication.Current?.Services;
        return sp is null ? null : (TService?)sp.GetService(typeof(TService));
    }
}
