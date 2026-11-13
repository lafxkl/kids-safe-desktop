namespace KidsSafeDesktop.Models;

/// <summary>音频内容的播放方式。</summary>
public enum KidsAudioKind
{
    /// <summary>打包的儿歌音频文件。</summary>
    Music,

    /// <summary>打包的环境音波形，循环播放用于安睡。</summary>
    Ambient,

    /// <summary>由系统语音（TTS）逐句朗读的文本内容。</summary>
    Speech
}

/// <summary>「听一听」里的一条可播放内容。</summary>
public class KidsAudioItem
{
    public string Id { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎵";
    public string Desc { get; set; } = string.Empty;

    /// <summary>卡片主题色，构造时由所属分组统一填充。</summary>
    public string Color { get; set; } = "#FF8A65";

    /// <summary>播放方式。</summary>
    public KidsAudioKind Kind { get; set; } = KidsAudioKind.Music;

    /// <summary>Music / Ambient 使用的打包文件名（如 song_twinkle.wav）。</summary>
    public string? FileName { get; set; }

    /// <summary>在线音频地址（同家长服务器，受控）。优先于 FileName 使用。</summary>
    public string? AudioUrl { get; set; }

    /// <summary>Speech 逐句朗读的文本。</summary>
    public string[] Lines { get; set; } = [];

    /// <summary>Speech 在界面上展示的中文释义提示（不参与朗读）。</summary>
    public string Hint { get; set; } = string.Empty;

    /// <summary>朗读语言："zh" 中文 / "en" 英文。</summary>
    public string Locale { get; set; } = "zh";

    /// <summary>是否循环播放（环境音为 true）。</summary>
    public bool Loop { get; set; }
}

/// <summary>「听一听」里的一个内容分组。</summary>
public class KidsAudioGroup
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎵";
    public string Color { get; set; } = "#FF8A65";
    public string Desc { get; set; } = string.Empty;
    public List<KidsAudioItem> Items { get; set; } = [];
}

/// <summary>
/// 「听一听」离线内容库。
///
/// 设计说明：这里的所有内容都随应用打包，**不加载任何第三方网站**。
/// 原因：第三方儿童站内嵌后会出现登录墙、VIP 弹窗和不受控的推荐内容
/// （实测喜马拉雅搜索页的热门推荐里混有成人向言情小说），不适合儿童模式；
/// 且网站改版会导致链接失效。儿歌部分选用公有领域传统曲调自行合成，
/// 既无版权风险，又可在无网络环境下播放。
/// </summary>
public static class KidsAudioData
{
    public static List<KidsAudioGroup> All { get; } = Build();

    /// <summary>需要随包释放的音频文件名清单。</summary>
    public static string[] ResourceFiles =>
        All.SelectMany(g => g.Items)
           .Where(i => i.Kind != KidsAudioKind.Speech && !string.IsNullOrEmpty(i.FileName))
           .Select(i => i.FileName!)
           .Distinct()
           .ToArray();

    private static KidsAudioItem Music(string id, string file, string title, string emoji, string desc)
        => new() { Id = id, GroupKey = "song", FileName = file, Title = title, Emoji = emoji, Desc = desc, Kind = KidsAudioKind.Music };

    private static KidsAudioItem Ambient(string id, string file, string title, string emoji, string desc)
        => new() { Id = id, GroupKey = "sleep", FileName = file, Title = title, Emoji = emoji, Desc = desc, Kind = KidsAudioKind.Ambient, Loop = true };

    private static KidsAudioItem Talk(string group, string id, string title, string emoji, string desc, string hint, string[] lines, string locale = "zh")
        => new()
        {
            Id = id, GroupKey = group, Title = title, Emoji = emoji, Desc = desc,
            Hint = hint, Lines = lines, Locale = locale, Kind = KidsAudioKind.Speech
        };

    private static List<KidsAudioGroup> Build()
    {
        var groups = new List<KidsAudioGroup>();

        // ── 儿歌音乐盒 ───────────────────────────────────────────
        groups.Add(new KidsAudioGroup
        {
            Key = "song",
            Name = "儿歌音乐盒",
            Emoji = "🎵",
            Color = "#FF7043",
            Desc = "经典童谣，音乐盒音色",
            Items =
            {
                Music("twinkle",       "song_twinkle.wav",       "小星星",       "⭐", "一闪一闪亮晶晶"),
                Music("two_tigers",    "song_two_tigers.wav",    "两只老虎",     "🐯", "跑得快，真奇怪"),
                Music("birthday",      "song_happy_birthday.wav","生日快乐",     "🎂", "祝你生日快乐"),
                Music("jingle_bells",  "song_jingle_bells.wav",  "铃儿响叮当",   "🔔", "欢乐的雪橇歌"),
                Music("new_year",      "song_new_year.wav",      "新年好",       "🎉", "祝贺大家新年好"),
                Music("mary_lamb",     "song_mary_lamb.wav",     "玛丽有只小羊", "🐑", "羊毛雪白真柔软"),
                Music("london_bridge", "song_london_bridge.wav", "伦敦大桥",     "🌉", "古老的英国童谣"),
                Music("little_bee",    "song_little_bee.wav",    "小蜜蜂",       "🐝", "嗡嗡嗡，去做工"),
            }
        });

        // ── 安睡摇篮 ─────────────────────────────────────────────
        groups.Add(new KidsAudioGroup
        {
            Key = "sleep",
            Name = "安睡摇篮",
            Emoji = "🌙",
            Color = "#7E57C2",
            Desc = "循环播放，助宝宝入睡",
            Items =
            {
                Ambient("rain",        "amb_rain.wav",       "轻柔雨声", "🌧️", "沙沙的雨，安心又放松"),
                Ambient("waves",       "amb_waves.wav",      "海浪轻拍", "🌊", "一起一伏，像在海边"),
                Ambient("whitenoise",  "amb_whitenoise.wav", "暖白噪音", "☁️", "遮蔽杂音，睡得更香"),
            }
        });

        // ── 睡前故事（语音朗读）─────────────────────────────────
        groups.Add(new KidsAudioGroup
        {
            Key = "story",
            Name = "睡前故事",
            Emoji = "📚",
            Color = "#9575CD",
            Desc = "温柔的故事陪伴入眠",
            Items =
            {
                Talk("story", "brave_lion", "不挑食的小狮子", "🦁", "好好吃饭才能长得壮",
                    "教小朋友不挑食",
                    [
                        "不挑食的小狮子。",
                        "森林里有一只小狮子，名字叫毛毛。",
                        "毛毛最喜欢吃肉，一点青菜都不肯碰。",
                        "吃饭的时候，他把胡萝卜偷偷藏到盘子底下。",
                        "妈妈看见了，笑着说：毛毛，你看爸爸为什么这么有力气呀？",
                        "毛毛摇摇头。妈妈说：因为爸爸什么都吃，什么都不挑。",
                        "毛毛想了想，把胡萝卜拿了出来，小口小口地咬。",
                        "咦，原来胡萝卜甜甜的，还挺好吃呢！",
                        "从那以后，毛毛每样菜都愿意尝一尝。",
                        "他的毛越来越亮，跑起来比谁都快。",
                        "小朋友，你今天有没有尝一口新的蔬菜呢？",
                    ]),
                Talk("story", "little_star", "爱帮忙的小星星", "⭐", "帮助别人自己也快乐",
                    "教小朋友乐于助人",
                    [
                        "爱帮忙的小星星。",
                        "天上有一颗小星星，它总觉得自己太小了，什么都做不了。",
                        "有一天晚上，一只小萤火虫迷路了，急得直哭。",
                        "小星星说：别怕，我来给你照亮。",
                        "它使劲地眨呀眨，把光洒在小路上。",
                        "小萤火虫顺着亮光，很快就找到了回家的路。",
                        "谢谢你，小星星！你真亮！小萤火虫开心地说。",
                        "小星星愣了一下，原来自己这么小，也能帮上大忙。",
                        "从那以后，每天晚上它都认真地发着光。",
                        "因为呀，说不定正有人需要它这一点点亮呢。",
                        "小朋友，你今天有没有帮别人做一件小事呢？",
                    ]),
                Talk("story", "polite_bunny", "懂礼貌的小兔子", "🐰", "说谢谢和对不起",
                    "教小朋友讲礼貌",
                    [
                        "懂礼貌的小兔子。",
                        "小兔子乖乖要去外婆家送胡萝卜。",
                        "路上遇到山羊伯伯，乖乖说：山羊伯伯，早上好！",
                        "山羊伯伯笑得胡子都翘起来了：真是个好孩子。",
                        "过小桥的时候，乖乖不小心撞到了小松鼠。",
                        "他马上说：对不起，小松鼠，你有没有摔疼？",
                        "小松鼠摇摇头：没关系，是我没看路。",
                        "到了外婆家，外婆递给他一块刚烤好的饼。",
                        "乖乖双手接过来，说：谢谢外婆！",
                        "外婆摸摸他的头：我们乖乖真有礼貌。",
                        "一句早上好、对不起、谢谢你，就能让别人甜甜的。",
                    ]),
                Talk("story", "brave_seed", "勇敢的小种子", "🌱", "坚持就会有收获",
                    "教小朋友坚持不放弃",
                    [
                        "勇敢的小种子。",
                        "泥土里躺着一颗小小的种子。",
                        "四周黑黑的、湿湿的，它有点害怕。",
                        "可是它听见头顶上有鸟在唱歌，太阳好像也很暖。",
                        "小种子悄悄地把脚伸出去，变成了细细的根。",
                        "它努力地往上顶啊顶，泥土太硬了，顶得腰都酸了。",
                        "它想放弃，可是又想看看外面的世界有多大。",
                        "于是它休息了一下，又接着往上顶。",
                        "有一天，噗的一声，它钻出了地面！",
                        "阳光一下子洒满了它的小脸，暖洋洋的。",
                        "后来呀，它长成了一棵会开花的小树。",
                        "小朋友，遇到困难别急着放弃，再试一次试试看。",
                    ]),
                Talk("story", "share_umbrella", "分享的小花伞", "☂️", "分享让快乐加倍",
                    "教小朋友学会分享",
                    [
                        "分享的小花伞。",
                        "下雨啦，小花猫撑着一把红色的伞回家。",
                        "走到半路，他看见小鸭子淋得湿漉漉的。",
                        "小花猫喊：小鸭子，快到伞底下来！",
                        "小鸭子挤了进来，两个人慢慢地走。",
                        "又看见小鸡抱着脑袋在发抖。",
                        "小花猫说：来来来，伞虽然小，挤一挤就够啦。",
                        "三个小伙伴贴在一起，谁也没被淋湿。",
                        "到家以后，大家笑着说谢谢，约定明天一起玩。",
                        "小花猫想：原来分享一点，快乐会变多好多呀。",
                        "小朋友，你愿意把心爱的东西分给小伙伴吗？",
                    ]),
                Talk("story", "clean_teeth", "牙齿里的小虫子", "🪥", "早晚都要刷牙",
                    "教小朋友认真刷牙",
                    [
                        "牙齿里的小虫子。",
                        "小熊壮壮最爱吃蜂蜜蛋糕，可是他最讨厌刷牙。",
                        "晚上妈妈说去刷牙，他总是说：明天再刷吧。",
                        "有一天，牙虫一家搬进了壮壮的牙齿里。",
                        "它们开起了派对，又唱又跳，还啃起了小饼干。",
                        "哎哟！壮壮的牙突然疼了起来，疼得他直掉眼泪。",
                        "牙医伯伯检查完说：是牙虫在作怪哦。",
                        "壮壮乖乖地刷了牙，上下左右，每个面都刷到。",
                        "牙虫一家被赶跑了，再也不敢来了。",
                        "从那以后，壮壮每天早晚都认真刷牙。",
                        "他的牙齿白白的，笑起来特别好看。",
                    ]),
            }
        });

        // ── 国学启蒙（语音朗读）─────────────────────────────────
        groups.Add(new KidsAudioGroup
        {
            Key = "chinese",
            Name = "国学启蒙",
            Emoji = "🏮",
            Color = "#EF5350",
            Desc = "唐诗三百首，跟着读",
            Items =
            {
                Talk("chinese", "jingyesi", "静夜思 · 李白", "🌕", "床前明月光",
                    "唐代诗人李白，写游子在月光下思念故乡",
                    ["静夜思，李白。", "床前明月光，", "疑是地上霜。", "举头望明月，", "低头思故乡。"]),
                Talk("chinese", "yonge", "咏鹅 · 骆宾王", "🦢", "白毛浮绿水",
                    "骆宾王七岁时所作，写鹅在水中嬉戏的样子",
                    ["咏鹅，骆宾王。", "鹅，鹅，鹅，", "曲项向天歌。", "白毛浮绿水，", "红掌拨清波。"]),
                Talk("chinese", "chunxiao", "春晓 · 孟浩然", "🌸", "春眠不觉晓",
                    "写春天早晨醒来，听见处处鸟鸣",
                    ["春晓，孟浩然。", "春眠不觉晓，", "处处闻啼鸟。", "夜来风雨声，", "花落知多少。"]),
                Talk("chinese", "minnong", "悯农 · 李绅", "🌾", "粒粒皆辛苦",
                    "告诉小朋友粮食来之不易，要爱惜每一粒米",
                    ["悯农，李绅。", "锄禾日当午，", "汗滴禾下土。", "谁知盘中餐，", "粒粒皆辛苦。"]),
                Talk("chinese", "guanque", "登鹳雀楼 · 王之涣", "🏯", "更上一层楼",
                    "站得高才能看得远，鼓励不断上进",
                    ["登鹳雀楼，王之涣。", "白日依山尽，", "黄河入海流。", "欲穷千里目，", "更上一层楼。"]),
                Talk("chinese", "jiangxue", "江雪 · 柳宗元", "❄️", "独钓寒江雪",
                    "一幅安静又孤独的雪景图",
                    ["江雪，柳宗元。", "千山鸟飞绝，", "万径人踪灭。", "孤舟蓑笠翁，", "独钓寒江雪。"]),
                Talk("chinese", "xiangsi", "相思 · 王维", "🫘", "此物最相思",
                    "借红豆表达深深的思念之情",
                    ["相思，王维。", "红豆生南国，", "春来发几枝。", "愿君多采撷，", "此物最相思。"]),
                Talk("chinese", "sanzijing", "三字经 · 开篇", "📜", "人之初，性本善",
                    "传统蒙学经典开篇，讲学习与做人的道理",
                    ["三字经，开篇。", "人之初，性本善。", "性相近，习相远。", "苟不教，性乃迁。", "教之道，贵以专。"]),
            }
        });

        // ── 英语启蒙（英文语音）─────────────────────────────────
        groups.Add(new KidsAudioGroup
        {
            Key = "english",
            Name = "英语启蒙",
            Emoji = "🔤",
            Color = "#26C6DA",
            Desc = "英文原声，磨耳朵",
            Items =
            {
                Talk("english", "greetings", "Greetings 日常问候", "👋", "打招呼用语",
                    "Hello 你好 / Good morning 早上好 / Good night 晚安 / Thank you 谢谢 / See you 再见",
                    ["Greetings.", "Hello!", "Good morning!", "Good afternoon!", "Good night!", "Thank you!", "You're welcome.", "See you tomorrow!"], "en"),
                Talk("english", "animals", "Animals 动物名字", "🦁", "常见动物单词",
                    "cat 猫 / dog 狗 / rabbit 兔子 / elephant 大象 / monkey 猴子 / bird 小鸟",
                    ["Animals.", "cat", "dog", "rabbit", "elephant", "monkey", "bird", "Great!"], "en"),
                Talk("english", "colors", "Colors 颜色", "🎨", "认识颜色单词",
                    "red 红 / yellow 黄 / blue 蓝 / green 绿 / orange 橙 / purple 紫",
                    ["Colors.", "red", "yellow", "blue", "green", "orange", "purple", "Well done!"], "en"),
                Talk("english", "numbers", "Numbers 数字1到10", "🔢", "数数1-10",
                    "one 一 / two 二 / three 三 ... ten 十",
                    ["Numbers.", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "Perfect!"], "en"),
                Talk("english", "family", "Family 我的家人", "👨‍👩‍👧", "家庭成员称呼",
                    "mother 妈妈 / father 爸爸 / sister 姐妹 / brother 兄弟 / grandma 奶奶 / grandpa 爷爷",
                    ["My family.", "mother", "father", "sister", "brother", "grandma", "grandpa", "I love my family."], "en"),
                Talk("english", "actions", "Actions 动作词", "🏃", "日常动作表达",
                    "run 跑 / jump 跳 / eat 吃 / drink 喝 / sleep 睡觉 / read 读书",
                    ["Actions.", "run", "jump", "eat", "drink", "sleep", "read", "Very good!"], "en"),
            }
        });

        // ── 科普知识（语音朗读）─────────────────────────────────
        groups.Add(new KidsAudioGroup
        {
            Key = "science",
            Name = "科普知识",
            Emoji = "🔬",
            Color = "#66BB6A",
            Desc = "十万个为什么",
            Items =
            {
                Talk("science", "sky_blue", "天空为什么是蓝的？", "☁️", "光的散射小秘密",
                    "了解瑞利散射：阳光里蓝光更容易被空气散射",
                    [
                        "天空为什么是蓝色的呢？",
                        "阳光看起来是白色的，其实它藏着七种颜色。",
                        "红、橙、黄、绿、蓝、靛、紫，像一道彩虹。",
                        "当阳光穿过空气的时候，蓝色光最调皮，",
                        "它们到处乱跑，把整个天空都照亮了。",
                        "所以我们一抬头，看到的就是蓝蓝的天。",
                        "傍晚时阳光要走很长的路，蓝色都跑散了，",
                        "剩下红红的橙色光，就成了美丽的晚霞。",
                    ]),
                Talk("science", "rain", "雨是从哪里来的？", "🌧️", "水循环小科普",
                    "了解蒸发—凝结—降水的循环过程",
                    [
                        "雨是从哪里来的呢？",
                        "太阳晒着大海、小河和池塘里的水，",
                        "水就变成了看不见的水蒸气，轻轻地往上飞。",
                        "高空很冷，水蒸气冷得抱成了小水珠，",
                        "许许多多小水珠聚在一起，就变成了云。",
                        "小水珠越抱越多、越来越重，",
                        "云托不住啦，它们就掉下来，",
                        "这就是下雨。",
                        "雨水流回河里，再回到大海，又开始新的旅行。",
                    ]),
                Talk("science", "teeth", "为什么要早晚刷牙？", "🦷", "保护牙齿",
                    "牙菌斑会产酸腐蚀牙釉质，刷牙可以清除它",
                    [
                        "为什么要早晚刷牙呢？",
                        "我们吃完东西后，牙缝里会留下一点点食物屑渣。",
                        "嘴巴里的细菌最喜欢吃这些残渣了。",
                        "它们吃完以后，会排出酸酸的东西，",
                        "这些酸会一点点咬坏我们的牙齿，咬出一个小洞。",
                        "小洞变大了，牙齿就会疼得厉害。",
                        "刷牙可以把细菌和食物残渣赶跑，",
                        "所以每天早上和晚上，都要好好刷牙哦。",
                    ]),
                Talk("science", "sleep", "为什么要好好睡觉？", "😴", "睡眠的重要性",
                    "睡眠时身体修复、生长激素分泌、记忆巩固",
                    [
                        "为什么要好好睡觉呢？",
                        "白天我们跑呀跳呀，身体里的零件都用累了。",
                        "睡觉的时候，身体就开始给自己修修补补。",
                        "这时候还会分泌一种让你长高的生长激素，",
                        "所以睡得香的小朋友，长得又快又高。",
                        "而且呀，睡梦中大脑会把白天学的本领，",
                        "整整齐齐地收进记性的小仓库里。",
                        "明天一早醒来，你就会觉得精神满满啦。",
                    ]),
                Talk("science", "plants", "植物为什么要晒太阳？", "🌻", "光合作用",
                    "植物通过光合作用把阳光变成养分",
                    [
                        "植物为什么要晒太阳呢？",
                        "植物有一项了不起的本领，叫做光合作用。",
                        "它们的叶子像一块块小小的太阳能板，",
                        "能把阳光的能量抓住。",
                        "再配上喝进去的水和空气里的二氧化碳，",
                        "就做出了自己最爱吃的养分。",
                        "同时还会放出新鲜的氧气送给我们。",
                        "所以说，多晒太阳，植物才长得壮，我们也能呼吸到新鲜空气。",
                    ]),
                Talk("science", "eat_veg", "为什么要多吃蔬菜？", "🥦", "均衡营养",
                    "蔬菜提供维生素和膳食纤维，增强抵抗力",
                    [
                        "为什么要多吃蔬菜呢？",
                        "蔬菜里藏着很多本领高强的维生素小卫士。",
                        "有的帮你保护眼睛，让眼睛亮亮的；",
                        "有的帮你增强抵抗力，少生病；",
                        "还有一种叫做膳食纤维，能让肠胃顺畅地工作。",
                        "只吃肉不吃菜，身体就会缺装备，",
                        "容易感冒，也容易没力气。",
                        "所以呀，每顿饭都要给蔬菜留个位置。",
                    ]),
            }
        });

        // 卡片颜色跟随所属分组
        foreach (var g in groups)
            foreach (var it in g.Items)
                it.Color = g.Color;

        return groups;
    }
}
