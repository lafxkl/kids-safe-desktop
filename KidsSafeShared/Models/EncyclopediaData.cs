using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>一道百科知识问答题。</summary>
public class EncyclopediaQuestion
{
    public string Category { get; init; } = string.Empty;
    public string Emoji { get; init; } = "📚";
    public string Question { get; init; } = string.Empty;
    public List<string> Options { get; init; } = new();
    public int CorrectIndex { get; init; }
    public string Explain { get; init; } = string.Empty;
}

/// <summary>百科知识题库（动物 / 植物 / 自然 / 科学 / 生活常识等）。</summary>
public static class EncyclopediaData
{
    public static readonly List<EncyclopediaQuestion> Questions = new()
    {
        // ===== 动物 =====
        new() { Category = "动物", Emoji = "🐱", Question = "小猫平时最爱用什么方式洗脸？", Options = new() { "用爪子沾口水擦脸", "用毛巾", "用水龙头", "不洗脸" }, CorrectIndex = 0, Explain = "猫咪用舌头舔湿爪子，再洗脸，还能清洁毛发。" },
        new() { Category = "动物", Emoji = "🐘", Question = "大象身上最特别的长长器官是什么？", Options = new() { "长鼻子", "长尾巴", "长耳朵当扇子", "长舌头" }, CorrectIndex = 0, Explain = "大象的长鼻子既能呼吸、闻味，也能卷东西吃。" },
        new() { Category = "动物", Emoji = "🐝", Question = "小蜜蜂飞到花上主要是为了？", Options = new() { "采花蜜和花粉", "睡觉", "玩耍", "躲雨" }, CorrectIndex = 0, Explain = "蜜蜂采蜜酿成甜甜的蜂蜜，还帮花朵传播花粉。" },
        new() { Category = "动物", Emoji = "🐟", Question = "鱼在水中靠什么呼吸？", Options = new() { "鳃", "鼻子", "皮肤", "耳朵" }, CorrectIndex = 0, Explain = "鱼用鳃过滤水中的氧气来呼吸。" },
        new() { Category = "动物", Emoji = "🦇", Question = "蝙蝠晚上活动，主要靠什么找路？", Options = new() { "发出超声波回声定位", "眼睛看得很远", "跟着月亮", "闻味道" }, CorrectIndex = 0, Explain = "蝙蝠发出超声波，靠回声判断前方有没有障碍。" },

        // ===== 植物 =====
        new() { Category = "植物", Emoji = "🌻", Question = "向日葵为什么叫‘向日’葵？", Options = new() { "花盘会跟着太阳转", "它是金色的", "它爱喝水", "它只在晚上开" }, CorrectIndex = 0, Explain = "向日葵的花盘会随太阳转动，朝向阳光生长。" },
        new() { Category = "植物", Emoji = "🌳", Question = "树木通过什么制造养分？", Options = new() { "叶子进行光合作用", "根喝水", "树干发胖", "花朵唱歌" }, CorrectIndex = 0, Explain = "绿叶利用阳光把水和空气变成养分，这就是光合作用。" },
        new() { Category = "植物", Emoji = "🌱", Question = "种子发芽最先长出来的是？", Options = new() { "根", "花", "果实", "叶子" }, CorrectIndex = 0, Explain = "种子先向下长出根，再向上长出茎和叶。" },
        new() { Category = "植物", Emoji = "🍎", Question = "苹果是植物的哪一部分？", Options = new() { "果实", "根", "叶子", "茎" }, CorrectIndex = 0, Explain = "苹果是植物的果实，里面还藏着种子。" },

        // ===== 自然 =====
        new() { Category = "自然", Emoji = "🌈", Question = "彩虹通常出现在什么之后？", Options = new() { "下雨后出太阳", "刮大风时", "下雪时", "打雷前" }, CorrectIndex = 0, Explain = "阳光照在空中的小水珠上，折射出七彩的彩虹。" },
        new() { Category = "自然", Emoji = "🌙", Question = "夜晚天空中最亮、形状会变的是？", Options = new() { "月亮", "太阳", "星星不动", "云" }, CorrectIndex = 0, Explain = "月亮本身不发光，是被太阳照亮，所以我们看到它的形状会变。" },
        new() { Category = "自然", Emoji = "❄️", Question = "雪花为什么大多是六角形？", Options = new() { "水的晶体结构决定的", "风剪的", "人画的", "随便长的" }, CorrectIndex = 0, Explain = "水蒸气结冰时，水分子会排成漂亮的六角形晶体。" },
        new() { Category = "自然", Emoji = "🌊", Question = "海水的味道为什么是咸的？", Options = new() { "里面溶解了很多盐", "有糖", "有气泡", "有泥沙" }, CorrectIndex = 0, Explain = "河水把陆地上的盐分带进大海，海水越积越咸。" },

        // ===== 科学 =====
        new() { Category = "科学", Emoji = "🔥", Question = "水加热到多少度会烧开？", Options = new() { "大约 100℃", "50℃", "0℃", "200℃" }, CorrectIndex = 0, Explain = "在平常气压下，水加热到约 100℃ 就会沸腾变成水蒸气。" },
        new() { Category = "科学", Emoji = "🧲", Question = "磁铁能吸起下面哪种东西？", Options = new() { "铁钉", "木头块", "塑料勺", "橡皮" }, CorrectIndex = 0, Explain = "磁铁能吸引铁、镍等金属，木头和塑料吸不起来。" },
        new() { Category = "科学", Emoji = "💡", Question = "电灯发光主要把电能变成了？", Options = new() { "光能和热能", "声音", "香味", "颜色" }, CorrectIndex = 0, Explain = "电灯通电后，电能变成光，也会放出一点热。" },
        new() { Category = "科学", Emoji = "🌡️", Question = "我们常用什么看体温有没有发烧？", Options = new() { "温度计", "尺子", "放大镜", "天平" }, CorrectIndex = 0, Explain = "体温计能测出身体的温度，正常约 36~37℃。" },

        // ===== 生活常识 =====
        new() { Category = "生活", Emoji = "🦷", Question = "为了保护牙齿，每天最好刷几次牙？", Options = new() { "早晚各一次", "一周一次", "只在早上", "不用刷" }, CorrectIndex = 0, Explain = "早晚刷牙、饭后漱口，能让牙齿更健康。" },
        new() { Category = "生活", Emoji = "🚦", Question = "过马路时，红灯表示？", Options = new() { "停，不能走", "快跑过去", "可以慢慢走", "随便走" }, CorrectIndex = 0, Explain = "红灯停、绿灯行，过马路还要走斑马线。" },
        new() { Category = "生活", Emoji = "🤧", Question = "打喷嚏时应该怎么做更礼貌？", Options = new() { "用手肘或纸巾挡住", "对着别人", "大声喊", "不用管" }, CorrectIndex = 0, Explain = "用手肘或纸巾捂住口鼻，能防止病菌传播。" },
        new() { Category = "生活", Emoji = "🔥", Question = "家里着火了，小朋友首先应该？", Options = new() { "告诉大人并拨打 119", "自己灭火", "躲起来", "泼油" }, CorrectIndex = 0, Explain = "小朋友先逃生并大声呼叫大人，再拨打火警电话 119。" },

        // ===== 身体 =====
        new() { Category = "身体", Emoji = "❤️", Question = "人体内一直跳动、输送血液的是？", Options = new() { "心脏", "胃", "头发", "指甲" }, CorrectIndex = 0, Explain = "心脏像水泵一样，把血液送到全身。" },
        new() { Category = "身体", Emoji = "🦴", Question = "我们的身体里坚硬的支架是什么？", Options = new() { "骨骼", "肌肉", "皮肤", "头发" }, CorrectIndex = 0, Explain = "骨骼支撑身体、保护内脏，还能让我们可以活动。" },
        new() { Category = "身体", Emoji = "🧠", Question = "用来思考、记事情的最重要器官是？", Options = new() { "大脑", "肚子", "脚", "手指" }, CorrectIndex = 0, Explain = "大脑指挥我们思考、学习、运动和感受。" },

        // ===== 食物 =====
        new() { Category = "食物", Emoji = "🥛", Question = "牛奶里含很多什么，能帮我们长高？", Options = new() { "钙", "沙子", "盐巴", "糖" }, CorrectIndex = 0, Explain = "牛奶富含钙，能帮助骨骼和牙齿长得结实。" },
        new() { Category = "食物", Emoji = "🍚", Question = "我们每天吃的主食（米饭、面条）主要提供？", Options = new() { "能量", "只会让人渴", "颜色", "声音" }, CorrectIndex = 0, Explain = "米饭、面条含有淀粉，是身体活动的主要能量来源。" },
    };
}
