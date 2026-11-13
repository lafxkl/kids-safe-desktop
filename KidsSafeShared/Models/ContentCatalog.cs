namespace KidsSafeDesktop.Models;

/// <summary>内容大类：音频（宝宝听）/ 视频（宝宝看）/ 图文（亲子阅）。</summary>
public enum MediaKind
{
    Audio,
    Video,
    Article
}

/// <summary>内容来源：随包离线 / 家长服务器在线。</summary>
public enum ContentItemSource
{
    Offline,
    Online
}

/// <summary>
/// 统一的儿童内容条目。覆盖「宝宝听 / 宝宝看 / 亲子阅」三类。
/// 设计原则：所有在线内容只可能来自家长自建的 KidsSafeServer（经人工审核），
/// 绝不直接引用任何第三方儿童站，避免登录墙、VIP 弹窗与成人向推荐内容。
/// </summary>
public class ContentItem
{
    public string Id { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎵";
    public string Desc { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF8A65";
    public string Hint { get; set; } = string.Empty;

    public MediaKind Kind { get; set; } = MediaKind.Audio;
    public ContentItemSource Source { get; set; } = ContentItemSource.Offline;

    // —— 音频（宝宝听）——
    public KidsAudioKind? AudioKind { get; set; }
    /// <summary>离线打包文件名（如 song_twinkle.wav）。</summary>
    public string? FileName { get; set; }
    /// <summary>在线音频地址（同服务端，受控）。</summary>
    public string? AudioUrl { get; set; }
    /// <summary>语音朗读逐句文本。</summary>
    public string[] Lines { get; set; } = [];
    public string Locale { get; set; } = "zh";
    public bool Loop { get; set; }

    // —— 视频（宝宝看）——
    /// <summary>在线视频地址（同服务端，受控）。</summary>
    public string? VideoUrl { get; set; }
    /// <summary>离线打包视频文件名。</summary>
    public string? VideoFileName { get; set; }
    /// <summary>无视频文件/地址时，用 TTS 旁白呈现「视听模式」。</summary>
    public string[] NarrationLines { get; set; } = [];

    // —— 图文（亲子阅）——
    public string[] Body { get; set; } = [];
    public string[] Tags { get; set; } = [];
}

/// <summary>内容分组（专辑 / 栏目）。</summary>
public class ContentGroup
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎵";
    public string Color { get; set; } = "#FF8A65";
    public string Desc { get; set; } = string.Empty;
    public MediaKind Kind { get; set; } = MediaKind.Audio;
    public List<ContentItem> Items { get; set; } = [];
}
