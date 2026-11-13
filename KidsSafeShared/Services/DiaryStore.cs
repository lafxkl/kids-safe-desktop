using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 日记本体的本地存储。
///
/// 存在应用私有目录的 <c>diary_v1.json</c>：不上传、不同步、不需要登录。
/// **没有做文件加密**，这是有意的——同一台设备上能读到这个文件的人（root、备份、调试连接），
/// 同样也能读到代码里的密钥，加密只是给自己安全感。真正挡住人的是密码页与系统沙箱。
/// 代价要在隐私政策里说清楚：本机明文存储，手机丢了、被 root 了，日记就等同于明文。
/// </summary>
public static class DiaryStore
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    /// <summary>单本上限。够孩子写六年，也避免文件无限膨胀。</summary>
    private const int MaxEntries = 2000;

    private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "diary_v1.json");

    // ── 读 ──────────────────────────────────────────────────

    /// <summary>全部日记，按日期倒序（新的在前）。</summary>
    public static List<DiaryEntry> All()
    {
        var list = LoadRaw();
        list.Sort((a, b) => string.CompareOrdinal(
            (b.Date ?? string.Empty) + (b.Time ?? string.Empty),
            (a.Date ?? string.Empty) + (a.Time ?? string.Empty)));
        return list;
    }

    public static DiaryEntry? Get(string id)
        => LoadRaw().FirstOrDefault(e => e.Id == id);

    public static bool HasAny => LoadRaw().Count > 0;

    public static int Count => LoadRaw().Count;

    public static int ThisMonthCount()
    {
        var prefix = DateTime.Today.ToString("yyyy-MM");
        return LoadRaw().Count(e => (e.Date ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal));
    }

    public static int TotalWords() => LoadRaw().Sum(e => e.Words);

    /// <summary>连续写了几天。今天还没写时从昨天往前算，不立刻清零。</summary>
    public static int Streak()
    {
        var dates = LoadRaw()
            .Select(e => e.Date ?? string.Empty)
            .Where(d => d.Length == 10)
            .ToHashSet();

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

    /// <summary>今天是否已经写过。</summary>
    public static bool WrittenToday()
        => LoadRaw().Any(e => e.Date == DateTime.Today.ToString("yyyy-MM-dd"));

    public static List<DiaryEntry> Search(string? keyword)
    {
        var list = All();
        if (string.IsNullOrWhiteSpace(keyword)) return list;

        var k = keyword.Trim();
        return list
            .Where(e => (e.Title ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase)
                     || (e.Body ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ── 写 ──────────────────────────────────────────────────

    /// <summary>新增或覆盖一篇。返回是否真的写了盘。</summary>
    public static bool Save(DiaryEntry entry)
    {
        if (entry is null) return false;

        var list = LoadRaw();
        if (string.IsNullOrWhiteSpace(entry.Id)) entry.Id = Guid.NewGuid().ToString("n");

        if (!string.IsNullOrEmpty(entry.Body) && entry.Body.Length > DiaryCatalog.MaxBodyLength)
            entry.Body = entry.Body[..DiaryCatalog.MaxBodyLength];

        entry.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var index = list.FindIndex(e => e.Id == entry.Id);
        if (index >= 0) list[index] = entry;
        else list.Add(entry);

        if (list.Count > MaxEntries)
        {
            // 超上限时丢最旧的，而不是拒绝写入——不能让孩子正写着突然存不进去
            list.Sort((a, b) => string.CompareOrdinal(a.Date + a.Time, b.Date + b.Time));
            list.RemoveRange(0, list.Count - MaxEntries);
        }

        return Persist(list);
    }

    public static void Delete(string id)
    {
        var list = LoadRaw();
        if (list.RemoveAll(e => e.Id == id) > 0) Persist(list);
    }

    /// <summary>家长重置时才会调用：清空全部日记，无法撤销。</summary>
    public static void ClearAll()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch { /* 忽略 */ }
    }

    // ── 落盘 ────────────────────────────────────────────────

    private static List<DiaryEntry> LoadRaw()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<DiaryEntry>();
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<DiaryEntry>();
            return JsonSerializer.Deserialize<List<DiaryEntry>>(json, Opts) ?? new List<DiaryEntry>();
        }
        catch
        {
            // 文件坏了不能让整个功能打不开：当作空本子，原文件留着（不覆盖、不删除）
            return new List<DiaryEntry>();
        }
    }

    private static bool Persist(List<DiaryEntry> list)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // 先写临时文件再替换：写到一半断电不会把整本日记弄坏
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list, Opts));
            File.Move(tmp, FilePath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
