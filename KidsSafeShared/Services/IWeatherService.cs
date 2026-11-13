using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 天气服务：自动获取定位（IP 定位或城市名地理编码）并拉取当前实况天气。
/// 使用公开、无需密钥的接口：ipwho.is（定位）与 Open-Meteo（天气）。
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// 获取当前天气。
    /// <param name="city">城市名；为 "auto" 或空时使用网络(IP)定位；否则按城市名地理编码。</param>
    /// 失败时返回 null（调用方应优雅降级）。
    /// </summary>
    Task<WeatherInfo?> GetCurrentAsync(string? city = "auto");

    /// <summary>
    /// 获取完整天气预报：当前实况 + 未来若干小时逐小时预报 + 自动推导的预警。
    /// <param name="city">城市名；为 "auto" 或空时使用网络(IP)定位；否则按城市名地理编码。</param>
    /// 失败时返回 null（调用方应优雅降级）。
    /// </summary>
    Task<WeatherForecast?> GetForecastAsync(string? city = "auto");
}
