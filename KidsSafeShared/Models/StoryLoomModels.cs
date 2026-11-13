namespace KidsSafeDesktop.Models;

/// <summary>
/// 「无限想象」共创故事的数据模型。
///
/// 移植自 Dreamloom（网页版 index.html），保留其核心设定：
/// 孩子给一句小心愿，五个小伙伴（灵感火花 / 场景星球 / 创作向导 / 精修精灵 / 天赋瞭望）
/// 依次把它织成一个四段睡前故事，并在六个维度上留下成长轨迹。
///
/// 与原版的三点差异（都是为了适配儿童桌面这个壳）：
/// 1. 原版把数据放 localStorage，这里改成本机 Preferences + System.Text.Json，
///    与星星中心、关爱中心共用同一套「只存本机、不上传」的约定；
/// 2. 原版音色来自微软 Edge 神经语音（需要本地 Node 代理），这里改用系统 TTS，
///    零依赖、离线可用，代价是音色只有一个；
/// 3. 原版可以直连大模型，这里默认关闭；打开时必须先过家长 PIN。
/// </summary>
public sealed class LoomAgent
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Accent { get; init; } = "#7B6BFF";
}

/// <summary>可点击的灵感词条。</summary>
public sealed class LoomChip
{
    public string Group { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>今晚的灵感袋：七个维度各取一个词，外加孩子原话与心愿主题。</summary>
public sealed class LoomIdea
{
    public string? Role { get; set; }
    public string? Scene { get; set; }
    public string? Item { get; set; }
    public string? Weather { get; set; }
    public string? Sound { get; set; }
    public string? Creature { get; set; }
    public string? Emotion { get; set; }

    /// <summary>孩子真正说出口的元素（不含兜底补的），AI 提示词里要区分对待。</summary>
    public List<string> Requested { get; } = new();

    /// <summary>孩子原话里提炼出的心愿主题（≤12 字）。</summary>
    public string? Topic { get; set; }

    /// <summary>孩子原话。</summary>
    public string? Wish { get; set; }

    public bool Has(string key) => key switch
    {
        "role" => !string.IsNullOrEmpty(Role),
        "scene" => !string.IsNullOrEmpty(Scene),
        "item" => !string.IsNullOrEmpty(Item),
        "weather" => !string.IsNullOrEmpty(Weather),
        "sound" => !string.IsNullOrEmpty(Sound),
        "creature" => !string.IsNullOrEmpty(Creature),
        "emotion" => !string.IsNullOrEmpty(Emotion),
        _ => false
    };

    public string? Get(string key) => key switch
    {
        "role" => Role,
        "scene" => Scene,
        "item" => Item,
        "weather" => Weather,
        "sound" => Sound,
        "creature" => Creature,
        "emotion" => Emotion,
        _ => null
    };

    public void Set(string key, string? value)
    {
        switch (key)
        {
            case "role": Role = value; break;
            case "scene": Scene = value; break;
            case "item": Item = value; break;
            case "weather": Weather = value; break;
            case "sound": Sound = value; break;
            case "creature": Creature = value; break;
            case "emotion": Emotion = value; break;
        }
    }
}

/// <summary>生成好的故事：标题 + 氛围 + 四段正文 + 四步逻辑线。</summary>
public sealed class LoomStory
{
    public string Title { get; set; } = "";
    public string Mood { get; set; } = "星夜";
    public List<string> Paragraphs { get; set; } = new();
    public List<string> Outline { get; set; } = new();

    /// <summary>true = 大模型写的，false = 内置模板。</summary>
    public bool IsAi { get; set; }

    /// <summary>本次采纳了几个孩子自己说的元素（成长统计用）。</summary>
    public int KeywordCount { get; set; }

    public string FullText => string.Join("\n", Paragraphs);

    public bool IsValid =>
        Paragraphs.Count == 4 &&
        Paragraphs.All(p => p.Length >= 15) &&
        Outline.Count == 4;
}

/// <summary>收藏进故事库的一条记录。</summary>
public sealed class LoomSavedStory
{
    public string Title { get; set; } = "";
    public string Mood { get; set; } = "";
    public string Text { get; set; } = "";
    public string TimeText { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Preview => Text.Split('\n').FirstOrDefault() ?? "";

    public string Emoji => Mood.Contains('雨') ? "🌧️" : Mood.Contains('雪') ? "🌨️" : "🌙";

    public string LineCountText => $"{Text.Split('\n').Length} 段";
}

/// <summary>一条创作世界记录（五个角色 + 孩子说的话，按时间倒序）。</summary>
public sealed class LoomRecord
{
    public string TimeText { get; set; } = "";
    public string Who { get; set; } = "";
    public string Text { get; set; } = "";
    public string Accent { get; set; } = "#7B6BFF";
    public DateTime At { get; set; } = DateTime.Now;
}

/// <summary>六维语言潜能的定义。</summary>
public sealed class LoomDimension
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Accent { get; init; } = "#FF6B9D";
}

/// <summary>天赋卡（静态展示，不做判断，避免给孩子贴标签）。</summary>
public sealed class LoomTalentCard
{
    public string Icon { get; init; } = "";
    public string Name { get; init; } = "";
    public string Desc { get; init; } = "";
    public string[] Badges { get; init; } = Array.Empty<string>();
    public string Accent { get; init; } = "#7B6BFF";
}

/// <summary>「无限想象」的全部静态素材。</summary>
public static class StoryLoomCatalog
{
    // ── 五个小伙伴 ──────────────────────────────────────────
    public static readonly LoomAgent[] Agents =
    [
        new() { Key = "spark",  Name = "灵感火花", Emoji = "💡", Accent = "#FF9F40" },
        new() { Key = "scene",  Name = "场景星球", Emoji = "🌌", Accent = "#7B6BFF" },
        new() { Key = "guide",  Name = "创作向导", Emoji = "🧭", Accent = "#4DC4E1" },
        new() { Key = "polish", Name = "精修精灵", Emoji = "✨", Accent = "#FF6B9D" },
        new() { Key = "talent", Name = "天赋瞭望", Emoji = "🔭", Accent = "#FFD60A" }
    ];

    public static LoomAgent Agent(string key) => Agents.First(a => a.Key == key);

    // ── 灵感词条（首页那一排「点一点，让灵感更具体」）────────
    public static readonly LoomChip[] Chips =
    [
        new() { Group = "role",     Emoji = "🐰", Text = "小兔子" },
        new() { Group = "role",     Emoji = "🦊", Text = "小狐狸" },
        new() { Group = "role",     Emoji = "🐼", Text = "小熊猫" },
        new() { Group = "role",     Emoji = "🐶", Text = "小狗" },
        new() { Group = "scene",    Emoji = "🌙", Text = "月亮湾" },
        new() { Group = "scene",    Emoji = "🌳", Text = "森林" },
        new() { Group = "scene",    Emoji = "❄️", Text = "雪地上" },
        new() { Group = "weather",  Emoji = "🌧️", Text = "下着细雨" },
        new() { Group = "weather",  Emoji = "🌕", Text = "月亮很圆" },
        new() { Group = "item",     Emoji = "🍯", Text = "蜂蜜" },
        new() { Group = "item",     Emoji = "🧣", Text = "围巾" },
        new() { Group = "creature", Emoji = "🧚", Text = "萤火虫" },
        new() { Group = "creature", Emoji = "⛵", Text = "小纸船" },
        new() { Group = "sound",    Emoji = "🔔", Text = "叮铃叮铃" },
        new() { Group = "emotion",  Emoji = "😴", Text = "想睡觉" },
        new() { Group = "emotion",  Emoji = "🌙", Text = "晚安" }
    ];

    // ── 元素词库：每组「标准词 + emoji」，用于从孩子的原话里认出元素 ──
    public static readonly (string Word, string Emoji)[] Roles =
    [
        ("小兔子", "🐰"), ("小刺猬", "🦔"), ("小狐狸", "🦊"), ("小猫咪", "🐱"), ("小熊猫", "🐼"),
        ("小企鹅", "🐧"), ("小恐龙", "🦕"), ("小松鼠", "🐿️"), ("小鲸鱼", "🐳"), ("小狗", "🐶")
    ];

    public static readonly (string Word, string Emoji)[] Scenes =
    [
        ("森林", "🌳"), ("云朵上", "☁️"), ("月亮湾", "🌙"), ("花园里", "🌸"),
        ("雪地上", "❄️"), ("海边", "🌊"), ("树洞里", "🕳️"), ("星星岛", "✨")
    ];

    public static readonly (string Word, string Emoji)[] Items =
    [
        ("小灯笼", "🏮"), ("软软的围巾", "🧣"), ("一罐蜂蜜", "🍯"), ("热牛奶", "🥛"),
        ("小饼干", "🍪"), ("小小毛毯", "🧶"), ("一颗糖果", "🍬"), ("会发光的珍珠", "🔮")
    ];

    public static readonly (string Word, string Emoji)[] Weathers =
    [
        ("下着细雨", "🌧️"), ("飘着雪花", "🌨️"), ("刮着晚风", "🌬️"), ("月亮很圆", "🌕"), ("空气甜甜的", "🍭")
    ];

    public static readonly (string Word, string Emoji)[] Sounds =
    [
        ("咕噜咕噜", "🫧"), ("窸窸窣窣", "🍂"), ("叮铃叮铃", "🔔"), ("滴滴答答", "🕰️")
    ];

    public static readonly (string Word, string Emoji)[] Creatures =
    [
        ("会发光的萤火虫", "🧚"), ("会说话的小星星", "⭐"), ("迷路的小纸船", "⛵"),
        ("打喷嚏的小花", "🌸"), ("打哈欠的云猫咪", "😺")
    ];

    public static readonly (string Word, string Emoji)[] Emotions =
    [
        ("想睡觉", "😴"), ("晚安", "🌙"), ("好困", "🥱"), ("怕黑", "🌥️"), ("抱抱", "🤗")
    ];

    /// <summary>七组词库，顺序即「识别优先级」。</summary>
    public static readonly (string Key, (string Word, string Emoji)[] Items)[] Lex =
    [
        ("role", Roles), ("scene", Scenes), ("item", Items), ("weather", Weathers),
        ("sound", Sounds), ("creature", Creatures), ("emotion", Emotions)
    ];

    /// <summary>同义词表：孩子说「兔子」「下雨了」，要能对上标准词。</summary>
    public static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["下着细雨"] = ["下雨", "下雨了", "雨天", "下雨天", "落雨", "下小雨", "雨"],
        ["飘着雪花"] = ["下雪", "下雪了", "下雪天", "雪", "雪花", "雪夜"],
        ["刮着晚风"] = ["刮风", "起风", "晚风", "风"],
        ["月亮很圆"] = ["月圆", "月亮圆", "圆月"],
        ["空气甜甜的"] = ["空气甜", "甜空气"],
        ["想睡觉"] = ["想睡", "想睡觉了", "困了", "要睡觉", "想睡啦"],
        ["晚安"] = ["晚安呀", "道晚安"],
        ["好困"] = ["困", "犯困", "打哈欠"],
        ["怕黑"] = ["天黑怕", "很黑", "黑黑的"],
        ["抱抱"] = ["抱一抱", "抱抱我", "抱"],
        ["一罐蜂蜜"] = ["蜂蜜", "吃蜂蜜"],
        ["热牛奶"] = ["牛奶", "喝牛奶", "奶"],
        ["小饼干"] = ["饼干", "吃饼干"],
        ["一颗糖果"] = ["糖果", "糖", "吃糖"],
        ["会发光的珍珠"] = ["珍珠", "发光珍珠"],
        ["小灯笼"] = ["灯笼", "提灯笼"],
        ["软软的围巾"] = ["围巾", "围脖"],
        ["小小毛毯"] = ["毛毯", "毯子"],
        ["小兔子"] = ["小兔", "兔兔", "兔子"],
        ["小刺猬"] = ["刺猬"],
        ["小狐狸"] = ["狐狸", "小狐"],
        ["小猫咪"] = ["小猫", "猫猫", "猫咪", "喵喵"],
        ["小熊猫"] = ["熊猫"],
        ["小企鹅"] = ["企鹅"],
        ["小恐龙"] = ["恐龙", "恐龙宝宝"],
        ["小松鼠"] = ["松鼠"],
        ["小鲸鱼"] = ["鲸鱼", "小鲸"],
        ["小狗"] = ["狗狗", "狗"],
        ["森林"] = ["树林", "大树", "林中"],
        ["云朵上"] = ["云朵", "云上", "白云上"],
        ["花园里"] = ["花园", "花丛", "小花"],
        ["雪地上"] = ["雪地", "雪堆"],
        ["海边"] = ["大海", "海边"],
        ["树洞里"] = ["树洞", "洞里"],
        ["星星岛"] = ["星星", "星河"],
        ["会发光的萤火虫"] = ["萤火虫", "火虫"],
        ["会说话的小星星"] = ["小星星", "唱歌的星星"],
        ["迷路的小纸船"] = ["纸船", "小船"],
        ["打喷嚏的小花"] = ["打喷嚏的花"],
        ["打哈欠的云猫咪"] = ["云猫咪", "云猫"],
        ["咕噜咕噜"] = ["咕噜", "咕咕"],
        ["窸窸窣窣"] = ["窸窣", "沙沙"],
        ["叮铃叮铃"] = ["叮铃", "铃铃", "铃声"],
        ["滴滴答答"] = ["滴答", "滴水声"]
    };

    // ── 六维语言潜能 ────────────────────────────────────────
    public static readonly LoomDimension[] Dimensions =
    [
        new() { Key = "imagination", Name = "想象力",   Emoji = "🌈", Accent = "#FF9F40" },
        new() { Key = "vocab",       Name = "词汇丰富", Emoji = "📚", Accent = "#4DC4E1" },
        new() { Key = "expression",  Name = "表达力",   Emoji = "🗣️", Accent = "#FF6B9D" },
        new() { Key = "narrative",   Name = "叙事结构", Emoji = "🧩", Accent = "#7B6BFF" },
        new() { Key = "emotion",     Name = "情感感知", Emoji = "💗", Accent = "#34C759" },
        new() { Key = "focus",       Name = "专注坚持", Emoji = "🎯", Accent = "#FFD60A" }
    ];

    // ── 天赋卡（描述倾向，不打分、不排名）──────────────────
    public static readonly LoomTalentCard[] Talents =
    [
        new()
        {
            Icon = "📖", Name = "故事演说型", Accent = "#4DC4E1",
            Desc = "表达连贯、乐意主动讲，适合朗读、戏剧方向。",
            Badges = ["主动表达", "情节连贯"]
        },
        new()
        {
            Icon = "🎨", Name = "意象联想型", Accent = "#FF9F40",
            Desc = "用词灵动、善用比喻与角色，想象力维度领先。",
            Badges = ["比喻丰富", "意象鲜活"]
        },
        new()
        {
            Icon = "🧩", Name = "结构主模型", Accent = "#7B6BFF",
            Desc = "叙事有因果、懂铺垫与收尾，逻辑组织出众。",
            Badges = ["因果清晰", "收尾完整"]
        },
        new()
        {
            Icon = "🌱", Name = "情感共情型", Accent = "#FF6B9D",
            Desc = "在意角色感受、爱用亲切词，共情能力强。",
            Badges = ["用词温暖", "感受入微"]
        }
    ];

    /// <summary>默认开场白（首次进入时由天赋瞭望说出）。</summary>
    public const string WelcomeLine = "你好呀，我是灵感火花。今晚想听什么故事？说一句小心愿，或点几个小元素，我们就开始～";

    /// <summary>输入框占位提示。</summary>
    public const string InputPlaceholder = "小兔子、下雨了、想睡觉…";
}
