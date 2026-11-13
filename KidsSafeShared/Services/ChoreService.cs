using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 小小家务英雄的数据层。
///
/// 与 <see cref="StarCenter"/> 的关系：
/// - 家庭积分（本页的「我的积分」）是独立账本，因为家务积分可以兑换家庭奖励，需要能扣减；
/// - 家长每确认一件家务，额外往星星中心记 1–2 颗星，用于「家务小帮手」这类勋章，
///   但不会动到家庭积分。两套账分开，才不会出现「兑换奖励把游戏星星扣掉」的怪事。
/// </summary>
public static class ChoreService
{
    private const string Key = "chore_store_v1";
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    private static ChoreStore? _store;
    private static readonly object Lock = new();

    public static ChoreStore Store
    {
        get
        {
            if (_store is not null) return _store;
            lock (Lock)
            {
                _store ??= Load();
                return _store;
            }
        }
    }

    private static ChoreStore Load()
    {
        try
        {
            var raw = Preferences.Get(Key, string.Empty);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var s = JsonSerializer.Deserialize<ChoreStore>(raw, Opts);
                if (s is not null && s.Tasks.Count > 0) return s;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Chore] 读取数据失败，使用默认值：{ex.Message}");
        }

        var fresh = new ChoreStore { Tasks = DefaultTasks(), Rewards = DefaultRewards() };
        Preferences.Set(Key, JsonSerializer.Serialize(fresh, Opts));
        return fresh;
    }

    public static void Save()
    {
        try { Preferences.Set(Key, JsonSerializer.Serialize(Store, Opts)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Chore] 保存失败：{ex.Message}"); }
    }

    private static List<ChoreTask> DefaultTasks() => new()
    {
        new() { Id = "t1",  Emoji = "🧹",  Name = "扫地",     Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t2",  Emoji = "🧽",  Name = "擦桌子",   Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t3",  Emoji = "🧸",  Name = "整理玩具", Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t4",  Emoji = "🍽️", Name = "洗碗",     Points = 10, Difficulty = ChoreDifficulty.Medium },
        new() { Id = "t5",  Emoji = "🗑️", Name = "倒垃圾",   Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t6",  Emoji = "👕",  Name = "叠衣服",   Points = 10, Difficulty = ChoreDifficulty.Medium },
        new() { Id = "t7",  Emoji = "🌸",  Name = "浇花",     Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t8",  Emoji = "🐾",  Name = "喂宠物",   Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t9",  Emoji = "📚",  Name = "整理书包", Points = 10, Difficulty = ChoreDifficulty.Medium },
        new() { Id = "t10", Emoji = "🛏️", Name = "铺床",     Points = 5,  Difficulty = ChoreDifficulty.Easy },
        new() { Id = "t11", Emoji = "🥬", Name = "洗菜", Points = 10, Difficulty = ChoreDifficulty.Medium },
        new() { Id = "t12", Emoji = "🧦", Name = "洗自己的袜子", Points = 8, Difficulty = ChoreDifficulty.Medium }
    };

    private static List<ChoreReward> DefaultRewards() => new()
    {
        new() { Id = "r1", Emoji = "📺", Name = "看 30 分钟动画片", Points = 20,  Description = "选一部喜欢的动画片" },
        new() { Id = "r2", Emoji = "🍦", Name = "吃冰淇淋",       Points = 30,  Description = "选一个口味" },
        new() { Id = "r3", Emoji = "🌳", Name = "去公园玩",       Points = 40,  Description = "户外玩耍时间" },
        new() { Id = "r4", Emoji = "🧸", Name = "选一个小玩具",   Points = 50,  Description = "去商店挑选" },
        new() { Id = "r5", Emoji = "🎮", Name = "玩 1 小时游戏",  Points = 60,  Description = "电子游戏时间" },
        new() { Id = "r6", Emoji = "🌙", Name = "周末晚睡 30 分钟", Points = 25, Description = "额外熬夜时间" },
        new() { Id = "r7", Emoji = "👫", Name = "朋友来家里玩",   Points = 80,  Description = "邀请好朋友来做客" },
        new() { Id = "r8", Emoji = "🍽️", Name = "选一家餐厅吃饭", Points = 100, Description = "全家外出就餐" }
    };

    // ── 今日状态 ────────────────────────────────────────────

    public static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    public static List<ChoreRecord> TodayRecords()
        => Store.Records.Where(r => r.Date == Today).ToList();

    public static ChoreRecord? TodayRecordOf(string taskId)
        => Store.Records.FirstOrDefault(r => r.Date == Today && r.TaskId == taskId);

    /// <summary>所有任务在今天的汇总状态（按任务 id）。</summary>
    public static Dictionary<string, ChoreRecord> TodayMap()
        => TodayRecords().GroupBy(r => r.TaskId).ToDictionary(g => g.Key, g => g.Last());

    // ── 打卡 / 审核 ────────────────────────────────────────

    public static bool Complete(string taskId)
    {
        var task = Store.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return false;

        // 同一天重复打卡：替换旧记录（含被驳回的）
        var existing = Store.Records.FirstOrDefault(r => r.Date == Today && r.TaskId == taskId);
        if (existing is not null) Store.Records.Remove(existing);

        Store.Records.Add(new ChoreRecord
        {
            Id = NewId(),
            TaskId = task.Id,
            TaskName = task.Name,
            TaskEmoji = task.Emoji,
            Points = task.Points,
            Difficulty = task.Difficulty,
            Date = Today,
            Time = DateTime.Now.ToString("HH:mm"),
            Status = ChoreStatus.Pending
        });

        Save();
        return true;
    }

    /// <summary>家长确认：积分入账 + 星星中心记一颗，并检查全勤 / 连续奖励。</summary>
    public static int Confirm(string taskId)
    {
        var rec = Store.Records.FirstOrDefault(r => r.Date == Today && r.TaskId == taskId);
        if (rec is null || rec.Status == ChoreStatus.Confirmed) return 0;

        rec.Status = ChoreStatus.Confirmed;
        rec.RejectReason = string.Empty;

        Store.Points.Current += rec.Points;
        Store.Points.Total += rec.Points;

        var bonus = 0;

        // 今日全部完成：额外 10 分
        var confirmedToday = TodayRecords().Count(r => r.Status == ChoreStatus.Confirmed);
        if (confirmedToday >= Store.Tasks.Count && Store.Tasks.Count > 0 && Store.LastBonusDate != Today)
        {
            Store.LastBonusDate = Today;
            Store.Points.Current += 10;
            Store.Points.Total += 10;
            bonus += 10;
        }

        // 连续 7 天：额外 20 分
        if (Store.LastWeekBonusDate != Today && StreakDays() >= 7)
        {
            Store.LastWeekBonusDate = Today;
            Store.Points.Current += 20;
            Store.Points.Total += 20;
            bonus += 20;
        }

        Save();

        // 星星中心：一件家务 2 颗（用任务 id 统计次数，解锁「家务小帮手」）
        StarCenter.Award("chore", rec.TaskId, rec.TaskName, rec.TaskEmoji, 2, "完成家务");

        return bonus;
    }

    public static void Reject(string taskId, string reason)
    {
        var rec = Store.Records.FirstOrDefault(r => r.Date == Today && r.TaskId == taskId);
        if (rec is null) return;
        rec.Status = ChoreStatus.Rejected;
        rec.RejectReason = string.IsNullOrWhiteSpace(reason) ? "这次还有一点没做好" : reason.Trim();
        Save();
    }

    /// <summary>连续打卡天数（只要有已确认的家务就算当天有打卡）。</summary>
    public static int StreakDays()
    {
        var days = Store.Records.Where(r => r.Status == ChoreStatus.Confirmed)
                                .Select(r => r.Day)
                                .Distinct()
                                .OrderByDescending(d => d)
                                .ToList();

        if (days.Count == 0) return 0;

        var streak = 0;
        var cursor = DateTime.Today;

        // 今天没打卡时，从昨天往前算（当天还没做完不该把连续记录清零）
        if (days[0] != cursor) cursor = cursor.AddDays(-1);

        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    // ── 兑换 ────────────────────────────────────────────────

    public static (bool Ok, string Message) Redeem(string rewardId)
    {
        var reward = Store.Rewards.FirstOrDefault(r => r.Id == rewardId);
        if (reward is null) return (false, "奖励不存在");
        if (Store.Points.Current < reward.Points)
            return (false, $"还差 {reward.Points - Store.Points.Current} 分才能兑换哦");

        Store.Points.Current -= reward.Points;
        Store.Points.Exchanged += reward.Points;

        Store.Redemptions.Add(new ChoreRedemption
        {
            Id = NewId(),
            RewardId = reward.Id,
            RewardName = reward.Name,
            RewardEmoji = reward.Emoji,
            Points = reward.Points,
            Date = Today,
            Time = DateTime.Now.ToString("HH:mm")
        });

        Save();
        return (true, $"兑换成功：{reward.Name}");
    }

    // ── 任务 / 奖励的增删改 ────────────────────────────────

    public static void SaveTask(ChoreTask task)
    {
        var idx = Store.Tasks.FindIndex(t => t.Id == task.Id);
        if (idx >= 0) Store.Tasks[idx] = task;
        else
        {
            task.Id = NewId();
            task.Custom = true;
            Store.Tasks.Add(task);
        }
        Save();
    }

    public static void DeleteTask(string id)
    {
        Store.Tasks.RemoveAll(t => t.Id == id);
        Save();
    }

    public static void SaveReward(ChoreReward reward)
    {
        var idx = Store.Rewards.FindIndex(r => r.Id == reward.Id);
        if (idx >= 0) Store.Rewards[idx] = reward;
        else
        {
            reward.Id = NewId();
            reward.Custom = true;
            Store.Rewards.Add(reward);
        }
        Save();
    }

    public static void DeleteReward(string id)
    {
        Store.Rewards.RemoveAll(r => r.Id == id);
        Save();
    }

    // ── 周报 ────────────────────────────────────────────────

    /// <summary>某一周的 7 天（周一起）。offset=0 本周，-1 上周。</summary>
    public static List<DateTime> WeekDates(int offset)
    {
        var today = DateTime.Today;
        var dow = (int)today.DayOfWeek;          // 周日 = 0
        if (dow == 0) dow = 7;
        var monday = today.AddDays(1 - dow).AddDays(offset * 7);
        return Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
    }

    public class DayStat
    {
        public DateTime Day { get; init; }
        public string WeekLabel { get; init; } = "一";
        public int Points { get; init; }
        public int Count { get; init; }
        public bool IsToday { get; init; }
    }

    public static List<DayStat> WeekStats(int offset)
    {
        var idx = 0;
        var names = new[] { "一", "二", "三", "四", "五", "六", "日" };
        var result = new List<DayStat>();

        foreach (var day in WeekDates(offset))
        {
            var key = day.ToString("yyyy-MM-dd");
            var recs = Store.Records.Where(r => r.Date == key && r.Status == ChoreStatus.Confirmed).ToList();
            result.Add(new DayStat
            {
                Day = day,
                WeekLabel = names[idx++],
                Points = recs.Sum(r => r.Points),
                Count = recs.Count,
                IsToday = day == DateTime.Today
            });
        }
        return result;
    }

    public static List<(string Name, string Emoji, int Count)> TopTasks(int offset)
    {
        var keys = WeekDates(offset).Select(d => d.ToString("yyyy-MM-dd")).ToHashSet();
        return Store.Records
            .Where(r => keys.Contains(r.Date) && r.Status == ChoreStatus.Confirmed)
            .GroupBy(r => r.TaskName)
            .Select(g => (
                Name: g.Key,
                Emoji: g.Last().TaskEmoji,
                Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToList();
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
