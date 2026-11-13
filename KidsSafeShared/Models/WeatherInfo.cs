namespace KidsSafeDesktop.Models;

/// <summary>
/// 一段天气信息（当前实况）。由 <see cref="IWeatherService"/> 从公开天气接口获取。
/// </summary>
public class WeatherInfo
{
    /// <summary>气温（摄氏度）</summary>
    public double TempC { get; set; }

    /// <summary>相对湿度（%）</summary>
    public int Humidity { get; set; }

    /// <summary>风速（km/h）</summary>
    public double WindKmh { get; set; }

    /// <summary>WMO 天气代码（Open-Meteo 标准）</summary>
    public int Code { get; set; }

    /// <summary>天气描述（中文）</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>天气 emoji 图标</summary>
    public string Emoji { get; set; } = "🌡️";

    /// <summary>定位到的城市名（用于显示）</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>数据更新时间（本地时间）</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 逐小时天气（未来若干小时）。由 <see cref="IWeatherService.GetForecastAsync"/> 解析 Open-Meteo hourly 得到。
/// </summary>
public class WeatherHour
{
    /// <summary>该小时的时间（本地时区，来自 timezone=auto）。</summary>
    public DateTime Time { get; set; }

    /// <summary>展示用时间标签：第一个为「现在」，其余为「HH:00」。</summary>
    public string TimeLabel { get; set; } = string.Empty;

    /// <summary>气温（摄氏度）</summary>
    public double TempC { get; set; }

    /// <summary>WMO 天气代码</summary>
    public int Code { get; set; }

    /// <summary>天气 emoji 图标</summary>
    public string Emoji { get; set; } = "🌡️";

    /// <summary>天气中文描述</summary>
    public string Desc { get; set; } = string.Empty;

    /// <summary>降水概率（%）</summary>
    public int PrecipProb { get; set; }
}

/// <summary>
/// 天气预警/提醒：因 Open-Meteo 免费接口不含官方预警，这里依据逐小时预报数据自动推导
/// （高温/低温/降雨/大风/雾/雷暴），分为「预警」（较重）与「注意」两档。
/// </summary>
public class WeatherAlert
{
    /// <summary>级别：「预警」或「注意」。</summary>
    public string Level { get; set; } = "注意";

    /// <summary>图标 emoji。</summary>
    public string Emoji { get; set; } = "⚠️";

    /// <summary>预警标题，如「高温预警」。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>一句话说明。</summary>
    public string Desc { get; set; } = string.Empty;
}

/// <summary>
/// 完整天气预报：当前实况 + 逐小时 + 自动推导的预警。
/// </summary>
public class WeatherForecast
{
    /// <summary>气温（摄氏度）</summary>
    public double TempC { get; set; }

    /// <summary>相对湿度（%）</summary>
    public int Humidity { get; set; }

    /// <summary>风速（km/h）</summary>
    public double WindKmh { get; set; }

    /// <summary>WMO 天气代码</summary>
    public int Code { get; set; }

    /// <summary>天气描述（中文）</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>天气 emoji 图标</summary>
    public string Emoji { get; set; } = "🌡️";

    /// <summary>城市名</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>更新时间（本地）</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>逐小时预报。</summary>
    public List<WeatherHour> Hours { get; set; } = new();

    /// <summary>自动推导的预警/提醒。</summary>
    public List<WeatherAlert> Alerts { get; set; } = new();
}

