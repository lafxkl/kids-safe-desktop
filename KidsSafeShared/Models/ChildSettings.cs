using System.Text.Json.Serialization;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 家长配置：密码、每日时长、允许的应用与网站。会持久化到安全存储。
/// </summary>
public class ChildSettings
{
    /// <summary>家长密码的 SHA256 哈希（未设置则为空）</summary>
    public string PinHash { get; set; } = string.Empty;

    /// <summary>每日可用时长（分钟）</summary>
    public int DailyLimitMinutes { get; set; } = 60;

    /// <summary>允许在儿童桌面显示的应用</summary>
    public List<AppItem> AllowedApps { get; set; } = new();

    /// <summary>允许的网站（白名单，可选功能）</summary>
    public List<string> AllowedSites { get; set; } = new();

    /// <summary>是否允许小游戏（总开关）。</summary>
    public bool GamesEnabled { get; set; } = true;

    /// <summary>
    /// 允许开启的具体游戏类型名（GameType 的 ToString）。
    /// 为空表示「全部允许」。仅在 GamesEnabled 为 true 时生效。
    /// </summary>
    public List<string> EnabledGames { get; set; } = new();

    /// <summary>当前选择的游戏难度（大厅选择后持久化，所有游戏共用）。</summary>
    public GameDifficulty Difficulty { get; set; } = GameDifficulty.Medium;

    /// <summary>本地账号唯一 Id（首次运行自动生成，仅离线账号也可能存在）。</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>小朋友的昵称（儿童桌面个性化显示，可在首页点击头像随时修改）。</summary>
    public string Nickname { get; set; } = "小朋友";

    /// <summary>专属头像 emoji（由昵称稳定生成，可在账号页修改）。</summary>
    public string AvatarEmoji { get; set; } = "🐯";

    /// <summary>当前桌面主题 Id（对应 ThemeService 中的主题）。</summary>
    public string ThemeId { get; set; } = "sunshine";

    /// <summary>
    /// 天气定位城市。"auto" 表示使用网络(IP)自动定位；否则按城市名解析。
    /// 可由家长后台或服务端统一管理。
    /// </summary>
    public string WeatherCity { get; set; } = "auto";

    /// <summary>喜马拉雅儿童默认进入的分类 Key（见媒体中心分类 Key）。</summary>
    public string XimalayaDefaultCategory { get; set; } = "story";

    /// <summary>家长端服务器地址（留空表示不启用远程管理）。</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>是否启用家长端服务器同步。</summary>
    public bool ServerEnabled { get; set; } = false;

    /// <summary>登录家长端服务器后拿到的令牌（持久化以便后续自动同步）。</summary>
    public string ServerToken { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许「在线内容」：开启后，宝宝听/宝宝看/亲子阅会从家长自建的
    /// KidsSafeServer 拉取经人工审核的补充内容。默认关闭，仅连接受控服务器、
    /// 绝不访问任何第三方站点；拉取失败时自动回退到离线内置内容。
    /// </summary>
    public bool OnlineContentEnabled { get; set; } = false;

    /// <summary>本地排行榜成绩记录。</summary>
    public List<ScoreEntry> HighScores { get; set; } = new();

    // ===== 护眼模式（全局暖色滤镜，减少蓝光） =====

    /// <summary>是否开启护眼模式（常开）。开启后始终生效。</summary>
    public bool EyeCareEnabled { get; set; } = false;

    /// <summary>护眼强度 0.0~1.0，映射为遮罩不透明度（默认 0.22，最大 0.55 以免画面过暗）。</summary>
    public double EyeCareIntensity { get; set; } = 0.22;

    /// <summary>是否开启定时自动护眼：在指定时段自动生效（无需常开）。</summary>
    public bool EyeCareAutoEnabled { get; set; } = false;

    /// <summary>自动护眼开始小时（0~23，默认 19 点）。</summary>
    public int EyeCareAutoStartHour { get; set; } = 19;

    /// <summary>自动护眼结束小时（0~23，默认 7 点，支持跨零点）。</summary>
    public int EyeCareAutoEndHour { get; set; } = 7;

    /// <summary>判断某个游戏当前是否对小朋友开放。</summary>
    public bool IsGameEnabled(GameType type)
    {
        if (!GamesEnabled) return false;
        if (EnabledGames is null || EnabledGames.Count == 0) return true;
        return EnabledGames.Contains(type.ToString());
    }
}
