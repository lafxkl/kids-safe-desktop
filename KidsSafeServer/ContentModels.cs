namespace KidsSafeServer;

/// <summary>
/// 「在线内容」数据模型。所有内容均由运营/家长在受控服务端人工审核后发布，
/// 绝不引用任何第三方儿童站，避免登录墙、VIP 弹窗与成人向推荐内容混入儿童模式。
/// </summary>
public class ContentItemDto
{
    public string Id { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎵";
    public string Desc { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF8A65";
    public string Hint { get; set; } = string.Empty;

    /// <summary>Audio / Video / Article</summary>
    public string Kind { get; set; } = "Audio";

    public string Source { get; set; } = "online";

    // —— 音频（宝宝听）——
    public string? AudioKind { get; set; }     // Music / Ambient / Speech
    public string? FileName { get; set; }       // 离线打包文件名（服务端通常留空，走在线 URL）
    public string? AudioUrl { get; set; }       // 在线音频地址（同服务端，受控）
    public string[]? Lines { get; set; }        // TTS 逐句文本
    public string Locale { get; set; } = "zh";
    public bool Loop { get; set; }

    // —— 视频（宝宝看）——
    public string? VideoUrl { get; set; }        // 在线视频地址（同服务端，受控）
    public string? VideoFileName { get; set; }   // 离线打包视频文件名
    public string[]? NarrationLines { get; set; } // 无视频时的旁白文本（TTS 视听模式）

    // —— 图文（亲子阅）——
    public string[]? Body { get; set; }          // 正文段落
    public string[]? Tags { get; set; }          // 标签
}

public class ContentGroupDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎵";
    public string Color { get; set; } = "#FF8A65";
    public string Desc { get; set; } = string.Empty;
    public string Kind { get; set; } = "Audio";
    public List<ContentItemDto> Items { get; set; } = new();
}

/// <summary>受控、人工审核的儿童内容种子。仅作示例，真实环境可由后台编辑后下发。</summary>
public static class ContentSeed
{
    public static List<ContentGroupDto> Audio()
    {
        return new()
        {
            new ContentGroupDto
            {
                Key = "online_song",
                Name = "在线儿歌精选",
                Emoji = "🌟",
                Color = "#26A69A",
                Desc = "家长服务器推荐的经典儿歌",
                Kind = "Audio",
                Items =
                {
                    new ContentItemDto
                    {
                        Id = "o_twinkle_twice",
                        GroupKey = "online_song",
                        Title = "小星星（慢速版）",
                        Emoji = "⭐",
                        Desc = "跟着轻轻唱",
                        Color = "#26A69A",
                        Kind = "Audio",
                        AudioKind = "Music",
                        AudioUrl = "/media/audio/song_twinkle_slow.mp3",
                        Locale = "zh"
                    },
                    new ContentItemDto
                    {
                        Id = "o_bus",
                        GroupKey = "online_song",
                        Title = "声音的故事：公共汽车",
                        Emoji = "🚌",
                        Desc = "听一听城市里的声音",
                        Color = "#26A69A",
                        Kind = "Audio",
                        AudioKind = "Speech",
                        Lines = new[]
                        {
                            "公共汽车。",
                            "早上，公共汽车嘟嘟地开过来。",
                            "叮咚，上车请投币。",
                            "爷爷奶奶坐下来，小朋友扶好扶手。",
                            "到了站，车门打开，大家有礼貌地走下车。",
                        },
                        Locale = "zh"
                    }
                }
            }
        };
    }

    public static List<ContentGroupDto> Video()
    {
        return new()
        {
            new ContentGroupDto
            {
                Key = "online_watch",
                Name = "在线视听故事",
                Emoji = "📺",
                Color = "#42A5F5",
                Desc = "边看边听的小故事",
                Kind = "Video",
                Items =
                {
                    new ContentItemDto
                    {
                        Id = "o_animal",
                        GroupKey = "online_watch",
                        Title = "动物宝宝的一天",
                        Emoji = "🐘",
                        Desc = "看看小动物怎么生活",
                        Color = "#42A5F5",
                        Kind = "Video",
                        // 若服务端有视频则填 VideoUrl；否则用旁白做「视听模式」
                        NarrationLines = new[]
                        {
                            "太阳升起来了，动物宝宝们醒来啦。",
                            "小象用长鼻子吸水洗澡，喷得满脸都是水珠。",
                            "小兔子在草地上蹦蹦跳，找最嫩的叶子吃。",
                            "天黑了，宝宝们靠在一起，安安静静地睡着了。",
                        }
                    },
                    new ContentItemDto
                    {
                        Id = "o_color",
                        GroupKey = "online_watch",
                        Title = "认识颜色",
                        Emoji = "🌈",
                        Desc = "红黄蓝绿真好看",
                        Color = "#42A5F5",
                        Kind = "Video",
                        NarrationLines = new[]
                        {
                            "红色，像熟透的苹果。",
                            "黄色，像温暖的太阳。",
                            "蓝色，像晴朗的天空。",
                            "绿色，像春天的小草。",
                            "把颜色排成一排，就成了一道美丽的彩虹。",
                        }
                    }
                }
            }
        };
    }

    public static List<ContentGroupDto> Articles()
    {
        return new()
        {
            new ContentGroupDto
            {
                Key = "online_read",
                Name = "给家长的育儿经",
                Emoji = "📖",
                Color = "#66BB6A",
                Desc = "陪伴孩子更好成长",
                Kind = "Article",
                Items =
                {
                    new ContentItemDto
                    {
                        Id = "o_screen",
                        GroupKey = "online_read",
                        Title = "如何安排屏幕时间",
                        Emoji = "⏰",
                        Desc = "保护视力从小做起",
                        Color = "#66BB6A",
                        Kind = "Article",
                        Tags = new[] { "护眼", "习惯" },
                        Body = new[]
                        {
                            "3–6 岁的小朋友，每天看屏幕的时间建议不超过 30 分钟。",
                            "把设备放在客厅等公共区域，而不是孩子独自待在房间。",
                            "看完屏幕后，带孩子到窗边远眺 5 分钟，让眼睛放松。",
                            "用「听故事」「做手工」替代部分屏幕时间，效果更好。",
                        }
                    },
                    new ContentItemDto
                    {
                        Id = "o_read_together",
                        GroupKey = "online_read",
                        Title = "亲子共读的小技巧",
                        Emoji = "🤝",
                        Desc = "让读书变成亲密时光",
                        Color = "#66BB6A",
                        Kind = "Article",
                        Tags = new[] { "阅读", "陪伴" },
                        Body = new[]
                        {
                            "每天固定一个安静的共读时间，比读多久更重要。",
                            "读书时多用表情和声音，孩子会更投入。",
                            "读到熟悉的地方，可以停下来让孩子猜一猜接下来发生什么。",
                            "读完问一个轻松的小问题，聊聊感受就好，不要变成考试。",
                        }
                    }
                }
            }
        };
    }
}
