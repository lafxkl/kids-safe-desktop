using System.Linq;
using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Networking;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 天气服务实现：
/// 1) 定位：city 为 "auto"/空 → 用 ipwho.is 做 IP 定位；否则用 Open-Meteo 地理编码按城市名解析。
/// 2) 天气：用 Open-Meteo 当前实况 + 逐小时接口（无需密钥）。
/// 3) 预警：Open-Meteo 免费接口不含官方预警，这里依据逐小时预报数据自动推导（高温/低温/降雨/大风/雾/雷暴）。
/// 所有外部请求都设超时并吞掉异常，失败时返回 null，由调用方降级处理。
/// </summary>
public class WeatherService : IWeatherService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<WeatherInfo?> GetCurrentAsync(string? city = "auto")
    {
        if (!HasNetwork()) return null;
        try
        {
            var (lat, lon, cityName) = await ResolveLocationAsync(city);
            if (lat is null || lon is null)
                return null;

            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.Value:0.000}&longitude={lon.Value:0.000}" +
                      $"&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&timezone=auto";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            if (!root.TryGetProperty("current", out var cur))
                return null;

            return ParseCurrent(cur, cityName);
        }
        catch
        {
            return null;
        }
    }

    public async Task<WeatherForecast?> GetForecastAsync(string? city = "auto")
    {
        if (!HasNetwork()) return null;
        try
        {
            var (lat, lon, cityName) = await ResolveLocationAsync(city);
            if (lat is null || lon is null)
                return null;

            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.Value:0.000}&longitude={lon.Value:0.000}" +
                      $"&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m" +
                      $"&hourly=temperature_2m,weather_code,precipitation_probability" +
                      $"&forecast_days=1&timezone=auto";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            if (!root.TryGetProperty("current", out var cur))
                return null;

            var forecast = new WeatherForecast
            {
                City = cityName,
                UpdatedAt = DateTime.Now
            };
            // 填充当前实况
            forecast.TempC = cur.TryGetProperty("temperature_2m", out var t) ? t.GetDouble() : 0;
            forecast.Humidity = cur.TryGetProperty("relative_humidity_2m", out var h) ? h.GetInt32() : 0;
            forecast.WindKmh = cur.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : 0;
            forecast.Code = cur.TryGetProperty("weather_code", out var c) ? c.GetInt32() : 0;
            forecast.Description = MapDescription(forecast.Code);
            forecast.Emoji = MapEmoji(forecast.Code);

            // 逐小时
            if (root.TryGetProperty("hourly", out var hourly))
            {
                var hours = BuildHours(hourly);
                forecast.Hours = hours;
                forecast.Alerts = DeriveAlerts(hours, forecast.WindKmh);
            }

            return forecast;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 离线时直接跳过所有外部请求（避免无意义的 8 秒超时与可能的未处理异常）。
    /// 仅在确认具备 Internet 访问时才发起网络调用。
    /// </summary>
    private static bool HasNetwork()
    {
        try
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch
        {
            // 取不到网络状态时保守地允许尝试
            return true;
        }
    }

    private static WeatherInfo ParseCurrent(JsonElement cur, string cityName)
    {
        double temp = cur.TryGetProperty("temperature_2m", out var t) ? t.GetDouble() : 0;
        int humidity = cur.TryGetProperty("relative_humidity_2m", out var h) ? h.GetInt32() : 0;
        int code = cur.TryGetProperty("weather_code", out var c) ? c.GetInt32() : 0;
        double wind = cur.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : 0;

        return new WeatherInfo
        {
            TempC = temp,
            Humidity = humidity,
            WindKmh = wind,
            Code = code,
            Description = MapDescription(code),
            Emoji = MapEmoji(code),
            City = cityName,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>把 hourly 数组解析为未来若干小时的列表（从当前小时起，最多取 12 小时）。</summary>
    private static List<WeatherHour> BuildHours(JsonElement hourly)
    {
        if (!hourly.TryGetProperty("time", out var times) ||
            times.ValueKind != JsonValueKind.Array)
            return new List<WeatherHour>();

        var temps = GetArray(hourly, "temperature_2m");
        var codes = GetArray(hourly, "weather_code");
        var precs = GetArray(hourly, "precipitation_probability");

        int n = times.GetArrayLength();
        var now = DateTime.Now;
        var nowHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        int start = 0;
        for (int i = 0; i < n; i++)
        {
            if (DateTime.TryParse(times[i].GetString(), out var dt) && dt >= nowHour)
            {
                start = i;
                break;
            }
            if (i == n - 1) start = 0;
        }

        var list = new List<WeatherHour>();
        int take = Math.Min(12, n - start);
        for (int i = 0; i < take; i++)
        {
            int idx = start + i;
            if (!DateTime.TryParse(times[idx].GetString(), out var dt)) continue;
            int code = idx < codes.Count ? (int)Math.Round(codes[idx]) : 0;
            double temp = idx < temps.Count ? temps[idx] : 0;
            int prec = idx < precs.Count ? (int)Math.Round(precs[idx]) : 0;
            list.Add(new WeatherHour
            {
                Time = dt,
                TimeLabel = i == 0 ? "现在" : dt.ToString("HH:00"),
                TempC = temp,
                Code = code,
                Emoji = MapEmoji(code),
                Desc = MapDescription(code),
                PrecipProb = prec
            });
        }
        return list;
    }

    private static List<double> GetArray(JsonElement hourly, string name)
    {
        if (hourly.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return arr.EnumerateArray().Select(x => x.GetDouble()).ToList();
        return new List<double>();
    }

    /// <summary>依据未来数小时数据自动推导预警/提醒（Open-Meteo 免费接口不含官方预警，故本地推导）。</summary>
    private static List<WeatherAlert> DeriveAlerts(List<WeatherHour> hours, double currentWind)
    {
        var alerts = new List<WeatherAlert>();
        if (hours.Count == 0) return alerts;

        double maxT = hours.Max(h => h.TempC);
        double minT = hours.Min(h => h.TempC);
        int maxP = hours.Max(h => h.PrecipProb);
        bool storm = hours.Any(h => h.Code is 95 or 96 or 99);
        bool fog = hours.Any(h => h.Code is 45 or 48);

        if (maxT >= 35)
        {
            bool severe = maxT >= 38;
            alerts.Add(new WeatherAlert
            {
                Level = severe ? "预警" : "注意",
                Emoji = "🔥",
                Title = severe ? "高温预警" : "高温提醒",
                Desc = $"今日最高约 {maxT:0}°，注意防暑补水"
            });
        }

        if (minT <= 0)
        {
            alerts.Add(new WeatherAlert
            {
                Level = "预警",
                Emoji = "🥶",
                Title = "低温结冰预警",
                Desc = $"最低约 {minT:0}°，注意保暖防滑"
            });
        }

        if (maxP >= 60)
        {
            alerts.Add(new WeatherAlert
            {
                Level = "注意",
                Emoji = "🌧️",
                Title = "降雨提醒",
                Desc = $"降水概率最高 {maxP}%，记得带伞 🌂"
            });
        }

        if (currentWind >= 30)
        {
            bool severe = currentWind >= 40;
            alerts.Add(new WeatherAlert
            {
                Level = severe ? "预警" : "注意",
                Emoji = "💨",
                Title = severe ? "大风预警" : "大风提醒",
                Desc = $"当前风速约 {currentWind:0} km/h"
            });
        }

        if (fog)
            alerts.Add(new WeatherAlert
            {
                Level = "注意",
                Emoji = "🌫️",
                Title = "大雾提醒",
                Desc = "能见度较低，注意交通安全"
            });

        if (storm)
            alerts.Add(new WeatherAlert
            {
                Level = "预警",
                Emoji = "⛈️",
                Title = "雷阵雨预警",
                Desc = "可能有雷阵雨，减少户外活动"
            });

        return alerts;
    }

    private async Task<(double? lat, double? lon, string city)> ResolveLocationAsync(string? city)
    {
        // 指定城市：用 Open-Meteo 地理编码（支持中文，返回中文名）
        if (!string.IsNullOrWhiteSpace(city) && city!.Trim() != "auto")
        {
            try
            {
                var q = Uri.EscapeDataString(city.Trim());
                var geo = $"https://geocoding-api.open-meteo.com/v1/search?name={q}&count=1&language=zh&format=json";
                using var resp = await _http.GetAsync(geo);
                if (resp.IsSuccessStatusCode)
                {
                    await using var s = await resp.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(s);
                    if (doc.RootElement.TryGetProperty("results", out var results) &&
                        results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
                    {
                        var r0 = results[0];
                        var lat = r0.GetProperty("latitude").GetDouble();
                        var lon = r0.GetProperty("longitude").GetDouble();
                        var name = r0.TryGetProperty("name", out var n) ? n.GetString() ?? city! : city!;
                        return (lat, lon, name);
                    }
                }
            }
            catch { /* 回落到 IP 定位 */ }
        }

        // 自动：IP 定位
        try
        {
            using var resp = await _http.GetAsync("https://ipwho.is/");
            if (resp.IsSuccessStatusCode)
            {
                await using var s = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(s);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var ok) && ok.GetBoolean() &&
                    root.TryGetProperty("latitude", out var lat) &&
                    root.TryGetProperty("longitude", out var lon))
                {
                    var cityName = root.TryGetProperty("city", out var c) ? c.GetString() ?? "这里" : "这里";
                    return (lat.GetDouble(), lon.GetDouble(), cityName);
                }
            }
        }
        catch { /* 忽略 */ }

        // 兜底：北京
        return (39.9042, 116.4074, "北京");
    }

    public static string MapEmoji(int code) => code switch
    {
        0 => "☀️",
        1 => "🌤️",
        2 => "⛅",
        3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 => "🌦️",
        56 or 57 => "🌧️",
        61 or 63 or 65 => "🌧️",
        66 or 67 => "🌧️",
        71 or 73 or 75 => "❄️",
        77 => "🌨️",
        80 or 81 or 82 => "🌦️",
        85 or 86 => "🌨️",
        95 => "⛈️",
        96 or 99 => "⛈️",
        _ => "🌡️"
    };

    public static string MapDescription(int code) => code switch
    {
        0 => "晴",
        1 => "晴间多云",
        2 => "多云",
        3 => "阴",
        45 or 48 => "有雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻雨",
        61 or 63 or 65 => "下雨",
        66 or 67 => "冻雨",
        71 or 73 or 75 => "下雪",
        77 => "小雪粒",
        80 or 81 or 82 => "阵雨",
        85 or 86 => "阵雪",
        95 => "雷阵雨",
        96 or 99 => "雷阵雨伴冰雹",
        _ => "未知"
    };
}
