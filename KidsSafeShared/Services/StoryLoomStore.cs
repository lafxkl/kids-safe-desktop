using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 「无限想象」的本地仓库：成长数据 + 故事库 + 世界记录。
///
/// 三条硬规矩（与原版一致，也符合本项目隐私政策）：
/// 1. 只写本机 Preferences，不发网络请求、不产生账号；
/// 2. 故事库最多存 30 个，世界记录最多 100 条 / 展示 30 条，防止无限增长；
/// 3. 任何读写异常都静默降级为空数据，绝不因为成长数据把页面弄崩。
///
/// 六维数值是「倾向」而不是「分数」：每生成一个故事，想象力 / 词汇 / 表达各 +1，
/// 叙事 / 情感 / 专注按概率 +1，顶到 10 就不再涨——避免孩子为了刷满而重复生成。
/// 页面上的文案也统一写成「随共创生长」，不做横向比较。
/// </summary>
public static class StoryLoomStore
{
    private const string KeyGrowth = "loom_growth";
    private const string KeyLibrary = "loom_library";
    private const string KeyRecords = "loom_world";

    private const int LibraryLimit = 30;
    private const int RecordLimit = 100;
    private const int RecordShowLimit = 30;
    private const int HistoryLimit = 80;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private static readonly Random Rng = new();
    private static readonly object Gate = new();

    // ── 状态 ────────────────────────────────────────────────

    private sealed class GrowthState
    {
        public int Sessions { get; set; }
        public int Keywords { get; set; }
        public Dictionary<string, int> Talents { get; set; } = new();
        public List<Dictionary<string, int>> History { get; set; } = new();
    }

    private static GrowthState? _growth;
    private static List<LoomSavedStory>? _library;
    private static List<LoomRecord>? _records;

    private static GrowthState Growth
    {
        get
        {
            if (_growth is not null) return _growth;
            _growth = ReadJson<GrowthState>(KeyGrowth) ?? new GrowthState();
            foreach (var d in StoryLoomCatalog.Dimensions)
                _growth.Talents.TryAdd(d.Key, 5);
            // 修掉越界值（历史版本或手工改文件）
            foreach (var key in _growth.Talents.Keys.ToList())
                _growth.Talents[key] = Math.Clamp(_growth.Talents[key], 0, 10);
            return _growth;
        }
    }

    public static void Load()
    {
        lock (Gate)
        {
            _growth = null;
            _library = ReadJson<List<LoomSavedStory>>(KeyLibrary);
            _records = ReadJson<List<LoomRecord>>(KeyRecords);
            _ = Growth; // 触发一次清洗
        }
    }

    // ── 统计 ────────────────────────────────────────────────

    public static int StoryCount => Library.Count;

    public static int Sessions => Growth.Sessions;

    public static int RecordCount => Records.Count;

    public static int KeywordCount => Growth.Keywords;

    public static int ValueOf(string dim) => Growth.Talents.TryGetValue(dim, out var v) ? v : 5;

    /// <summary>上一个故事之前的六维快照，用于显示「▲+1」。</summary>
    public static int PreviousValueOf(string dim)
    {
        var hist = Growth.History;
        if (hist.Count < 2) return ValueOf(dim);
        return hist[^2].TryGetValue(dim, out var v) ? v : 5;
    }

    public static int DeltaOf(string dim) => ValueOf(dim) - PreviousValueOf(dim);

    /// <summary>最突出的维度（并列时按定义顺序取第一个）。只用于「今晚多聊了聊 X」，不做排名。</summary>
    public static LoomDimension? StrongestDimension()
    {
        LoomDimension? best = null;
        var bestValue = -1;
        foreach (var d in StoryLoomCatalog.Dimensions)
        {
            var v = ValueOf(d.Key);
            if (v > bestValue)
            {
                bestValue = v;
                best = d;
            }
        }
        return best;
    }

    // ── 记账 ────────────────────────────────────────────────

    /// <summary>一次共创完成后更新六维，并留下一个历史快照。</summary>
    public static void Bump(LoomStory story)
    {
        lock (Gate)
        {
            var g = Growth;
            g.Sessions++;

            void Inc(string key, int delta)
            {
                var cur = g.Talents.TryGetValue(key, out var v) ? v : 5;
                g.Talents[key] = Math.Clamp(Math.Min(10, cur + delta), 0, 10);
            }

            Inc("imagination", 1);
            Inc("vocab", 1);
            Inc("expression", 1);
            if (Rng.NextDouble() > 0.6) Inc("narrative", 1);
            if (Rng.NextDouble() > 0.6) Inc("emotion", 1);
            if (Rng.NextDouble() > 0.75) Inc("focus", 1);

            g.History.Add(new Dictionary<string, int>(g.Talents));
            if (g.History.Count > HistoryLimit) g.History.RemoveRange(0, g.History.Count - HistoryLimit);

            g.Keywords += Math.Max(0, story.KeywordCount);
            SaveGrowth();
        }
    }

    private static void SaveGrowth()
    {
        try { Preferences.Set(KeyGrowth, JsonSerializer.Serialize(Growth, JsonOpts)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Loom] 保存成长数据失败：{ex.Message}"); }
    }

    // ── 故事库 ──────────────────────────────────────────────

    public static List<LoomSavedStory> Library
    {
        get
        {
            if (_library is not null) return _library;
            _library = ReadJson<List<LoomSavedStory>>(KeyLibrary) ?? new List<LoomSavedStory>();
            _library = _library.Where(s => !string.IsNullOrWhiteSpace(s.Title)).Take(LibraryLimit).ToList();
            return _library;
        }
    }

    /// <summary>收藏故事。已经存在同样标题 + 正文时返回 false，由页面给出提示。</summary>
    public static bool AddStory(LoomStory story)
    {
        lock (Gate)
        {
            var text = story.FullText;
            if (Library.Any(s => s.Title == story.Title && s.Text == text)) return false;

            var now = DateTime.Now;
            Library.Insert(0, new LoomSavedStory
            {
                Title = story.Title,
                Mood = story.Mood,
                Text = text,
                TimeText = now.ToString("M月d日 HH:mm"),
                CreatedAt = now
            });
            if (Library.Count > LibraryLimit) Library.RemoveRange(LibraryLimit, Library.Count - LibraryLimit);
            SaveLibrary();
            return true;
        }
    }

    public static void RemoveStory(LoomSavedStory story)
    {
        lock (Gate)
        {
            Library.Remove(story);
            SaveLibrary();
        }
    }

    private static void SaveLibrary()
    {
        try { Preferences.Set(KeyLibrary, JsonSerializer.Serialize(Library, JsonOpts)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Loom] 保存故事库失败：{ex.Message}"); }
    }

    // ── 世界记录 ────────────────────────────────────────────

    public static List<LoomRecord> Records
    {
        get
        {
            if (_records is not null) return _records;
            _records = ReadJson<List<LoomRecord>>(KeyRecords) ?? new List<LoomRecord>();
            return _records;
        }
    }

    /// <summary>最近的世界记录（倒序，最多 30 条）。</summary>
    public static List<LoomRecord> RecentRecords() => Records.TakeLast(RecordShowLimit).Reverse().ToList();

    public static void PushRecord(string agentKey, string text)
    {
        lock (Gate)
        {
            var agent = StoryLoomCatalog.Agents.FirstOrDefault(a => a.Key == agentKey);
            var rec = new LoomRecord
            {
                TimeText = DateTime.Now.ToString("HH:mm"),
                Who = agentKey == "child" ? "我" : agent?.Name ?? "小伙伴",
                Text = text,
                Accent = agentKey == "child" ? "#8AB4FF" : agent?.Accent ?? "#7B6BFF",
                At = DateTime.Now
            };

            Records.Add(rec);
            if (Records.Count > RecordLimit) Records.RemoveRange(0, Records.Count - RecordLimit);

            try { Preferences.Set(KeyRecords, JsonSerializer.Serialize(Records, JsonOpts)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Loom] 保存世界记录失败：{ex.Message}"); }
        }
    }

    // ── 清空 ────────────────────────────────────────────────

    /// <summary>清空成长数据（不动故事库，收藏是孩子的东西）。</summary>
    public static void ClearGrowth()
    {
        lock (Gate)
        {
            _growth = new GrowthState();
            foreach (var d in StoryLoomCatalog.Dimensions) _growth.Talents[d.Key] = 5;
            SaveGrowth();
            _records = new List<LoomRecord>();
            try { Preferences.Remove(KeyRecords); } catch { /* 忽略 */ }
        }
    }

    public static void ClearLibrary()
    {
        lock (Gate)
        {
            _library = new List<LoomSavedStory>();
            try { Preferences.Remove(KeyLibrary); } catch { /* 忽略 */ }
        }
    }

    // ── 兜底读写 ────────────────────────────────────────────

    private static T? ReadJson<T>(string key) where T : class
    {
        try
        {
            var raw = Preferences.Get(key, "");
            return string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<T>(raw, JsonOpts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 读取 {key} 失败：{ex.Message}");
            return null;
        }
    }
}
