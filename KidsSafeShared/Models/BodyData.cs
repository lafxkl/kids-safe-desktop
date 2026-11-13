using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>身体百科里的一个身体部位。</summary>
public class BodyPart
{
    public string Name { get; init; } = string.Empty;
    public string Emoji { get; init; } = "🧍";
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string FunFact { get; init; } = string.Empty;
}

/// <summary>身体百科知识库（头部 / 四肢 / 身体内部）。</summary>
public static class BodyData
{
    public static readonly List<BodyPart> Parts = new()
    {
        // ===== 头部 =====
        new() { Category = "头部", Emoji = "🧠", Name = "大脑", Description = "人体的「司令部」，负责思考、记忆和指挥身体活动。", FunFact = "你每天想到的事情，都由大脑在悄悄处理！" },
        new() { Category = "头部", Emoji = "👀", Name = "眼睛", Description = "用来看世界、分辨颜色和形状。", FunFact = "眼睛里的瞳孔会随着光线变亮或变暗而放大、缩小。" },
        new() { Category = "头部", Emoji = "👂", Name = "耳朵", Description = "听声音，还帮忙保持身体平衡。", FunFact = "耳朵里藏着人体最小的骨头，比米粒还小。" },
        new() { Category = "头部", Emoji = "👃", Name = "鼻子", Description = "闻气味，也帮忙呼吸。", FunFact = "鼻子大约能分辨一万种不同气味！" },
        new() { Category = "头部", Emoji = "👄", Name = "嘴巴", Description = "说话、吃饭、微笑都靠它。", FunFact = "人一辈子大约要说 1.2 亿个字！" },
        new() { Category = "头部", Emoji = "🦷", Name = "牙齿", Description = "咬碎和咀嚼食物，帮助消化。", FunFact = "小时候有 20 颗乳牙，长大后有 28~32 颗恒牙。" },
        new() { Category = "头部", Emoji = "👅", Name = "舌头", Description = "尝味道，也帮忙发音说话。", FunFact = "舌头能分辨酸、甜、苦、咸、鲜五种味道。" },
        new() { Category = "头部", Emoji = "💇", Name = "头发", Description = "保护头皮，还能保暖。", FunFact = "头发每个月大约长 1 厘米。" },

        // ===== 四肢 =====
        new() { Category = "四肢", Emoji = "✋", Name = "手", Description = "拿东西、写字、拥抱都要用到手。", FunFact = "每只手里有 27 块小骨头。" },
        new() { Category = "四肢", Emoji = "🦶", Name = "脚", Description = "站立、走路、跑步都靠脚。", FunFact = "脚底有很多穴位，和全身都有关系。" },
        new() { Category = "四肢", Emoji = "💪", Name = "手臂", Description = "抬起、抱物、用力都离不开手臂。", FunFact = "手臂上的肌肉越锻炼越强壮。" },

        // ===== 身体内部 =====
        new() { Category = "身体内部", Emoji = "❤️", Name = "心脏", Description = "不停地跳动，把血液送到全身。", FunFact = "心脏一天大约要跳 10 万次！" },
        new() { Category = "身体内部", Emoji = "🫁", Name = "肺", Description = "吸气吐气，把氧气送进血液。", FunFact = "大人每分钟大约呼吸 12~20 次。" },
        new() { Category = "身体内部", Emoji = "🫃", Name = "胃", Description = "搅拌和消化吃进去的食物。", FunFact = "胃液很酸，能帮我们把食物分解开。" },
        new() { Category = "身体内部", Emoji = "🟫", Name = "肝脏", Description = "帮身体解毒，还储存能量。", FunFact = "肝脏是身体里最大的内脏器官。" },
        new() { Category = "身体内部", Emoji = "🦴", Name = "骨骼", Description = "支撑身体，保护柔软的内脏。", FunFact = "成人一共有 206 块骨头。" },
        new() { Category = "身体内部", Emoji = "🤚", Name = "皮肤", Description = "包裹全身，保护身体、感受冷热。", FunFact = "皮肤是人体面积最大的器官。" }
    };

    public static readonly List<string> Categories = new() { "全部", "头部", "四肢", "身体内部" };
}
