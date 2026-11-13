using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>特需训练方向。</summary>
public enum SpecialTrack
{
    /// <summary>视力障碍（低视力 / 全盲）——以听觉与触觉为主要输入通道。</summary>
    Vision,

    /// <summary>聋 / 重听——以视觉与振动为主要输入通道，训练节律、词汇与口型。</summary>
    Hearing,

    /// <summary>社交情绪（听障 / 孤独症谱系常见需求）——表情识别与情境理解。</summary>
    Social
}

/// <summary>
/// 一个训练模块。字段刻意区分「练什么（Goal）」「为什么这样练（Mechanism）」
/// 「练多久（Dose）」「家长怎么带（Tip）」，目的是让训练有依据、可复现，
/// 而不是把一堆小游戏塞给孩子。
/// </summary>
public class SpecialModule
{
    public SpecialTrack Track { get; init; }
    public string Emoji { get; init; } = "⭐";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Accent { get; init; } = "#7E57C2";

    /// <summary>供 XAML 直接绑定的颜色（Accent 字符串转 Color，省掉一层转换器）。</summary>
    public Color AccentColor => SpecialColors.Parse(Accent);

    /// <summary>训练目标（可观察、可测量的行为）。</summary>
    public string Goal { get; init; } = "";

    /// <summary>作用机制（为什么这个活动能练到该能力）。</summary>
    public string Mechanism { get; init; } = "";

    /// <summary>建议剂量：单次时长 + 每日频次。</summary>
    public string Dose { get; init; } = "";

    /// <summary>给家长 / 老师的一句话要领。</summary>
    public string Tip { get; init; } = "";
}

/// <summary>手语词汇卡（词 + 拼音 + 手形示意 + 口型提示）。</summary>
public class SignWord
{
    public string Word { get; init; } = "";
    public string Pinyin { get; init; } = "";
    public string Emoji { get; init; } = "";

    /// <summary>手形 / 动作的简化文字描述（用于无视频时的提示，不作为规范教材）。</summary>
    public string HandShape { get; init; } = "";

    /// <summary>口型提示：帮助听障儿童配合唇读与构音。</summary>
    public string MouthHint { get; init; } = "";

    public string Accent { get; init; } = "#26A69A";

    public Color AccentColor => SpecialColors.Parse(Accent);
}

/// <summary>情绪表情卡（社交情绪识别训练）。</summary>
public class EmotionCard
{
    public string Face { get; init; } = "";
    public string Name { get; init; } = "";
    public string Scene { get; init; } = "";
    public string Accent { get; init; } = "#7E57C2";

    public Color AccentColor => SpecialColors.Parse(Accent);
}

/// <summary>字符串色值 → Color 的安全转换（手写色值打错时给中性兜底，不让页面崩）。</summary>
internal static class SpecialColors
{
    public static Color Parse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.FromArgb("#7E57C2");
        try { return Color.FromArgb(hex); }
        catch { return Color.FromArgb("#7E57C2"); }
    }
}

/// <summary>
/// 特需训练的静态内容库。
/// 说明：这里的「机制」均取自公开的儿童发展 / 特殊教育共识结论
/// （听觉加工与语音回路、感觉运动同步、手势-口语共享语言网络、WCAG 无障碍阈值等），
/// 表述上只说明「这条通道在练什么」，不承诺疗效——训练效果存在个体差异，
/// 需要家长与康复师按孩子的实际情况调整。
/// </summary>
public static class SpecialTrainingData
{
    public static IReadOnlyList<SpecialModule> Vision { get; } = new List<SpecialModule>
    {
        new()
        {
            Track = SpecialTrack.Vision,
            Emoji = "🎯",
            Title = "声音寻宝",
            Subtitle = "听觉定位 · 空间方位",
            Accent = "#26A69A",
            Goal = "听到声音后，指出声源在「左 / 中 / 右」哪一格；连续 6 次里答对 5 次。",
            Mechanism = "听觉定位依赖双耳时间差（ITD）与强度差（ILD）。盲童与低视力儿童常把这条通道" +
                        "发展为空间认知的主要入口，用「先听、再指」的方式练左右与远近概念，比看图片更贴近其日常。",
            Dose = "每次 3～5 分钟，每天 1～2 次；音量保持 50% 左右，双耳都要能听到。",
            Tip = "先让孩子闭眼；家长不要用手势提示，只用语言说「听，声音在哪一边？」"
        },
        new()
        {
            Track = SpecialTrack.Vision,
            Emoji = "🎵",
            Title = "听音复述",
            Subtitle = "听觉序列记忆",
            Accent = "#5C6BC0",
            Goal = "按顺序重放 2～5 个不同音高的音符；连续 3 次全对就加一个音。",
            Mechanism = "属于工作记忆中的「语音回路」：声音按先后顺序被临时存储并复现。听觉序列广度" +
                        "与词汇学习、语音意识相关，是盲童识字与外语学习的重要底层能力。",
            Dose = "每次 5 分钟，每天 1 次；从 2 个音开始，不要跳级。",
            Tip = "孩子复述时允许他先用手指点、再出声，动作辅助能降低记忆负荷。"
        },
        new()
        {
            Track = SpecialTrack.Vision,
            Emoji = "🔊",
            Title = "听声辨物",
            Subtitle = "音色辨别 · 听觉分类",
            Accent = "#8E24AA",
            Goal = "从 3 个声音里挑出「不一样的」那个；准确率达到 80%。",
            Mechanism = "音色（频谱包络）辨别是听觉分类的基础。低视力儿童在无法依靠视觉扫视比较时，" +
                        "需要靠「声音质地」建立归类能力，这与物体恒常性、概念形成直接相关。",
            Dose = "每次 3 分钟，每天 1 次，可放在饭后安静时段。",
            Tip = "环境越安静越好；先用差异大的声音（高/低），再过渡到相近的声音。"
        }
    };

    public static IReadOnlyList<SpecialModule> Hearing { get; } = new List<SpecialModule>
    {
        new()
        {
            Track = SpecialTrack.Hearing,
            Emoji = "💡",
            Title = "节奏光影",
            Subtitle = "视觉节律 · 序列记忆",
            Accent = "#FF8A65",
            Goal = "跟着屏幕依次亮起的色块复现同一顺序；节拍误差稳定在 0.2 秒内。",
            Mechanism = "感觉运动同步（sensorimotor synchronization）可以完全经视觉通道建立，并不依赖听觉。"
                        + "稳定的节律感支持语言韵律、朗读流畅性与注意维持，对听障儿童的早期语言学习很有价值。",
            Dose = "每次 5 分钟，每天 1 次；屏幕亮度不要太低。",
            Tip = "全程不需要出声；孩子答对时手机会振动，告诉他「对了」。"
        },
        new()
        {
            Track = SpecialTrack.Hearing,
            Emoji = "🤟",
            Title = "手语图卡",
            Subtitle = "高频词 · 手形 + 口型",
            Accent = "#26A69A",
            Goal = "看到词能做出对应手势，并配合正确口型；掌握 12 个生活高频词。",
            Mechanism = "手势与口语共享同一套语言脑网络，手语不是「翻译」而是完整的语言系统。"
                        + "同时给出字形、拼音与口型提示，可兼顾手语习得与唇读、构音练习。",
            Dose = "每次 5～8 个词，每天 1 次；学会的词要在真实场景里用出来。",
            Tip = "家长和孩子面对面、保持同一高度；说「谢谢」时把词说出来并做手势，双通道同时输入。"
        }
    };

    public static IReadOnlyList<SpecialModule> Social { get; } = new List<SpecialModule>
    {
        new()
        {
            Track = SpecialTrack.Social,
            Emoji = "😊",
            Title = "表情配对",
            Subtitle = "情绪识别 · 情境理解",
            Accent = "#7E57C2",
            Goal = "看表情说出情绪名称，并说出「他为什么这样」；12 题里答对 10 题。",
            Mechanism = "听障儿童与孤独症谱系儿童常在「读脸」上遇到困难，而表情是口语之外最主要的情绪线索。"
                        + "把表情与具体情境配对，练的是心理理论（Theory of Mind）的前置能力：识别—命名—归因。",
            Dose = "每次 3～5 分钟，每天 1 次；答错时不要急着纠正，先问「你看到了什么」。",
            Tip = "练完立刻在生活里复盘：「刚才弟弟哭了，他是什么感觉？」把卡片迁移到真实场景。"
        }
    };

    public static IReadOnlyList<SpecialModule> All { get; } =
        Vision.Concat(Hearing).Concat(Social).ToList();

    /// <summary>手语图卡词表（12 个生活高频词）。</summary>
    public static IReadOnlyList<SignWord> SignWords { get; } = new List<SignWord>
    {
        new() { Word = "你好", Pinyin = "nǐ hǎo", Emoji = "👋", Accent = "#26A69A",
                HandShape = "右手五指并拢、掌心朝外，在额头前向斜上方挥出。",
                MouthHint = "嘴角向两边展开，露出牙齿，「你」时舌尖抵上齿背。" },
        new() { Word = "谢谢", Pinyin = "xiè xie", Emoji = "🙏", Accent = "#FF8A65",
                HandShape = "右手拇指竖起，指尖轻点下巴两下，再向前伸出。",
                MouthHint = "「谢」时舌尖前伸轻触上齿，气流从齿缝擦出。" },
        new() { Word = "再见", Pinyin = "zài jiàn", Emoji = "👋", Accent = "#5C6BC0",
                HandShape = "右手掌心朝外，五指并拢，左右摆动两下。",
                MouthHint = "「再」时嘴微张，「见」时嘴角向两边拉开。" },
        new() { Word = "对不起", Pinyin = "duì bu qǐ", Emoji = "😔", Accent = "#8E24AA",
                HandShape = "右手五指并拢，指尖抵住下巴，向前下方移动。",
                MouthHint = "「对」时舌尖抵上齿背，「起」时嘴角上扬、露齿。" },
        new() { Word = "请", Pinyin = "qǐng", Emoji = "🤲", Accent = "#00ACC1",
                HandShape = "右手掌心向上、五指并拢，向自己方向轻拉一下。",
                MouthHint = "嘴角向两边展开，舌面抬高，气流从鼻腔出。" },
        new() { Word = "妈妈", Pinyin = "mā ma", Emoji = "👩", Accent = "#EC407A",
                HandShape = "右手拇指伸出，指尖在下巴前点两下。",
                MouthHint = "双唇闭合，气流从鼻腔出（m），可让孩子摸自己鼻翼感受振动。" },
        new() { Word = "爸爸", Pinyin = "bà ba", Emoji = "👨", Accent = "#42A5F5",
                HandShape = "右手拇指伸出，指尖在嘴唇前点两下。",
                MouthHint = "双唇先闭合再突然放开（b），放一张纸在嘴前，纸应被气流吹动。" },
        new() { Word = "老师", Pinyin = "lǎo shī", Emoji = "🧑‍🏫", Accent = "#FFA726",
                HandShape = "右手在胸前竖起拇指（表示「人」），再在额前向外点一下。",
                MouthHint = "「老」时舌尖上翘，「师」时舌尖后卷、气流擦出。" },
        new() { Word = "朋友", Pinyin = "péng you", Emoji = "🧑‍🤝‍🧑", Accent = "#66BB6A",
                HandShape = "两手食指伸出，先互相勾住，再向两边分开。",
                MouthHint = "「朋」时双唇闭合并鼓起，「友」时嘴唇撮圆。" },
        new() { Word = "喝水", Pinyin = "hē shuǐ", Emoji = "🥤", Accent = "#29B6F6",
                HandShape = "右手虚握成杯状，举到嘴边做喝的动作。",
                MouthHint = "「喝」时嘴张开、喉部放松，可让孩子摸喉部感受振动。" },
        new() { Word = "吃饭", Pinyin = "chī fàn", Emoji = "🍚", Accent = "#EF6C00",
                HandShape = "右手食指与中指并拢，向嘴边送两下。",
                MouthHint = "「吃」时舌尖上翘抵硬腭，送气；「饭」时上齿轻咬下唇。" },
        new() { Word = "帮帮我", Pinyin = "bāng bang wǒ", Emoji = "🆘", Accent = "#E53935",
                HandShape = "左手握拳，右手掌心托住左拳向上抬两下；再用手掌拍一下自己胸口。",
                MouthHint = "「帮」时双唇闭合、气流冲出；「我」时嘴唇撮圆。" }
    };

    /// <summary>情绪表情卡。</summary>
    public static IReadOnlyList<EmotionCard> Emotions { get; } = new List<EmotionCard>
    {
        new() { Face = "😄", Name = "开心", Scene = "收到喜欢的礼物，笑到眼睛都弯了。", Accent = "#FFD54F" },
        new() { Face = "😢", Name = "难过", Scene = "心爱的玩具摔坏了，眼泪掉下来。", Accent = "#64B5F6" },
        new() { Face = "😠", Name = "生气", Scene = "排队时被插队，眉毛立起来、拳头握紧。", Accent = "#E57373" },
        new() { Face = "😨", Name = "害怕", Scene = "打雷的晚上，缩在被子里面。", Accent = "#9575CD" },
        new() { Face = "😲", Name = "惊讶", Scene = "打开盒子，里面跳出一只小兔子。", Accent = "#4DB6AC" },
        new() { Face = "😴", Name = "累", Scene = "玩了一整天，眼睛快睁不开了。", Accent = "#90A4AE" },
        new() { Face = "😳", Name = "害羞", Scene = "被大家看着唱歌，脸红了。", Accent = "#F48FB1" },
        new() { Face = "🤔", Name = "困惑", Scene = "这道题看不懂，歪着头想。", Accent = "#A1887F" },
        new() { Face = "😌", Name = "安心", Scene = "妈妈抱着我，轻轻拍我的背。", Accent = "#AED581" },
        new() { Face = "😤", Name = "不服气", Scene = "比赛输了，撅着嘴不说话。", Accent = "#FFB74D" },
        new() { Face = "🤗", Name = "喜欢", Scene = "见到好朋友，张开手臂抱过去。", Accent = "#4FC3F7" },
        new() { Face = "😭", Name = "委屈", Scene = "被误会了，想解释又说不出来。", Accent = "#7986CB" }
    };
}
