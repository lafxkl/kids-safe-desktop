namespace KidsSafeDesktop.Models;

/// <summary>
/// 「宝宝看 / 亲子阅」的离线种子内容。全部随应用打包，不联网也可用。
/// 内容均为适合儿童的自我保护、生活好习惯主题，文案由团队编写、可审核。
/// 在线模式下服务端可下发同类补充内容（同样只来自家长自建服务器）。
/// </summary>
public static class KidsContentSeeds
{
    // ── 宝宝看：视听故事（无视频文件时用旁白 + 海报呈现）─────────────
    public static List<ContentGroup> VideoGroups()
    {
        return new()
        {
            new ContentGroup
            {
                Key = "habit_watch",
                Name = "好习惯养成",
                Emoji = "🌈",
                Color = "#42A5F5",
                Desc = "边看边听，学做懂事的好孩子",
                Kind = MediaKind.Video,
                Items =
                {
                    Watch("w_sleep", "早睡早起身体棒", "🌙", "好好睡觉长得高",
                        "天黑了，小枕头和小被子都准备好了。",
                        "小朋友洗完脸、刷好牙，钻进暖暖的被窝。",
                        "闭上眼睛，听一听窗外的风声，呼——呼——",
                        "睡得香，第二天就有精神，长得又快又高。"),
                    Watch("w_dress", "自己穿衣不挑食", "👕", "自己的事情自己做",
                        "早上闹钟叮铃响，伸个懒腰起床啦。",
                        "小手抓着衣领，头一缩，衣服穿好啦。",
                        "裤子、袜子、小鞋子，一件一件自己来。",
                        "穿得整整齐齐，妈妈竖起大拇指夸我真棒。"),
                    Watch("w_wash", "饭前洗手讲卫生", "🧼", "把小细菌赶跑",
                        "吃饭前，卷起袖子走到水龙头边。",
                        "搓搓手心，搓搓手背，指缝也要洗干净。",
                        "哗啦啦，清水冲一冲，毛巾擦干亮晶晶。",
                        "干干净净的小手，才去拿香喷喷的饭菜。"),
                }
            },
            new ContentGroup
            {
                Key = "animal_watch",
                Name = "动物好朋友",
                Emoji = "🐾",
                Color = "#26A69A",
                Desc = "认识可爱的动物朋友",
                Kind = MediaKind.Video,
                Items =
                {
                    Watch("a_penguin", "小企鹅走路摇", "🐧", "南极的小绅士",
                        "冰天雪地里，住着穿黑西装的小企鹅。",
                        "它一摇一摆地走，像个小绅士。",
                        "扑通一声跳进海里，用翅膀当船桨游泳。",
                        "上岸后又排着队，摇摇摆摆回家去。"),
                    Watch("a_bee", "小蜜蜂嗡嗡嗡", "🐝", "勤劳的小工人",
                        "花园里飞来一只小蜜蜂，嗡嗡嗡。",
                        "它落在花心上，用小管吸甜甜的花蜜。",
                        "采满一肚子的蜜，飞回蜂巢送给伙伴。",
                        "小蜜蜂真辛苦，我们不要去打扰它。"),
                    Watch("a_cat", "小猫轻轻走", "🐱", "温柔的小宠物",
                        "我家的小猫，走路一点声音都没有。",
                        "它的脚底有软软的肉垫，像穿了棉花鞋。",
                        "它最爱在阳光下打滚，尾巴一甩一甩。",
                        "轻轻摸摸它的头，它会眯起眼睛笑。"),
                }
            }
        };
    }

    // ── 亲子阅：安全与好习惯图文 ───────────────────────────────
    public static List<ContentGroup> ArticleGroups()
    {
        return new()
        {
            new ContentGroup
            {
                Key = "safety_read",
                Name = "安全小卫士",
                Emoji = "🛡️",
                Color = "#EF5350",
                Desc = "保护自己，平安长大",
                Kind = MediaKind.Article,
                Items =
                {
                    Article("s_traffic", "过马路看红绿灯", "🚦", "红灯停，绿灯行",
                        new[] { "安全", "出行" },
                        "马路上的车跑得很快，不能乱穿。",
                        "过马路一定要走斑马线，还要看红绿灯。",
                        "红灯亮了停一停，绿灯亮了左右看清楚再过。",
                        "大手拉小手，跟着爸爸妈妈一起走最安全。"),
                    Article("s_stranger", "不跟陌生人走", "🙅", "只跟认识的人走",
                        new[] { "防拐", "自我保护" },
                        "如果有不认识的人给你糖吃、说带你去玩，要摇头拒绝。",
                        "不要告诉别人自己叫什么、家住哪里。",
                        "如果找不到爸爸妈妈，找穿制服的警察或商场阿姨帮忙。",
                        "记住：陌生人的好处，都不能要。"),
                    Article("s_water", "防溺水要牢记", "🌊", "水边不独行",
                        new[] { "防溺水", "安全" },
                        "没有大人陪着，不能去河边、池塘边玩水。",
                        "游泳要选有救生员的游泳池，还要穿好泳圈。",
                        "看到小伙伴落水，不要自己跳下去，要大声喊大人。",
                        "水边湿滑，跑跳很容易摔跤，慢慢走。"),
                    Article("s_fire", "消防安全我知道", "🔥", "火警电话 119",
                        new[] { "消防", "应急" },
                        "打火机、插座这些不能随便碰。",
                        "闻到怪味或看到烟，赶紧告诉大人并离开房间。",
                        "发生火灾弯腰捂口鼻，跟着安全出口走。",
                        "记住火警电话是 119，但平时不能乱打哦。"),
                }
            },
            new ContentGroup
            {
                Key = "habit_read",
                Name = "好习惯绘本",
                Emoji = "📚",
                Color = "#66BB6A",
                Desc = "每天进步一点点",
                Kind = MediaKind.Article,
                Items =
                {
                    Article("h_hands", "勤洗手病菌走", "🧼", "七步洗手法",
                        new[] { "卫生", "健康" },
                        "吃东西前、上厕所后、玩完泥巴都要洗手。",
                        "用流动的水和肥皂，手心手背指缝都搓到。",
                        "至少洗二十秒，相当于唱两遍生日歌的时间。",
                        "干干净净的小手，能把生病的小虫虫挡在门外。"),
                    Article("h_eyes", "保护亮眼睛", "👀", "看书姿势要端正",
                        new[] { "护眼", "习惯" },
                        "看书时眼睛离书本一尺远，不趴着也不躺着看。",
                        "看电视、玩平板的时间要短，看完远眺一会儿。",
                        "光线太暗或太亮都伤眼睛，要在明亮处看书。",
                        "多去户外跑一跑，眼睛更喜欢绿色和远方。"),
                }
            }
        };
    }

    private static ContentItem Watch(string id, string title, string emoji, string desc, params string[] lines)
        => new()
        {
            Id = id,
            GroupKey = "watch",
            Title = title,
            Emoji = emoji,
            Desc = desc,
            Kind = MediaKind.Video,
            Source = ContentItemSource.Offline,
            NarrationLines = lines
        };

    private static ContentItem Article(string id, string title, string emoji, string desc, string[] tags, params string[] body)
        => new()
        {
            Id = id,
            GroupKey = "read",
            Title = title,
            Emoji = emoji,
            Desc = desc,
            Kind = MediaKind.Article,
            Source = ContentItemSource.Offline,
            Tags = tags,
            Body = body
        };
}
