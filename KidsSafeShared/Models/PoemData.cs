using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>一首古诗。</summary>
public sealed class Poem
{
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Dynasty { get; init; } = string.Empty;

    /// <summary>正文，按句分行（朗读时用空格连起来，断句更自然）。</summary>
    public string[] Lines { get; init; } = [];

    /// <summary>主题分类，见 <see cref="PoemData.Categories"/>。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>注释：每条形如「字词 —— 解释」。</summary>
    public string[] Notes { get; init; } = [];

    /// <summary>白话译文。</summary>
    public string Translation { get; init; } = string.Empty;

    /// <summary>一句话赏析 / 小知识，给孩子看的。</summary>
    public string Appreciation { get; init; } = string.Empty;

    /// <summary>列表卡片上显示的「作者 · 朝代」。</summary>
    public string AuthorLine => $"{Dynasty} · {Author}";

    /// <summary>分类对应的 emoji（列表卡片左侧圆的图案）。</summary>
    public string Emoji => PoemData.EmojiOf(Category);

    /// <summary>分类对应的主题色（列表卡片左侧圆的底色）。</summary>
    public Color Accent => Color.FromArgb(PoemData.ColorOf(Category));

    /// <summary>正文前两句，用于列表预览。</summary>
    public string Preview => string.Join("，", Lines.Take(2)).TrimEnd('。', '，') + "……";

    /// <summary>朗读用全文。</summary>
    public string SpeakText => $"{Title}，{Author}。" + string.Join("，", Lines);
}

/// <summary>
/// 古诗学习的内置内容：小学阶段最常背的 26 首，按主题分五类。
///
/// 全部离线内置，不依赖服务端也不访问第三方接口——孩子点开就能读，
/// 断网、飞行模式下一样能用。文本以人教版小学语文教材的通行版本为准，
/// 个别字词在不同版本里有出入时，取教材写法。
/// </summary>
public static class PoemData
{
    public const string CategoryMissing = "思念·送别";
    public const string CategorySeason = "四季风光";
    public const string CategoryNature = "山水田园";
    public const string CategoryChild = "童趣生活";
    public const string CategorySpirit = "励志哲理";

    public static readonly string[] Categories =
    [
        CategoryNature, CategorySeason, CategoryChild, CategoryMissing, CategorySpirit
    ];

    public static string EmojiOf(string category) => category switch
    {
        CategoryNature => "⛰️",
        CategorySeason => "🌸",
        CategoryChild => "🧒",
        CategoryMissing => "🌙",
        CategorySpirit => "🌱",
        _ => "📜"
    };

    public static string ColorOf(string category) => category switch
    {
        CategoryNature => "#26A69A",
        CategorySeason => "#EC407A",
        CategoryChild => "#FB8C00",
        CategoryMissing => "#7E57C2",
        CategorySpirit => "#43A047",
        _ => "#8D6E63"
    };

    public static readonly List<Poem> All =
    [
        // ── 山水田园 ───────────────────────────────────
        new Poem
        {
            Title = "望庐山瀑布", Author = "李白", Dynasty = "唐", Category = CategoryNature,
            Lines = ["日照香炉生紫烟", "遥看瀑布挂前川", "飞流直下三千尺", "疑是银河落九天"],
            Notes =
            [
                "香炉 —— 指庐山香炉峰，山峰形状像香炉，常有云雾缭绕",
                "紫烟 —— 阳光照在云雾上，泛出紫色的光",
                "三千尺 —— 夸张的说法，形容瀑布非常高",
                "九天 —— 古人说的最高一层天"
            ],
            Translation = "太阳照着香炉峰，升起紫色的云雾；远远望去，瀑布像一条白练挂在山前。水流从高处飞泻而下，好像有三千尺那么长，让人怀疑是银河从九天之上落了下来。",
            Appreciation = "李白最爱用夸张。三千尺、银河落九天，读完脑子里就有哗哗的水声。"
        },
        new Poem
        {
            Title = "江雪", Author = "柳宗元", Dynasty = "唐", Category = CategoryNature,
            Lines = ["千山鸟飞绝", "万径人踪灭", "孤舟蓑笠翁", "独钓寒江雪"],
            Notes =
            [
                "绝 —— 断绝，这里指一只鸟也没有",
                "万径 —— 成千上万条小路",
                "蓑笠 —— 蓑衣和斗笠，古人用来挡雨雪的用具",
                "翁 —— 老人"
            ],
            Translation = "群山之中一只飞鸟都没有，条条小路上也看不见人的脚印。只有一叶小舟上坐着一位披蓑衣、戴斗笠的老翁，独自在飘雪的寒江上垂钓。",
            Appreciation = "四句诗像一幅画：越写越空，最后只剩一个老人和一场雪。"
        },
        new Poem
        {
            Title = "寻隐者不遇", Author = "贾岛", Dynasty = "唐", Category = CategoryNature,
            Lines = ["松下问童子", "言师采药去", "只在此山中", "云深不知处"],
            Notes =
            [
                "隐者 —— 隐居在山里、不愿做官的人",
                "童子 —— 隐者身边的小徒弟",
                "言 —— 说",
                "云深 —— 云雾很深，指山里看不清的地方"
            ],
            Translation = "在松树下问隐者的小徒弟，他说师父采药去了。就在这座山里，可是云雾太深，不知道具体在什么地方。",
            Appreciation = "全诗只有二十个字，一问一答，却留下了一大片云雾让人去想。"
        },
        new Poem
        {
            Title = "敕勒歌", Author = "北朝民歌", Dynasty = "南北朝", Category = CategoryNature,
            Lines = ["敕勒川，阴山下", "天似穹庐，笼盖四野", "天苍苍，野茫茫", "风吹草低见牛羊"],
            Notes =
            [
                "敕勒川 —— 敕勒族居住的平原，在今内蒙古一带",
                "穹庐 —— 圆顶的毡帐，就是蒙古包",
                "苍苍 —— 深青色，形容天空高远",
                "见 —— 同「现」，出现、显露"
            ],
            Translation = "敕勒川在阴山脚下。天空像一个巨大的圆顶帐篷，罩住了四面的原野。天空青苍苍，原野雾茫茫，风吹过让草低下头，就露出成群的牛羊。",
            Appreciation = "最后七个字最妙：草有多高、牛羊有多少，都不用说，风一吹就全看见了。"
        },
        new Poem
        {
            Title = "早发白帝城", Author = "李白", Dynasty = "唐", Category = CategoryNature,
            Lines = ["朝辞白帝彩云间", "千里江陵一日还", "两岸猿声啼不住", "轻舟已过万重山"],
            Notes =
            [
                "白帝城 —— 在今重庆奉节，建在长江边的高山上",
                "朝辞 —— 早晨告别",
                "江陵 —— 今湖北荆州，离白帝城约千里水路",
                "万重山 —— 一层又一层数不清的山"
            ],
            Translation = "清晨告别了彩云缭绕的白帝城，千里之外的江陵一天就能到达。两岸猿猴的叫声还在耳边不停，轻快的小船已经穿过了层层叠叠的群山。",
            Appreciation = "四句诗一直在「动」，读完像跟着小船顺流冲了一路。"
        },
        new Poem
        {
            Title = "望天门山", Author = "李白", Dynasty = "唐", Category = CategoryNature,
            Lines = ["天门中断楚江开", "碧水东流至此回", "两岸青山相对出", "孤帆一片日边来"],
            Notes =
            [
                "天门山 —— 在今安徽当涂，长江两岸各有一座山，隔江相对像一道门",
                "楚江 —— 长江流经古楚国一带的称呼",
                "回 —— 回旋、掉头，形容水流在这里打转",
                "孤帆 —— 一只单独的小船"
            ],
            Translation = "天门山从中间断开，长江从这里奔涌而出；碧绿的江水向东流到这里，打了个回旋。两岸的青山面对面地迎面而来，一片孤帆从太阳升起的地方缓缓驶来。",
            Appreciation = "「相对出」三个字，把静止的山写得像在向人走来。"
        },
        new Poem
        {
            Title = "饮湖上初晴后雨", Author = "苏轼", Dynasty = "宋", Category = CategoryNature,
            Lines = ["水光潋滟晴方好", "山色空蒙雨亦奇", "欲把西湖比西子", "淡妆浓抹总相宜"],
            Notes =
            [
                "潋滟 —— 水面波光闪动的样子",
                "空蒙 —— 细雨迷蒙、朦朦胧胧",
                "西子 —— 西施，春秋时期越国的美女",
                "相宜 —— 合适、好看"
            ],
            Translation = "晴天时西湖波光闪动，正是好看的时候；下雨时山色迷迷蒙蒙，也同样奇妙。如果把西湖比作美人西施，那么无论是淡雅的妆还是浓艳的妆，她都很合适。",
            Appreciation = "把湖比作美人，从此西湖又叫「西子湖」。"
        },

        // ── 四季风光 ───────────────────────────────────
        new Poem
        {
            Title = "春晓", Author = "孟浩然", Dynasty = "唐", Category = CategorySeason,
            Lines = ["春眠不觉晓", "处处闻啼鸟", "夜来风雨声", "花落知多少"],
            Notes =
            [
                "晓 —— 天亮",
                "不觉晓 —— 不知不觉就天亮了",
                "闻 —— 听见",
                "知多少 —— 不知道有多少"
            ],
            Translation = "春天睡得香甜，不知不觉天就亮了，醒来听见四处都是鸟叫。想起昨夜的风雨声，不知道有多少花被打落了。",
            Appreciation = "整首诗都在床上写成：先听见鸟，再想起雨，最后惦记花。"
        },
        new Poem
        {
            Title = "小池", Author = "杨万里", Dynasty = "宋", Category = CategorySeason,
            Lines = ["泉眼无声惜细流", "树阴照水爱晴柔", "小荷才露尖尖角", "早有蜻蜓立上头"],
            Notes =
            [
                "泉眼 —— 泉水的出口",
                "惜细流 —— 好像舍不得让细细的水流走",
                "晴柔 —— 晴天里柔和的风光",
                "尖尖角 —— 刚露出水面的小荷叶卷儿"
            ],
            Translation = "泉眼悄悄无声，像舍不得那一点点细流；树荫映在水面上，像是喜爱这晴天的柔和。小荷叶才刚露出尖尖的角，就已经有蜻蜓立在上面了。",
            Appreciation = "夏天刚开始的样子，就藏在那只先到的蜻蜓身上。"
        },
        new Poem
        {
            Title = "绝句", Author = "杜甫", Dynasty = "唐", Category = CategorySeason,
            Lines = ["两个黄鹂鸣翠柳", "一行白鹭上青天", "窗含西岭千秋雪", "门泊东吴万里船"],
            Notes =
            [
                "黄鹂 —— 黄莺，叫声很好听",
                "白鹭 —— 一种白色的大水鸟",
                "西岭 —— 成都西边的雪山",
                "东吴 —— 今江苏一带，指很远的下游"
            ],
            Translation = "两只黄鹂在翠绿的柳枝间鸣叫，一行白鹭飞上瓦蓝的天空。窗口正好框住西岭上千年不化的积雪，门前停泊着从万里之外东吴驶来的船。",
            Appreciation = "四句诗有四种颜色：黄、翠、白、青，像一幅彩色照片。"
        },
        new Poem
        {
            Title = "元日", Author = "王安石", Dynasty = "宋", Category = CategorySeason,
            Lines = ["爆竹声中一岁除", "春风送暖入屠苏", "千门万户曈曈日", "总把新桃换旧符"],
            Notes =
            [
                "元日 —— 农历正月初一，春节",
                "一岁除 —— 一年过去了",
                "屠苏 —— 一种过年时喝的药酒",
                "桃符 —— 画着神像的桃木板，后来演变成春联"
            ],
            Translation = "在噼噼啪啪的爆竹声里，旧的一年过去了；春风送来暖意，人们喝着屠苏酒。初升的太阳照亮千家万户，家家都把旧的桃符换成新的。",
            Appreciation = "古人过年的样子，和今天贴春联、放鞭炮几乎一样。"
        },
        new Poem
        {
            Title = "春日", Author = "朱熹", Dynasty = "宋", Category = CategorySeason,
            Lines = ["胜日寻芳泗水滨", "无边光景一时新", "等闲识得东风面", "万紫千红总是春"],
            Notes =
            [
                "胜日 —— 天气晴好的日子",
                "寻芳 —— 踏青赏花",
                "等闲 —— 平常、轻易",
                "东风 —— 春风"
            ],
            Translation = "在风和日丽的日子到泗水边踏青，无边无际的景色一下子都变新了。谁都能轻易认出春风的样子——那万紫千红，全都是春天。",
            Appreciation = "最后一句成了成语「万紫千红」，专指春天，也指热闹兴旺。"
        },

        // ── 童趣生活 ───────────────────────────────────
        new Poem
        {
            Title = "咏鹅", Author = "骆宾王", Dynasty = "唐", Category = CategoryChild,
            Lines = ["鹅，鹅，鹅", "曲项向天歌", "白毛浮绿水", "红掌拨清波"],
            Notes =
            [
                "曲项 —— 弯着脖子",
                "向天歌 —— 朝着天空叫",
                "拨 —— 划动"
            ],
            Translation = "鹅，鹅，鹅，弯着脖子朝天空歌唱。雪白的羽毛浮在绿水上，红红的脚掌划开清清的水波。",
            Appreciation = "相传骆宾王写这首诗时才七岁。白毛、绿水、红掌、清波，四种颜色全齐了。"
        },
        new Poem
        {
            Title = "池上", Author = "白居易", Dynasty = "唐", Category = CategoryChild,
            Lines = ["小娃撑小艇", "偷采白莲回", "不解藏踪迹", "浮萍一道开"],
            Notes =
            [
                "小艇 —— 小船",
                "偷采 —— 悄悄地去摘（这里是可爱的说法）",
                "不解 —— 不懂得",
                "浮萍 —— 浮在水面上的绿色水草"
            ],
            Translation = "一个小孩子撑着小船，偷偷去采了白莲回来。他不懂得藏起自己的踪迹，水面的浮萍被船划开，留下一条清清楚楚的水路。",
            Appreciation = "「不懂藏」才是孩子：以为没被发现，其实一路都留着证据。"
        },
        new Poem
        {
            Title = "村居", Author = "高鼎", Dynasty = "清", Category = CategoryChild,
            Lines = ["草长莺飞二月天", "拂堤杨柳醉春烟", "儿童散学归来早", "忙趁东风放纸鸢"],
            Notes =
            [
                "拂堤 —— 轻轻擦过堤岸",
                "春烟 —— 春天水边草木间升起的雾气",
                "散学 —— 放学",
                "纸鸢 —— 风筝"
            ],
            Translation = "二月里草在长、莺在飞，杨柳轻拂堤岸，好像醉在春天的烟雾里。孩子们放学回来得早，赶紧趁着东风去放风筝。",
            Appreciation = "古人放学也赶着放风筝，和今天的孩子一模一样。"
        },
        new Poem
        {
            Title = "四时田园杂兴", Author = "范成大", Dynasty = "宋", Category = CategoryChild,
            Lines = ["昼出耘田夜绩麻", "村庄儿女各当家", "童孙未解供耕织", "也傍桑阴学种瓜"],
            Notes =
            [
                "耘田 —— 在田里除草",
                "绩麻 —— 把麻搓成线",
                "未解 —— 还不懂得",
                "傍 —— 靠近"
            ],
            Translation = "白天到田里除草，夜里在家里搓麻线，村里的男男女女各有各的活儿。小孙子还不懂耕田织布，也在桑树荫下学着种瓜。",
            Appreciation = "大人在忙，小孩在学——「学种瓜」三个字把童年写活了。"
        },

        // ── 思念·送别 ─────────────────────────────────
        new Poem
        {
            Title = "静夜思", Author = "李白", Dynasty = "唐", Category = CategoryMissing,
            Lines = ["床前明月光", "疑是地上霜", "举头望明月", "低头思故乡"],
            Notes =
            [
                "疑 —— 好像、以为",
                "举头 —— 抬起头",
                "故乡 —— 老家"
            ],
            Translation = "床前洒着一片明亮的月光，好像是地上结了霜。抬起头望着天上的明月，低下头就想起了远方的家乡。",
            Appreciation = "二十个字，抬头低头两个动作，就把想家的心情说尽了。"
        },
        new Poem
        {
            Title = "赠汪伦", Author = "李白", Dynasty = "唐", Category = CategoryMissing,
            Lines = ["李白乘舟将欲行", "忽闻岸上踏歌声", "桃花潭水深千尺", "不及汪伦送我情"],
            Notes =
            [
                "将欲行 —— 正要出发",
                "踏歌 —— 一边走一边用脚打拍子唱歌",
                "桃花潭 —— 在今安徽泾县",
                "不及 —— 比不上"
            ],
            Translation = "我坐上船正要离开，忽然听见岸上传来踏歌的声音。桃花潭的水哪怕有千尺深，也比不上汪伦送我的这份情谊。",
            Appreciation = "用水的深度比情的深度，是李白最拿手的写法。"
        },
        new Poem
        {
            Title = "九月九日忆山东兄弟", Author = "王维", Dynasty = "唐", Category = CategoryMissing,
            Lines = ["独在异乡为异客", "每逢佳节倍思亲", "遥知兄弟登高处", "遍插茱萸少一人"],
            Notes =
            [
                "九月九日 —— 重阳节，古人有登高、插茱萸的习俗",
                "异乡 —— 他乡",
                "倍 —— 更加",
                "茱萸 —— 一种有香味的植物，重阳节插在身上求平安"
            ],
            Translation = "独自在他乡做客，每逢过节就更加想念亲人。遥想兄弟们今天登高的地方，人人都插上茱萸，只少了我一个。",
            Appreciation = "「每逢佳节倍思亲」是中国人过节时最常想起的一句诗。"
        },
        new Poem
        {
            Title = "相思", Author = "王维", Dynasty = "唐", Category = CategoryMissing,
            Lines = ["红豆生南国", "春来发几枝", "愿君多采撷", "此物最相思"],
            Notes =
            [
                "红豆 —— 又叫相思子，古人用来寄托思念",
                "南国 —— 南方",
                "采撷 —— 采摘",
                "君 —— 你"
            ],
            Translation = "红豆生长在南方，春天来了会发出多少新枝？希望你多采摘一些，因为这东西最能寄托思念。",
            Appreciation = "从此红豆有了另一个名字：相思豆。"
        },
        new Poem
        {
            Title = "游子吟", Author = "孟郊", Dynasty = "唐", Category = CategoryMissing,
            Lines = ["慈母手中线", "游子身上衣", "临行密密缝", "意恐迟迟归", "谁言寸草心", "报得三春晖"],
            Notes =
            [
                "游子 —— 离家远行的人",
                "临行 —— 快要出发的时候",
                "意恐 —— 担心",
                "寸草心 —— 小草的心意，比喻子女微薄的心",
                "三春晖 —— 春天三个月的阳光，比喻母爱"
            ],
            Translation = "慈母手里拿着针线，缝的是远行孩子身上的衣裳。临出发前密密地缝，怕的是孩子很久才能回来。谁说小草那一点点心意，能报答得了春天阳光般的母爱呢？",
            Appreciation = "「寸草心」和「三春晖」成了母爱的代名词。"
        },

        // ── 励志哲理 ───────────────────────────────────
        new Poem
        {
            Title = "悯农", Author = "李绅", Dynasty = "唐", Category = CategorySpirit,
            Lines = ["锄禾日当午", "汗滴禾下土", "谁知盘中餐", "粒粒皆辛苦"],
            Notes =
            [
                "锄禾 —— 给禾苗松土除草",
                "日当午 —— 太阳正当头顶，一天最热的时候",
                "餐 —— 饭食",
                "皆 —— 都"
            ],
            Translation = "农民顶着正午的太阳在田里锄草，汗水一滴滴落进禾苗下的泥土。谁知道碗里的饭，每一粒都来得这样辛苦。",
            Appreciation = "吃饭前想一想这首诗，碗里的米就不是理所当然的了。"
        },
        new Poem
        {
            Title = "登鹳雀楼", Author = "王之涣", Dynasty = "唐", Category = CategorySpirit,
            Lines = ["白日依山尽", "黄河入海流", "欲穷千里目", "更上一层楼"],
            Notes =
            [
                "鹳雀楼 —— 在今山西永济，因常有鹳雀栖息得名",
                "依山尽 —— 靠着山头慢慢落下",
                "欲穷 —— 想要看尽",
                "千里目 —— 极远处的景色"
            ],
            Translation = "太阳靠着群山缓缓落下，黄河朝着大海奔流。想要看到千里之外的景色，就要再登上一层楼。",
            Appreciation = "后两句常被用来说明：想看得更远，就要站得更高。"
        },
        new Poem
        {
            Title = "题西林壁", Author = "苏轼", Dynasty = "宋", Category = CategorySpirit,
            Lines = ["横看成岭侧成峰", "远近高低各不同", "不识庐山真面目", "只缘身在此山中"],
            Notes =
            [
                "西林壁 —— 庐山西林寺的墙壁，这首诗题在墙上",
                "岭 —— 连绵的山",
                "不识 —— 看不清、认不出",
                "缘 —— 因为"
            ],
            Translation = "从正面看是连绵的山岭，从侧面看是陡峭的山峰，远看近看、高处低处，样子都不相同。认不清庐山真正的面目，只因为自己就身处这座山中。",
            Appreciation = "换个角度看问题，往往比站在原地看着更清楚。"
        },
        new Poem
        {
            Title = "长歌行（节选）", Author = "汉乐府", Dynasty = "汉", Category = CategorySpirit,
            Lines = ["青青园中葵", "朝露待日晞", "阳春布德泽", "万物生光辉", "少壮不努力", "老大徒伤悲"],
            Notes =
            [
                "葵 —— 葵菜，一种蔬菜",
                "晞 —— 晒干",
                "布德泽 —— 布施恩惠，这里指春天给万物好处",
                "徒 —— 白白地"
            ],
            Translation = "园子里的葵菜青青的，早晨的露水等着太阳晒干。温暖的春天把恩泽洒向大地，万物都焕发出光彩。年轻力壮时不努力，等到年老就只能白白地悲伤了。",
            Appreciation = "「少壮不努力，老大徒伤悲」是从汉代一直传到今天的提醒。"
        },
        new Poem
        {
            Title = "竹石", Author = "郑燮", Dynasty = "清", Category = CategorySpirit,
            Lines = ["咬定青山不放松", "立根原在破岩中", "千磨万击还坚劲", "任尔东西南北风"],
            Notes =
            [
                "咬定 —— 紧紧抓住，形容竹根扎得牢",
                "破岩 —— 破裂的岩石",
                "坚劲 —— 坚强有力",
                "任尔 —— 任凭你"
            ],
            Translation = "竹子紧紧抓住青山不肯放松，根原本就扎在破裂的岩石里。经历千万次的磨难打击，它还是那样坚强有力，任凭你东西南北的狂风怎么吹。",
            Appreciation = "郑燮就是「扬州八怪」里的郑板桥，他画竹、也像竹。"
        }
    ];

    /// <summary>按分类筛选；category 为 null 或「全部」时返回全部。</summary>
    public static List<Poem> ByCategory(string? category)
    {
        if (string.IsNullOrEmpty(category) || category == "全部") return All;
        return All.Where(p => p.Category == category).ToList();
    }

    /// <summary>各分类的诗词数量，用于分类卡片上的计数。</summary>
    public static int CountOf(string category) => All.Count(p => p.Category == category);
}
