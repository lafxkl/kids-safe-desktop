namespace KidsSafeDesktop.Models;

/// <summary>
/// 一个「可以合法免费看绘本」的来源（公益图书馆 / 基金会项目 / 开放目录）。
///
/// 为什么是内置清单而不是服务端下发：
/// 1) 条目只有几条，且都是机构官网，变化以年计，热更新收益接近零；
/// 2) 这几家都要联网才能看，清单本身离线可读就够，不需要把内容搬进 App；
/// 3) 走服务端下发要多一套接口 + 四端解析 + 缓存失效，维护成本远大于收益。
/// 真需要更新时随版本发（或走已有的 dll 增量更新），与其它内置内容一致。
/// </summary>
public sealed class ReadingSource
{
    public required string Id { get; init; }

    public required string Emoji { get; init; }

    public required string Name { get; init; }

    public required string Url { get; init; }

    /// <summary>语言与形式，例如「英文（真人朗读）」。</summary>
    public required string Lang { get; init; }

    /// <summary>适合年龄，写成「4–10 岁」。</summary>
    public required string Ages { get; init; }

    /// <summary>这是什么、谁做的。</summary>
    public required string What { get; init; }

    /// <summary>具体怎么用，写成能照着做的两步。</summary>
    public required string How { get; init; }

    /// <summary>使用边界：不提供下载、需要注册、只是视频等等。</summary>
    public required string Note { get; init; }
}

/// <summary>一本常被问到的经典绘本，以及「去哪里能合法看到」。</summary>
public sealed class BookPick
{
    public required string Title { get; init; }

    public required string Original { get; init; }

    public required string Author { get; init; }

    /// <summary>一句推荐，讲清楚这本书好在哪，不是营销话术。</summary>
    public required string Why { get; init; }

    /// <summary>检索/借阅地址（只指向官方目录，不指向任何 PDF 直链）。</summary>
    public required string SearchUrl { get; init; }
}

/// <summary>
/// 关爱中心的「绘本怎么找」。
///
/// 写作原则：
/// 1) 只列明确允许免费公开访问的来源，绝不收录网盘合集与扫描版 PDF；
/// 2) 说清楚每个来源「是什么形式」（在线阅读 / 朗读视频 / 借阅），别让家长点进去才发现不是下载；
/// 3) 对受版权保护的经典绘本，给的是「去哪借、去哪买、去哪查馆藏」，不给下载地址；
/// 4) 明确告诉家长网盘合集的风险，而不是含糊地说「注意版权」。
/// </summary>
public static class ReadingCatalog
{
    /// <summary>清单修订日期。官网 care-sources.html 上写的是同一天。</summary>
    public const string UpdatedAt = "2026-11-24";

    public static readonly ReadingSource[] Sources =
    {
        new()
        {
            Id = "icdl",
            Emoji = "🌍",
            Name = "国际儿童数字图书馆",
            Url = "https://en.childrenslibrary.org/",
            Lang = "多语种（含少量中文）",
            Ages = "3–13 岁",
            What = "ICDL 基金会做的公益数字图书馆，把各国童书扫描后在网页上直接翻开看，可以按年龄、主题、语言筛选。",
            How = "进站先选语言，再选年龄；不用注册、不用装软件，点封面就能一页页翻。",
            Note = "只能在线阅读，不提供下载；不少书出版年代较早，画风偏旧，但内容干净、没有广告。"
        },
        new()
        {
            Id = "storyline",
            Emoji = "🎬",
            Name = "Storyline Online",
            Url = "https://storylineonline.net/",
            Lang = "英文（真人朗读）",
            Ages = "4–10 岁",
            What = "美国演员工会基金会（SAG-AFTRA Foundation）的公益项目：演员给孩子朗读绘本，画面就是书的插图，配字幕。",
            How = "选一本书放视频，和孩子一起听；每本还附一份活动指南，可以照着提问或做手工。",
            Note = "是「听别人读」，不是电子书，也没有中文；适合亲子共听和练听力，不适合当识字材料。"
        },
        new()
        {
            Id = "openlibrary",
            Emoji = "📚",
            Name = "Open Library",
            Url = "https://openlibrary.org/",
            Lang = "英文为主，含部分中文",
            Ages = "全年龄",
            What = "Internet Archive 的开放图书目录，收录上千万条书目：公版书可以直接读全文，受版权保护的书可以线上借阅。",
            How = "搜书名或作者，看页面上有没有「Read」或「Borrow」按钮；借阅要注册一个免费账号，借期 1 小时，可以续借。",
            Note = "不是每本都有电子版，标着「Borrow」的才能借；借阅和全文阅读都要联网。"
        }
    };

    /// <summary>
    /// 家长最常问的几本。全部是受版权保护的作品 —— 这里只给「查馆藏 / 借阅」的入口，
    /// 不给任何 PDF 直链，也不建议去找网盘版。
    /// </summary>
    public static readonly BookPick[] Picks =
    {
        new()
        {
            Title = "猜猜我有多爱你",
            Original = "Guess How Much I Love You",
            Author = "Sam McBratney",
            Why = "睡前读最合适。读完可以和孩子比一比「谁的爱更多」，比说十遍「我爱你」都管用。",
            SearchUrl = "https://openlibrary.org/search?q=Guess+How+Much+I+Love+You"
        },
        new()
        {
            Title = "好饿的毛毛虫",
            Original = "The Very Hungry Caterpillar",
            Author = "Eric Carle",
            Why = "数数、星期、认识食物，一本书能玩好几样；洞洞书版本还能用手指抠。",
            SearchUrl = "https://openlibrary.org/search?q=The+Very+Hungry+Caterpillar"
        },
        new()
        {
            Title = "棕熊，棕熊，你在看什么？",
            Original = "Brown Bear, Brown Bear, What Do You See?",
            Author = "Bill Martin Jr. / Eric Carle",
            Why = "句式重复，孩子听两遍就能跟着念，是很省力的英语启蒙入口。",
            SearchUrl = "https://openlibrary.org/search?q=Brown+Bear+Brown+Bear+What+Do+You+See"
        },
        new()
        {
            Title = "月亮，晚安",
            Original = "Goodnight Moon",
            Author = "Margaret Wise Brown",
            Why = "把房间里的东西一件件说晚安，节奏慢下来，适合当睡前仪式的一部分。",
            SearchUrl = "https://openlibrary.org/search?q=Goodnight+Moon"
        },
        new()
        {
            Title = "野兽国",
            Original = "Where the Wild Things Are",
            Author = "Maurice Sendak",
            Why = "讲怒气怎么来的、又怎么散的。孩子闹过脾气之后读，比说「别生气」有用。",
            SearchUrl = "https://openlibrary.org/search?q=Where+the+Wild+Things+Are"
        }
    };

    /// <summary>版权与使用边界。这几条会原样显示在页面底部，改文案时别删。</summary>
    public static readonly string[] Rules =
    {
        "只收「明确免费、可公开访问」的来源：公益图书馆、基金会或出版社授权的项目。不放网盘合集，也不放扫描版 PDF。",
        "网盘里常见的「620 本」「520 本经典绘本」来源大多不明，属于未授权扫描。下载和转发都可能侵权，文件里还常夹带广告或可执行程序。",
        "中文绘本优先借：城市少儿图书馆、学校图书室大多有；国家图书馆少儿馆可以线上预约后到馆取。",
        "经典绘本的平装版大多二三十元，比一次出游便宜。真想长期反复读，买一本比存一堆来源不明的文件踏实。"
    };
}
