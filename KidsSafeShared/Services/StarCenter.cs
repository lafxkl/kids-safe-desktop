using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 星星中心：全应用唯一的「奖励账本」。
///
/// 设计取舍：
/// 1. 全部本地存储（Preferences + JSON），不上传、不需要登录 —— 儿童产品先把「不采集」做到位；
/// 2. 记账只增不减（星星币不可回退），兑换家务奖励时由家务模块单独记「已兑换」，
///    避免两套账互相打架；
/// 3. 任何读写异常都静默降级为 0，绝不因为奖励系统把游戏/学习页弄崩；
/// 4. 连续天数在「第一次获得星星」时推进，同一天多次获得不重复计天。
/// </summary>
public static class StarCenter
{
    private const string KeyTotal = "star_total";
    private const string KeyTodayDate = "star_today_date";
    private const string KeyToday = "star_today";
    private const string KeyStreak = "star_streak";
    private const string KeyLastDate = "star_last_date";
    private const string KeyRecords = "star_records";
    private const string KeyBadges = "star_badges";
    private const string KeyGames = "star_games";
    private const string KeyCounts = "star_counts";
    private const string KeyFullStar = "star_full_score";

    private const int RecordLimit = 80;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    // ── 读取 ────────────────────────────────────────────────

    public static int Total => ReadInt(KeyTotal);

    /// <summary>今日获得的星星（跨天自动归零，不需要定时任务）。</summary>
    public static int Today
    {
        get
        {
            EnsureToday();
            return ReadInt(KeyToday);
        }
    }

    /// <summary>今日获得星星的次数，用于「今天已经玩了 N 局」这类文案。</summary>
    public static int TodayTimes => Recent(200).Count(r => r.Time.Date == DateTime.Today);

    public static int Streak => ReadInt(KeyStreak);

    public static List<StarRecord> Recent(int count = 20)
    {
        var list = ReadJson<List<StarRecord>>(KeyRecords) ?? new List<StarRecord>();
        return count <= 0 ? list : list.Take(count).ToList();
    }

    /// <summary>某一天获得的星星合计（心情/学习页做趋势用）。</summary>
    public static int StarsOn(DateTime day)
        => Recent(RecordLimit).Where(r => r.Time.Date == day.Date).Sum(r => r.Stars);

    /// <summary>某个来源累计获得次数（例如某款游戏玩了几局）。</summary>
    public static int CountOf(string sourceId)
    {
        var dict = ReadJson<Dictionary<string, int>>(KeyCounts) ?? new();
        return dict.TryGetValue(sourceId, out var n) ? n : 0;
    }

    /// <summary>按类别统计次数（game / learn / chore / story / care）。</summary>
    public static int CountOfKind(string kind)
        => Recent(RecordLimit).Count(r => r.Kind == kind);

    public static int DistinctGamesPlayed
    {
        get
        {
            var set = ReadJson<HashSet<string>>(KeyGames) ?? new HashSet<string>();
            return set.Count;
        }
    }

    // ── 记账 ────────────────────────────────────────────────

    /// <summary>
    /// 记一笔星星。返回累加后的账本快照与本次新解锁的勋章。
    /// </summary>
    public static AwardResult Award(
        string kind,
        string sourceId,
        string title,
        string emoji,
        int stars,
        string note = "",
        bool fullScore = false)
    {
        stars = Math.Clamp(stars, 0, 20);

        var today = DateTime.Today;
        EnsureToday();

        var isFirstToday = ReadString(KeyTodayDate) != TodayKey;
        if (isFirstToday)
        {
            Preferences.Set(KeyTodayDate, TodayKey);
            Preferences.Set(KeyToday, 0);
        }

        // 连续天数：昨天有记录 +1，断档从 1 重新算
        if (isFirstToday)
        {
            var last = ReadString(KeyLastDate);
            var streak = ReadInt(KeyStreak);
            streak = last == DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd")
                ? streak + 1
                : 1;
            Preferences.Set(KeyStreak, streak);
            Preferences.Set(KeyLastDate, TodayKey);
        }

        if (stars > 0)
        {
            Preferences.Set(KeyTotal, Total + stars);
            Preferences.Set(KeyToday, ReadInt(KeyToday) + stars);
        }

        // 记录
        var records = ReadJson<List<StarRecord>>(KeyRecords) ?? new List<StarRecord>();
        records.Insert(0, new StarRecord
        {
            Time = DateTime.Now,
            SourceId = sourceId,
            Title = title,
            Emoji = emoji,
            Stars = stars,
            Kind = kind,
            Note = note
        });
        if (records.Count > RecordLimit) records.RemoveRange(RecordLimit, records.Count - RecordLimit);
        WriteJson(KeyRecords, records);

        // 计数
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var dict = ReadJson<Dictionary<string, int>>(KeyCounts) ?? new();
            dict[sourceId] = (dict.TryGetValue(sourceId, out var n) ? n : 0) + 1;
            WriteJson(KeyCounts, dict);
        }

        if (kind == "game" && !string.IsNullOrWhiteSpace(sourceId))
        {
            var set = ReadJson<HashSet<string>>(KeyGames) ?? new HashSet<string>();
            if (set.Add(sourceId)) WriteJson(KeyGames, set);
        }

        if (fullScore)
            Preferences.Set(KeyFullStar, ReadInt(KeyFullStar) + 1);

        var newBadges = RefreshBadges();

        return new AwardResult
        {
            Stars = stars,
            Total = Total,
            Today = ReadInt(KeyToday),
            Streak = ReadInt(KeyStreak),
            IsFirstAwardToday = isFirstToday,
            NewBadges = newBadges
        };
    }

    // ── 勋章 ────────────────────────────────────────────────

    private static readonly (string Id, string Emoji, string Name, string Desc)[] BadgeDefs =
    {
        ("first",   "🌟", "第一次闪光",   "第一次获得星星"),
        ("streak3", "🔥", "连续三天",     "连续 3 天完成一件事"),
        ("streak7", "🌈", "一周不断",     "连续 7 天有收获"),
        ("streak30","🏅", "月度坚持",     "连续 30 天，很了不起"),
        ("total50", "⭐", "小星星",       "累计 50 颗星星"),
        ("total200","🌠", "星光闪闪",     "累计 200 颗星星"),
        ("total500","🌌", "银河收藏家",   "累计 500 颗星星"),
        ("total1000","👑","星星之王",     "累计 1000 颗星星"),
        ("game5",   "🎮", "游戏新手",     "玩过 5 款不同的小游戏"),
        ("game10",  "🕹️", "游戏达人",     "玩过 10 款不同的小游戏"),
        ("game19",  "🏆", "全游戏通关",   "玩过全部 19 款小游戏"),
        ("learn10", "📚", "学习小达人",   "完成 10 次学习打卡"),
        ("learn50", "🎓", "学习标兵",     "完成 50 次学习打卡"),
        ("full",    "💯", "满星一局",     "某一局拿到满星"),
        ("chore10", "🧹", "家务小帮手",   "完成 10 件家务"),
        ("story5",  "🌙", "睡前故事家",   "听完 5 个故事"),
        ("care7",   "💗", "心情记录一周", "连续记录 7 天心情")
    };

    /// <summary>全部勋章（含未解锁的进度）。</summary>
    public static List<StarBadge> Badges()
    {
        var unlockedAt = ReadJson<Dictionary<string, DateTime>>(KeyBadges) ?? new();
        var list = new List<StarBadge>();

        foreach (var (id, emoji, name, desc) in BadgeDefs)
        {
            var (done, progress) = Evaluate(id);
            list.Add(new StarBadge
            {
                Id = id,
                Emoji = emoji,
                Name = name,
                Description = desc,
                Progress = progress,
                Unlocked = done || unlockedAt.ContainsKey(id),
                UnlockedAt = unlockedAt.TryGetValue(id, out var t) ? t : null
            });
        }
        return list;
    }

    private static (bool Done, string Progress) Evaluate(string id)
    {
        var total = Total;
        var streak = Streak;
        var games = DistinctGamesPlayed;

        return id switch
        {
            "first" => (total > 0, $"{Math.Min(total, 1)} / 1 颗"),
            "streak3" => (streak >= 3, $"{Math.Min(streak, 3)} / 3 天"),
            "streak7" => (streak >= 7, $"{Math.Min(streak, 7)} / 7 天"),
            "streak30" => (streak >= 30, $"{Math.Min(streak, 30)} / 30 天"),
            "total50" => (total >= 50, $"{Math.Min(total, 50)} / 50 颗"),
            "total200" => (total >= 200, $"{Math.Min(total, 200)} / 200 颗"),
            "total500" => (total >= 500, $"{Math.Min(total, 500)} / 500 颗"),
            "total1000" => (total >= 1000, $"{Math.Min(total, 1000)} / 1000 颗"),
            "game5" => (games >= 5, $"{Math.Min(games, 5)} / 5 款"),
            "game10" => (games >= 10, $"{Math.Min(games, 10)} / 10 款"),
            "game19" => (games >= 19, $"{Math.Min(games, 19)} / 19 款"),
            "learn10" => (CountOfKind("learn") >= 10, $"{Math.Min(CountOfKind("learn"), 10)} / 10 次"),
            "learn50" => (CountOfKind("learn") >= 50, $"{Math.Min(CountOfKind("learn"), 50)} / 50 次"),
            "full" => (ReadInt(KeyFullStar) > 0, $"{Math.Min(ReadInt(KeyFullStar), 1)} / 1 次"),
            "chore10" => (CountOfKind("chore") >= 10, $"{Math.Min(CountOfKind("chore"), 10)} / 10 件"),
            "story5" => (CountOfKind("story") >= 5, $"{Math.Min(CountOfKind("story"), 5)} / 5 个"),
            "care7" => (CountOfKind("care") >= 7, $"{Math.Min(CountOfKind("care"), 7)} / 7 天"),
            _ => (false, "")
        };
    }

    /// <summary>重算并落盘新解锁的勋章，返回本次新解锁的列表（用于弹窗庆祝）。</summary>
    private static List<StarBadge> RefreshBadges()
    {
        var unlockedAt = ReadJson<Dictionary<string, DateTime>>(KeyBadges) ?? new();
        var fresh = new List<StarBadge>();

        foreach (var badge in Badges())
        {
            if (!badge.Unlocked) continue;
            if (badge.UnlockedAt is not null) continue;

            unlockedAt[badge.Id] = DateTime.Now;
            badge.UnlockedAt = DateTime.Now;
            fresh.Add(badge);
        }

        if (fresh.Count > 0) WriteJson(KeyBadges, unlockedAt);
        return fresh;
    }

    /// <summary>清空全部奖励记录（家长在「我的星星」里手动操作，需二次确认）。</summary>
    public static void Reset()
    {
        foreach (var key in new[]
                 {
                     KeyTotal, KeyTodayDate, KeyToday, KeyStreak, KeyLastDate,
                     KeyRecords, KeyBadges, KeyGames, KeyCounts, KeyFullStar
                 })
        {
            try { Preferences.Remove(key); } catch { /* 忽略 */ }
        }
    }

    // ── 文案 ────────────────────────────────────────────────
    public static string KindName(string kind) => kind switch
    {
        "game" => "小游戏",
        "learn" => "学习",
        "story" => "故事",
        "chore" => "家务",
        "care" => "心情",
        _ => "成长"
    };

    /// <summary>按正确率换算星级（0–5）。没有正确率数据时给 3 星鼓励。</summary>
    public static int StarsFromRatio(double ratio)
    {
        if (double.IsNaN(ratio) || ratio <= 0) return 3;
        return ratio switch
        {
            >= 0.95 => 5,
            >= 0.85 => 4,
            >= 0.65 => 3,
            >= 0.4 => 2,
            _ => 1
        };
    }

    // ── 存储工具 ────────────────────────────────────────────

    private static string TodayKey => DateTime.Today.ToString("yyyy-MM-dd");

    private static void EnsureToday()
    {
        try
        {
            if (ReadString(KeyTodayDate) != TodayKey)
                Preferences.Set(KeyToday, 0);
        }
        catch { /* 读不到就当 0 */ }
    }

    private static int ReadInt(string key)
    {
        try { return Preferences.Get(key, 0); }
        catch { return 0; }
    }

    private static string ReadString(string key)
    {
        try { return Preferences.Get(key, string.Empty); }
        catch { return string.Empty; }
    }

    private static T? ReadJson<T>(string key) where T : class
    {
        try
        {
            var raw = Preferences.Get(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return JsonSerializer.Deserialize<T>(raw, JsonOpts);
        }
        catch { return null; }
    }

    private static void WriteJson<T>(string key, T value)
    {
        try { Preferences.Set(key, JsonSerializer.Serialize(value, JsonOpts)); }
        catch { /* 存储失败不影响游戏进行 */ }
    }
}
