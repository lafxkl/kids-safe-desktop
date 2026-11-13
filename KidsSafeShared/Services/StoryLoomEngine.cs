using System.Text;
using System.Text.RegularExpressions;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 「无限想象」的故事引擎。
///
/// 移植自 Dreamloom 网页版的 <c>topicOf / extractKeys / rectify / moodOf / buildStory</c>。
/// 为什么要保留一个「内置模板」而不是全靠大模型：
/// 1. 留守家庭网络常不稳定，睡前的三分钟不该卡在加载上；
/// 2. 模板天然满足「四步因果链」，可以当大模型失败时的兜底；
/// 3. 不需要任何 Key，装上就能用。
///
/// 模板不是随机拼句子：三条故事线各自有完整的因果——
/// 「小纸船迷路」讲的是「不盲从、先观察再行动」，
/// 「怕黑」讲的是「把不认识的影子认出来，恐惧就变小了」，
/// 「晚安约定」讲的是「把声音一句句放轻」的入睡仪式。
/// </summary>
public static partial class StoryLoomEngine
{
    // 去掉口语化的壳子，只留下「心愿主题」
    [GeneratedRegex("[，。！？!?,、.\\s]+")]
    private static partial Regex PunctRegex();

    [GeneratedRegex("我想听|我要听|我想|我要|想听|请讲|给我讲|讲一个|说一个|编一个|来一个|写一个|关于|有关|的故事|故事|一个|今晚|睡前|晚安|好吗|吧")]
    private static partial Regex FillerRegex();

    private static readonly string[] SkyLines =
    [
        "细雨轻轻落下", "雪花慢慢飘落", "晚风轻轻吹过", "一轮圆月挂在天上", "空气里有淡淡的花香"
    ];

    private static readonly string[] SkyKeys =
    [
        "下着细雨", "飘着雪花", "刮着晚风", "月亮很圆", "空气甜甜的"
    ];

    private static readonly Dictionary<string, string> ItemUses = new()
    {
        ["小灯笼"] = "{R}把小灯笼放在身旁，暖光照清了眼前的一小块地方。",
        ["会发光的珍珠"] = "{R}把会发光的珍珠放在身旁，柔光让周围清楚了些。",
        ["软软的围巾"] = "{R}围好软软的围巾，身上暖和了，心里也安定了一点。",
        ["小小毛毯"] = "{R}盖好小小毛毯，舒服地坐下来。",
        ["一罐蜂蜜"] = "{R}把一罐蜂蜜放回架子，准备明早用来做早餐。",
        ["热牛奶"] = "{R}慢慢喝完热牛奶，把杯子放稳，身体暖了起来。",
        ["小饼干"] = "{R}把小饼干收进盒子，留作明天的点心。",
        ["一颗糖果"] = "{R}把一颗糖果放进糖果盒，准备明天再吃。"
    };

    private static readonly Random Rng = new();

    private static string Pick((string Word, string Emoji)[] arr) => arr[Rng.Next(arr.Length)].Word;

    /// <summary>
    /// 从孩子原话里提炼心愿主题；太短或全是口头语时返回 null。
    ///
    /// 传入 <paramref name="idea"/> 时会把已经识别出的元素词一起剔掉，
    /// 否则「小兔子，下雨了，想睡觉」会被当成一个名为「小兔子下雨了想睡觉」的心愿，
    /// 标题就变成《小兔子与『小兔子下雨了想睡觉』的奇妙夜》——元素已经单独承载了意图，不必重复。
    /// </summary>
    public static string? TopicOf(string? text, LoomIdea? idea = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = PunctRegex().Replace(text, "");
        t = FillerRegex().Replace(t, "");

        if (idea is not null)
        {
            foreach (var (key, _) in StoryLoomCatalog.Lex)
            {
                var word = idea.Get(key);
                if (string.IsNullOrEmpty(word)) continue;
                t = t.Replace(word, "", StringComparison.Ordinal);
                if (StoryLoomCatalog.Synonyms.TryGetValue(word, out var syn))
                    foreach (var s in syn)
                        t = t.Replace(s, "", StringComparison.Ordinal);
            }
            t = t.Trim();
        }

        if (t.Length < 2) return null;
        return t.Length > 12 ? t[..12] : t;
    }

    /// <summary>认出孩子原话里的元素。每组只取第一个命中的词，避免一句话塞进两个主角。</summary>
    public static LoomIdea ExtractKeys(string text)
    {
        var idea = new LoomIdea();
        if (string.IsNullOrWhiteSpace(text)) return idea;

        foreach (var (key, items) in StoryLoomCatalog.Lex)
        {
            if (idea.Has(key)) continue;
            foreach (var (word, _) in items)
            {
                var hit = text.Contains(word, StringComparison.Ordinal);
                if (!hit && StoryLoomCatalog.Synonyms.TryGetValue(word, out var syn))
                    hit = syn.Any(s => text.Contains(s, StringComparison.Ordinal));
                if (hit)
                {
                    idea.Set(key, word);
                    break;
                }
            }
        }

        return idea;
    }

    /// <summary>补齐空缺：孩子没提到的，用温柔的默认值填上，保证故事总能写完。</summary>
    public static LoomIdea Rectify(LoomIdea g)
    {
        g.Requested.Clear();
        foreach (var (key, _) in StoryLoomCatalog.Lex)
            if (g.Has(key)) g.Requested.Add(key);

        g.Role ??= Pick(StoryLoomCatalog.Roles);
        g.Scene ??= g.Role == "小鲸鱼" ? "海边" : "森林";
        g.Weather ??= g.Scene == "雪地上" ? "飘着雪花" : "月亮很圆";
        g.Creature ??= "打哈欠的云猫咪";
        g.Item ??= "小小毛毯";
        return g;
    }

    public static string MoodOf(LoomIdea g) => g.Weather switch
    {
        "下着细雨" => "雨夜",
        "飘着雪花" => "雪夜",
        "刮着晚风" => "风之夜晚",
        "月亮很圆" => "月圆之夜",
        "空气甜甜的" => "甜梦之夜",
        _ => "星夜"
    };

    private static string SkyOf(string? weather)
    {
        if (weather is null) return "夜色安静又温柔";
        var i = Array.IndexOf(SkyKeys, weather);
        return i >= 0 ? SkyLines[i] : "夜色安静又温柔";
    }

    /// <summary>
    /// 用内置模板写一个四段故事。三条分支对应三种真实场景：走失、怕黑、舍不得睡。
    /// </summary>
    public static LoomStory BuildTemplate(LoomIdea g)
    {
        var R = g.Role ?? "小兔子";
        var S = g.Scene ?? "森林";
        var I = g.Item ?? "小小毛毯";
        var CR = g.Creature ?? "打哈欠的云猫咪";
        var mood = MoodOf(g);

        var use = ItemUses.TryGetValue(I, out var tpl)
            ? tpl.Replace("{R}", R)
            : $"{R}把{I}安放在身边。";

        var intro = $"{R}在{S}有个安静的休息角落。今晚，{SkyOf(g.Weather)}。";

        string title;
        List<string> paras;
        List<string> outline;

        if (CR == "迷路的小纸船")
        {
            title = $"{R}给小纸船指路";
            paras =
            [
                $"{intro}{R}正准备休息，看见迷路的小纸船停在一小片浅水里。纸船说：「我记得家在一座小桥旁，可找不到方向了。」",
                $"{R}没有让纸船随意漂走，而是留在岸边，陪它找那座桥。{use}它们约好，先看清附近有什么，再慢慢找。",
                $"沿着浅水望过去，{R}发现不远处正有一座小桥，桥旁系着和纸船一样颜色的丝带。纸船认出了丝带，顺着缓缓的水流漂到桥旁，停进了自己的小船坞。",
                $"「找到家啦，谢谢你陪我看清方向！」纸船轻轻摇了摇。{R}回到原来的休息角落，想到纸船已经安稳停好，也安心闭上了眼睛。"
            ];
            outline =
            [
                "纸船找不到桥旁的家",
                "主角陪纸船留在浅水边寻找线索",
                "发现小桥和丝带，纸船认出家",
                "纸船归家，主角回到休息处安心入睡"
            ];
        }
        else if (g.Emotion == "怕黑")
        {
            title = $"{R}认出了夜里的影子";
            paras =
            [
                $"{intro}{R}却还不敢闭眼：休息角落旁有团晃动的黑影，它不知道那是什么。",
                $"{CR}听见了它的小声询问，便陪在旁边。{use}{R}没有急着躲开，而是请伙伴陪自己仔细看一看影子。",
                $"它们看见，树枝一动，那团影子也跟着动；树枝停下，影子也停下。{R}轻轻笑了：「原来是树枝的影子呀。」刚才不认识的黑影，现在有了熟悉的名字。",
                $"认出影子以后，{R}不再一直盯着它。{CR}在旁边轻声说了晚安，{R}慢慢放松下来，在熟悉的夜色里睡着了。"
            ];
            outline =
            [
                "主角因不认识晃动的影子而怕黑",
                "伙伴陪伴，主角决定观察",
                "主角发现影子与树枝同步，认出来源",
                "困扰解除，主角在原处安心入睡"
            ];
        }
        else
        {
            title = $"{R}和{CR}的晚安约定";
            var sound = string.IsNullOrEmpty(g.Sound)
                ? "它想听一句轻轻的晚安，又怕打扰别人。"
                : $"它轻轻模仿孩子说的「{g.Sound}」，想把这个声音编进晚安的小节拍。";

            var opening = g.Emotion == "抱抱"
                ? "想要一个温暖的拥抱，却不好意思开口。"
                : "已经有些困了，却还想等伙伴一起说晚安。";

            var third = g.Emotion == "抱抱"
                ? $"{R}也说出了想要抱抱的心愿，伙伴温柔地回应了它。"
                : "说到最后一句时，它们都不再惦记着继续玩了。";

            paras =
            [
                $"{intro}{R}{opening}",
                $"{CR}来到休息角落旁，也打了一个小哈欠。{sound}{R}小声邀请：「我们就在这里，一起说晚安吧。」{use}",
                $"{R}和伙伴约好，每说一句晚安，就把声音放轻一点。第一句送给今天见过的朋友，第二句送给彼此，最后一句送给自己。{third}",
                $"说完约好的晚安，{CR}在旁边安静休息。{R}也在自己的角落闭上眼睛：伙伴就在身边，今天的心愿已经完成，可以放心睡觉了。"
            ];
            outline =
            [
                "主角想等伙伴一起道晚安",
                "伙伴到来，主角发出邀请",
                "一起完成声音渐轻的晚安约定",
                "心愿完成，彼此在原处安心休息"
            ];
        }

        return new LoomStory
        {
            Title = title,
            Mood = string.IsNullOrEmpty(g.Emotion) ? mood : $"{g.Emotion} · {mood}",
            Paragraphs = paras,
            Outline = outline,
            IsAi = false,
            KeywordCount = g.Requested.Count
        };
    }

    /// <summary>
    /// 给大模型的提示词。关键是「先写四步逻辑链，再写正文」——
    /// 原版调试下来，这样输出才不会前后矛盾。
    /// </summary>
    public static List<(string Role, string Content)> BuildAiMessages(LoomIdea g, string? wish)
    {
        var label = new (string Key, string Name)[]
        {
            ("role", "主角"), ("scene", "场景"), ("weather", "天气"), ("sound", "声音"),
            ("creature", "神奇伙伴"), ("item", "重要物品"), ("emotion", "心情")
        };

        var lines = new List<string>();
        foreach (var (key, name) in label)
        {
            if (!g.Has(key)) continue;
            if (g.Requested.Count > 0 && !g.Requested.Contains(key)) continue;
            lines.Add($"- {name}：{g.Get(key)}");
        }

        var sys = new StringBuilder()
            .AppendLine("你是一位给 3-8 岁儿童写中文睡前故事的作家。你写的故事必须『逻辑通顺』：")
            .AppendLine("1. 主角明确、愿望明确，每一段都由上一段的因果自然引出（因为…所以…），不许跳步、不许前后矛盾；")
            .AppendLine("2. 结构固定四步：①主角想睡觉却被一个小困扰或小愿望牵住 → ②出现一个小小的转折或求助 → ③用温柔的办法解决（主角要主动参与） → ④安心入睡；")
            .AppendLine("3. 语言简单、温柔、口语化，句子短，多用拟声词和具体画面；")
            .AppendLine("4. 不许说教、不许吓人、不许出现危险或悲伤结局；")
            .AppendLine("5. 全文 4 段，每段 2-4 句；围绕同一个小困扰展开，结尾明确解决开头的问题；")
            .AppendLine("6. 孩子原话里的角色、动作、目标优先于补充元素，不得把孩子要求的具体事件改成只提一下主题；")
            .AppendLine("7. 物品按真实用途使用：食物不能当衣物，不能靠数星星突然找到路。声音要交代来源，地点变化要交代过程；")
            .AppendLine("8. 正文与 outline 逐段对应。写完自查人物、物品、地点和因果，修正后再输出；")
            .Append("只输出一个 JSON 对象，不要任何多余文字或代码块。")
            .ToString();

        var usr = new StringBuilder()
            .AppendLine("请根据今晚的灵感创作一个睡前故事。")
            .AppendLine()
            .AppendLine("必须自然用到的元素：")
            .AppendLine(lines.Count > 0 ? string.Join("\n", lines) : "（可自由发挥）");

        if (!string.IsNullOrWhiteSpace(wish))
            usr.AppendLine().AppendLine($"孩子说：「{wish}」");
        if (!string.IsNullOrWhiteSpace(g.Topic))
            usr.AppendLine().AppendLine($"孩子的心愿主题：「{g.Topic}」，故事要围绕它展开。");

        usr.AppendLine()
           .AppendLine("先在 outline 里写出四步逻辑链（每步一句，说明『谁因为什么做了什么，导致什么』），再据此写正文。")
           .AppendLine("严格只输出如下 JSON：")
           .Append("{\"outline\":[\"第1步\",\"第2步\",\"第3步\",\"第4步\"],\"title\":\"故事标题\",\"mood\":\"氛围2-4字\",\"paras\":[\"第1段\",\"第2段\",\"第3段\",\"第4段\"]}");

        return [("system", sys), ("user", usr.ToString())];
    }

    /// <summary>把大模型返回的内容解析成故事；格式不对就抛，让调用方重试或回退模板。</summary>
    public static LoomStory ParseAiJson(string? raw)
    {
        var t = (raw ?? "").Trim().TrimStart('\uFEFF');
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = t.IndexOf('\n');
            if (nl > 0) t = t[(nl + 1)..];
            if (t.EndsWith("```", StringComparison.Ordinal)) t = t[..^3];
            t = t.Trim();
        }

        var s = t.IndexOf('{');
        var e = t.LastIndexOf('}');
        if (s >= 0 && e > s) t = t[s..(e + 1)];

        using var doc = System.Text.Json.JsonDocument.Parse(t);
        var root = doc.RootElement;

        var paras = new List<string>();
        if (root.TryGetProperty("paras", out var pa) && pa.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var p in pa.EnumerateArray())
                paras.Add(p.GetString()?.Trim() ?? "");

        var outline = new List<string>();
        if (root.TryGetProperty("outline", out var ol) && ol.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var p in ol.EnumerateArray())
                outline.Add(p.GetString()?.Trim() ?? "");

        var title = root.TryGetProperty("title", out var ti) ? (ti.GetString() ?? "").Trim() : "";
        var mood = root.TryGetProperty("mood", out var mo) ? (mo.GetString() ?? "").Trim() : "";

        if (paras.Count != 4 || paras.Any(p => p.Length < 15 || p.Length > 600))
            throw new InvalidOperationException("故事需要四个完整且适合朗读的段落");
        if (outline.Count != 4 || outline.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("故事缺少完整的四步情节");
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("故事标题格式不正确");

        return new LoomStory
        {
            Title = title.Length > 60 ? title[..60] : title,
            Mood = string.IsNullOrWhiteSpace(mood) ? "星夜" : (mood.Length > 30 ? mood[..30] : mood),
            Paragraphs = paras,
            Outline = outline,
            IsAi = true
        };
    }
}
