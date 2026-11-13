namespace KidsSafeDesktop.Models;

/// <summary>
/// 家长端服务器下发的托管配置（字段与服务端 ManagedProfile 一致，便于直接 JSON 映射）。
/// 儿童端拉取后合并到本地 ChildSettings。
/// </summary>
public class RemoteProfile
{
    public int DailyLimitMinutes { get; set; } = 60;
    public bool GamesEnabled { get; set; } = true;
    public List<string> EnabledGames { get; set; } = new();
    public string ThemeId { get; set; } = "sunshine";
    public string Nickname { get; set; } = "小朋友";
    public string WeatherCity { get; set; } = "auto";
    public string XimalayaDefaultCategory { get; set; } = "story";

    /// <summary>
    /// 家长在服务器后台打开的「在线内容」开关：开启后宝宝听 / 宝宝看 / 亲子阅 /
    /// 故事屋 / 电子课本会从该服务器拉取补充内容，关闭则只用离线内置内容。
    /// </summary>
    public bool OnlineContentEnabled { get; set; }

    public List<RemoteApp> AllowedApps { get; set; } = new();
}

/// <summary>服务端定义的一个允许应用。</summary>
public class RemoteApp
{
    public string Name { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string? LaunchTarget { get; set; }
    public string? Emoji { get; set; }
    public string? AccentColor { get; set; }
}
