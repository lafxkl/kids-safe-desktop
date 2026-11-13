using System.Text.Json;

namespace KidsSafeServer;

/// <summary>
/// 服务端托管的儿童桌面配置（家长在网页后台编辑，儿童端拉取后应用）。
/// 与儿童端 ChildSettings 的「可远程管理」字段一一对应。
/// </summary>
public class ManagedProfile
{
    /// <summary>每日可用时长（分钟）</summary>
    public int DailyLimitMinutes { get; set; } = 60;

    /// <summary>是否允许小游戏（总开关）</summary>
    public bool GamesEnabled { get; set; } = true;

    /// <summary>开启的游戏类型名（GameType 的 ToString）；为空表示全部允许。</summary>
    public List<string> EnabledGames { get; set; } = new();

    /// <summary>桌面主题 Id</summary>
    public string ThemeId { get; set; } = "sunshine";

    /// <summary>小朋友昵称</summary>
    public string Nickname { get; set; } = "小朋友";

    /// <summary>天气城市（"auto" 表示自动定位）</summary>
    public string WeatherCity { get; set; } = "auto";

    /// <summary>喜马拉雅儿童默认分类 Key</summary>
    public string XimalayaDefaultCategory { get; set; } = "story";

    /// <summary>
    /// 是否允许儿童端使用「在线内容」：宝宝听 / 宝宝看 / 亲子阅 / 故事屋 / 电子课本
    /// 会从本服务器拉取经人工审核的补充内容。关闭时儿童端只用离线内置内容。
    /// 默认关闭，由家长在后台显式打开。
    /// </summary>
    public bool OnlineContentEnabled { get; set; } = false;

    /// <summary>允许在儿童桌面显示的应用白名单</summary>
    public List<ServerApp> AllowedApps { get; set; } = new();

    /// <summary>最后更新时间（UTC）</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>服务端定义的一个允许应用（名称 + 包名/启动目标 + 展示样式）。</summary>
public class ServerApp
{
    public string Name { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string? LaunchTarget { get; set; }
    public string? Emoji { get; set; }
    public string? AccentColor { get; set; }
}

/// <summary>
/// 配置存储：每个账号一份 JSON。
///
/// 单账号时代只有 data/profile.json 一个文件；加了账号体系后，正式账号存在
/// <c>data/profiles/{accountId}.json</c>，而 <c>profile.json</c> 留给「还没注册账号」
/// 的老密码模式（虚拟账号 default）。第一个账号注册时会把老文件复制过去，
/// 原文件保留不动 —— 万一家长又想退回老模式，配置还在。
/// </summary>
public class ProfileStore
{
    private readonly string _legacyPath;
    private readonly string _accountDir;
    private readonly object _lock = new();

    public ProfileStore(string legacyPath, string accountDir)
    {
        _legacyPath = legacyPath;
        _accountDir = accountDir;
        var dir = Path.GetDirectoryName(_legacyPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
        Directory.CreateDirectory(_accountDir);
    }

    private string PathFor(string? accountId) =>
        string.IsNullOrEmpty(accountId) || accountId == "default"
            ? _legacyPath
            : Path.Combine(_accountDir, accountId + ".json");

    public ManagedProfile Load(string? accountId = "default")
    {
        var path = PathFor(accountId);
        lock (_lock)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var p = JsonSerializer.Deserialize<ManagedProfile>(json);
                    if (p is not null) return p;
                }
                catch { /* 损坏则用默认 */ }
            }
            var seed = Seed();
            Save(seed, accountId);
            return seed;
        }
    }

    public void Save(ManagedProfile profile, string? accountId = "default")
    {
        var path = PathFor(accountId);
        lock (_lock)
        {
            profile.UpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
            File.WriteAllText(path, json);
        }
    }

    /// <summary>把老的单账号配置复制给新账号（首个账号注册时调用）。</summary>
    public bool AdoptLegacy(string accountId)
    {
        lock (_lock)
        {
            if (!File.Exists(_legacyPath)) return false;
            var target = PathFor(accountId);
            try
            {
                File.Copy(_legacyPath, target, overwrite: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>首次运行给一份合理默认配置。</summary>
    private static ManagedProfile Seed() => new()
    {
        DailyLimitMinutes = 60,
        GamesEnabled = true,
        EnabledGames = new List<string>(),
        ThemeId = "sunshine",
        Nickname = "小朋友",
        WeatherCity = "auto",
        XimalayaDefaultCategory = "story",
        AllowedApps = new List<ServerApp>
        {
            new() { Name = "计算器", LaunchTarget = "calc.exe", Emoji = "🧮", AccentColor = "#2E7D32" },
            new() { Name = "画图", LaunchTarget = "mspaint.exe", Emoji = "🎨", AccentColor = "#AD1457" },
            new() { Name = "记事本", LaunchTarget = "notepad.exe", Emoji = "📝", AccentColor = "#1565C0" },
        }
    };
}
