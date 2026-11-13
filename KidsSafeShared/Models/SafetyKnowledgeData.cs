using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一条安全知识小贴士（emoji 图标 + 文字说明）。
/// </summary>
public class SafetyTip
{
    public string Emoji { get; init; } = "💡";
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// 一个安全知识主题（如交通安全、防火安全），含若干贴士。
/// </summary>
public class SafetyTopic
{
    public string Emoji { get; init; } = "🛡️";
    public string Title { get; init; } = string.Empty;
    public string Color { get; init; } = "#E53935";
    public List<SafetyTip> Tips { get; init; } = new();
}

/// <summary>
/// 安全知识板块的静态内容（离线内置，按主题分类分组）。
/// 与安全小问答题库主题对应，方便「先学后练」。
/// </summary>
public static class SafetyKnowledgeData
{
    public static readonly List<SafetyTopic> Topics = new()
    {
        new SafetyTopic
        {
            Emoji = "🚦", Title = "交通安全", Color = "#FB8C00",
            Tips = new()
            {
                new() { Emoji = "🚸", Text = "过马路走斑马线，红灯停、绿灯行，左右看车再过。" },
                new() { Emoji = "🚗", Text = "坐车坐后排，系好安全带，头手不伸出窗外。" },
                new() { Emoji = "🚸", Text = "不在马路上追逐打闹，不在车边玩耍。" },
                new() { Emoji = "🚌", Text = "乘车站稳扶好，听大人安排，不把头手伸出窗外。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🤝", Title = "防骗防拐", Color = "#8E24AA",
            Tips = new()
            {
                new() { Emoji = "🍬", Text = "不吃陌生人给的食物、饮料，不跟陌生人走。" },
                new() { Emoji = "🚗", Text = "有人说「你爸妈让我来接你」——先给爸妈打电话确认，别上车。" },
                new() { Emoji = "🧭", Text = "陌生人问路，只告诉他怎么走，不带路、不上车，也不跟他去找「那条街」。" },
                new() { Emoji = "🏠", Text = "独自在家不给陌生人开门。不管说修水管、送快递、查水表，都说「放门口」。" },
                new() { Emoji = "📱", Text = "和爸妈走散了别乱跑：待在原地，或找穿制服的警察、保安，也可以进最近的商店找店员。" },
                new() { Emoji = "🏪", Text = "在商场、超市走散，直接走到收银台，请收银员帮你广播找爸爸妈妈。" },
                new() { Emoji = "🙅", Text = "有人拉你走，大声喊「我不认识你！」，同时抱住路边的树、栏杆或货架不放。" },
                new() { Emoji = "🏃", Text = "往人多的地方跑：商店、门卫室、有摄像头的地方，不要往小巷、停车场跑。" },
                new() { Emoji = "🎁", Text = "有人让你「别告诉爸爸妈妈」，这件事一定要说——真对你好的人不会让你瞒着爸妈。" },
                new() { Emoji = "🚫", Text = "不让陌生人拍照、拍视频，也不跟他去「拍广告」「当模特」「试镜」。" },
                new() { Emoji = "🧑", Text = "认识的邻居、叔叔阿姨要单独带你去别处，也要先问过爸妈，别怕不好意思。" },
                new() { Emoji = "📞", Text = "记住爸妈的手机号和 110。打 110 要说清「我在哪、发生了什么事」。" },
                new() { Emoji = "🌐", Text = "网上认识的人不能见面，也不告诉他你在哪个学校、住哪里。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🔥", Title = "防火安全", Color = "#E53935",
            Tips = new()
            {
                new() { Emoji = "🚭", Text = "打火机、火柴不是玩具，不乱玩火。" },
                new() { Emoji = "🔌", Text = "看到冒火花或焦味，马上告诉大人，不自己碰。" },
                new() { Emoji = "🧯", Text = "衣服着火要“停、躺、滚”，就地打滚压灭火苗。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🔌", Title = "用电安全", Color = "#3949AB",
            Tips = new()
            {
                new() { Emoji = "💧", Text = "不用湿手碰插座和电器，水会导电。" },
                new() { Emoji = "🧵", Text = "电线破损露铜丝，不自己碰，告诉大人修。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🌊", Title = "防溺水", Color = "#1E88E5",
            Tips = new()
            {
                new() { Emoji = "🏊", Text = "玩水要有大人陪，去正规泳池，不去河边池塘。" },
                new() { Emoji = "🆘", Text = "别人落水不自己下水救，大声喊“救命”找大人。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🍎", Title = "饮食安全", Color = "#43A047",
            Tips = new()
            {
                new() { Emoji = "🫧", Text = "水果蔬菜洗干净再吃，保护肚子不生病。" },
                new() { Emoji = "🥫", Text = "过期、变质的食物不能吃，告诉大人处理。" },
                new() { Emoji = "💊", Text = "药不是糖，不认识的别放进嘴里，分不清问大人。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🌏", Title = "灾害应对", Color = "#00897B",
            Tips = new()
            {
                new() { Emoji = "🌏", Text = "地震躲到结实桌子下护住头，不乘电梯。" },
                new() { Emoji = "⛈️", Text = "雷雨天到室内，远离大树、电线和窗户。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "💻", Title = "网络安全", Color = "#D81B60",
            Tips = new()
            {
                new() { Emoji = "🔒", Text = "姓名、学校、住址、电话，都不告诉网友。" },
                new() { Emoji = "🔑", Text = "密码和验证码只告诉自己，谁要都是骗子。" },
                new() { Emoji = "📸", Text = "不拍、不发自己的身体照片，谁要都不给。" },
                new() { Emoji = "🤐", Text = "让你“别告诉爸妈”的网友，马上拉黑并告诉大人。" },
                new() { Emoji = "🚫", Text = "网友约见面不去；要去一定让爸妈陪着，在人多的地方。" },
                new() { Emoji = "🎁", Text = "说“免费送皮肤、送钱”却要你先付钱的，全是骗局。" },
                new() { Emoji = "🔗", Text = "陌生人发来的链接、二维码，不点、不扫。" },
                new() { Emoji = "😰", Text = "网上有人骂你、威胁你，先截图留证据，再告诉爸妈。" },
                new() { Emoji = "📍", Text = "不开位置共享，不发“我在哪儿”的照片。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "🙅", Title = "保护自己", Color = "#5E35B1",
            Tips = new()
            {
                new() { Emoji = "🩲", Text = "背心短裤盖住的地方是隐私部位，别人不能看、不能摸、不能拍。" },
                new() { Emoji = "🗣️", Text = "身体的每个部位都有正确的名字，说出来一点也不羞。" },
                new() { Emoji = "🙅", Text = "任何人让你不舒服的触碰，都可以大声说“不”，然后走开。" },
                new() { Emoji = "🏃", Text = "熟人、亲戚、老师也一样，不喜欢就拒绝，礼貌没有安全重要。" },
                new() { Emoji = "🤫", Text = "让你“别告诉爸妈”的秘密是坏秘密，一定要说。" },
                new() { Emoji = "🚪", Text = "不跟大人单独去没人的房间、车里，先说“我要告诉妈妈”。" },
                new() { Emoji = "🎁", Text = "给礼物、给钱、让你脱衣服拍照的，都是坏人，马上离开。" },
                new() { Emoji = "👨‍⚕️", Text = "医生检查身体要有爸妈在场，没有就说不。" },
                new() { Emoji = "💔", Text = "被欺负了不是你的错，一定要告诉爸妈，他们会保护你。" },
                new() { Emoji = "📝", Text = "说不出口，就先写纸条、发消息给爸妈，或告诉信任的老师。" },
                new() { Emoji = "📱", Text = "网上认识的“哥哥姐姐”要照片、要你保密，都是坏人。" },
                new() { Emoji = "🐶", Text = "不帮陌生人找小狗、带路、搬东西——大人不需要小孩帮忙。" }
            }
        },
        new SafetyTopic
        {
            Emoji = "📞", Title = "急救电话", Color = "#C62828",
            Tips = new()
            {
                new() { Emoji = "🚒", Text = "着火打 119，急救打 120，报警打 110。" },
                new() { Emoji = "🏠", Text = "记住家庭住址和爸妈电话，方便说清位置。" }
            }
        }
    };
}
