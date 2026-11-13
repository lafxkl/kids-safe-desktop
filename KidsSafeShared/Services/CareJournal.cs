using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 心情日记与「想对爸妈说的话」的本地存储。
///
/// 隐私取舍（留守儿童场景尤其重要）：
/// - 全部只写本机 Preferences，不上传、不同步、不需要登录；
/// - 不记录定位、不记录联系人、不做任何分析上报；
/// - 数据只在「心情日记」和「给爸妈的话」两个页面可见，家长面板不展示内容明细，
///   只展示「最近记录天数」——孩子需要一个可以安全放情绪的地方。
/// </summary>
public static class CareJournal
{
    private const string KeyEntries = "care_journal_v1";
    private const string KeyLetters = "care_letters_v1";

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    public static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    // ── 心情 ────────────────────────────────────────────────

    public static List<MoodEntry> All()
    {
        try
        {
            var raw = Preferences.Get(KeyEntries, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return new List<MoodEntry>();
            return JsonSerializer.Deserialize<List<MoodEntry>>(raw, Opts) ?? new List<MoodEntry>();
        }
        catch { return new List<MoodEntry>(); }
    }

    public static MoodEntry? TodayEntry() => All().FirstOrDefault(e => e.Date == Today);

    /// <summary>记录今天的心情。同一天再记录会覆盖。返回本次获得星星前的状态。</summary>
    public static (bool FirstToday, AwardResult Award) SaveMood(string moodId, string note)
    {
        var list = All();
        var firstToday = list.All(e => e.Date != Today);

        list.RemoveAll(e => e.Date == Today);
        list.Add(new MoodEntry
        {
            Date = Today,
            MoodId = moodId,
            Note = note ?? string.Empty,
            Time = DateTime.Now.ToString("HH:mm")
        });

        // 只保留最近 180 天
        var cutoff = DateTime.Today.AddDays(-180).ToString("yyyy-MM-dd");
        list = list.Where(e => string.CompareOrdinal(e.Date, cutoff) >= 0).ToList();
        list.Sort((a, b) => string.CompareOrdinal(a.Date, b.Date));

        try { Preferences.Set(KeyEntries, JsonSerializer.Serialize(list, Opts)); }
        catch { /* 存不下也不影响使用 */ }

        AwardResult award;
        if (firstToday)
        {
            award = StarCenter.Award("care", "mood", "记录今天的心情", "💗", 1, CareCatalog.MoodOf(moodId).Label);
        }
        else
        {
            // 同一天补记不再记星星，但仍然返回当前账本状态供页面展示
            award = new AwardResult
            {
                Stars = 0,
                Total = StarCenter.Total,
                Today = StarCenter.Today,
                Streak = StarCenter.Streak
            };
        }

        return (firstToday, award);
    }

    /// <summary>最近 N 天（含今天）的记录，缺的天用 null 占位。</summary>
    public static List<(DateTime Day, MoodEntry? Entry)> Recent(int days)
    {
        var list = All();
        var result = new List<(DateTime, MoodEntry?)>();
        for (var i = days - 1; i >= 0; i--)
        {
            var day = DateTime.Today.AddDays(-i);
            var key = day.ToString("yyyy-MM-dd");
            result.Add((day, list.FirstOrDefault(e => e.Date == key)));
        }
        return result;
    }

    /// <summary>连续记录天数（今天没记时从昨天往前算，不立刻清零）。</summary>
    public static int Streak()
    {
        var dates = All().Select(e => e.Date).ToHashSet();
        if (dates.Count == 0) return 0;

        var cursor = DateTime.Today;
        if (!dates.Contains(cursor.ToString("yyyy-MM-dd"))) cursor = cursor.AddDays(-1);

        var streak = 0;
        while (dates.Contains(cursor.ToString("yyyy-MM-dd")))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    public static int CountOf(string moodId) => All().Count(e => e.MoodId == moodId);

    // ── 想对爸妈说的话 ──────────────────────────────────────

    public static List<ParentLetter> Letters()
    {
        try
        {
            var raw = Preferences.Get(KeyLetters, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return new List<ParentLetter>();
            var list = JsonSerializer.Deserialize<List<ParentLetter>>(raw, Opts) ?? new List<ParentLetter>();
            list.Reverse();
            return list;
        }
        catch { return new List<ParentLetter>(); }
    }

    public static void AddLetter(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var list = Letters();
        list.Reverse();   // 恢复正序存储
        list.Add(new ParentLetter
        {
            Date = Today,
            Time = DateTime.Now.ToString("HH:mm"),
            Text = text.Trim()
        });

        if (list.Count > 100) list.RemoveRange(0, list.Count - 100);

        try { Preferences.Set(KeyLetters, JsonSerializer.Serialize(list, Opts)); }
        catch { /* 忽略 */ }
    }

    public static void DeleteLetter(int indexFromTop)
    {
        var list = Letters();
        if (indexFromTop < 0 || indexFromTop >= list.Count) return;
        var text = list[indexFromTop];
        list.RemoveAt(indexFromTop);
        list.Reverse();
        try { Preferences.Set(KeyLetters, JsonSerializer.Serialize(list, Opts)); }
        catch { /* 忽略 */ }
    }
}
