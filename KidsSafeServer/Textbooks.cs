using System.Text.Json;
using System.Text.Json.Serialization;

namespace KidsSafeServer;

/// <summary>一本电子课本对外暴露的样子。</summary>
public sealed record Textbook(
    string Id,
    string Edition,
    string EditionName,
    int Grade,
    int Term,
    string TermName,
    string Year,
    bool Legacy,
    string Title,
    int Pages,
    string Cover,
    string PdfUrl,
    string Subject,
    string SubjectName);

/// <summary>教材版本（人教版 / 北师大版 / 沪教版五四制 ……），归属某个科目。</summary>
public sealed record TextbookEdition(string Id, string Name, string Short, string Subject, string SubjectName, int MaxGrade);

/// <summary>科目（语文 / 数学 / 英语 …）。</summary>
public sealed record TextbookSubject(string Id, string Name, string Icon);

/// <summary>某个年级下的一册书（上册 / 下册）。</summary>
public sealed record TextbookGrade(int Grade, string Title, List<Textbook> Books);

/// <summary>
/// 电子课本目录。
///
/// 数据分两层：
/// ① 人工维护的清单 <c>wwwroot/books/books.json</c>（哪本书、哪版、几年级、哪一册）；
/// ② 页面图片本身，放在 <c>wwwroot/books/pages/{id}/page-0001.jpg</c>。
///
/// 页数以磁盘上实际的图片数量为准 —— 清单写错页数不会导致客户端翻到空白页，
/// 换一本重新导出的书只要覆盖目录即可。清单按文件修改时间缓存，改完不用重启服务。
/// </summary>
public static class TextbookCatalog
{
    private static readonly object Gate = new();
    private static List<Textbook> _cache = new();
    private static DateTime _cacheStamp = DateTime.MinValue;

    /// <summary>清单里的原始条目（只描述"是哪本书"，不含页数与 URL）。</summary>
    private sealed class ManifestBook
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("edition")] public string Edition { get; set; } = "";
        [JsonPropertyName("subject")] public string Subject { get; set; } = "";
        [JsonPropertyName("grade")] public int Grade { get; set; }
        [JsonPropertyName("term")] public int Term { get; set; } = 1;
        [JsonPropertyName("year")] public string Year { get; set; } = "";
        [JsonPropertyName("legacy")] public bool Legacy { get; set; }
    }

    private sealed class Manifest
    {
        [JsonPropertyName("subjects")] public List<ManifestSubject> Subjects { get; set; } = new();
        [JsonPropertyName("editions")] public List<ManifestEdition> Editions { get; set; } = new();
        [JsonPropertyName("books")] public List<ManifestBook> Books { get; set; } = new();
    }

    private sealed class ManifestSubject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("icon")] public string Icon { get; set; } = "📚";
    }

    private sealed class ManifestEdition
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("short")] public string Short { get; set; } = "";
        [JsonPropertyName("subject")] public string Subject { get; set; } = "";
        [JsonPropertyName("maxGrade")] public int MaxGrade { get; set; } = 6;
    }

    /// <summary>没有清单时的兜底：老版本只有语文人教两个版本。</summary>
    private static readonly TextbookSubject[] FallbackSubjects =
    {
        new("chinese", "语文", "📖")
    };

    private static readonly TextbookEdition[] FallbackEditions =
    {
        new("rj", "人教版（六三制）", "人教", "chinese", "语文", 6),
        new("rj54", "人教版（五四制）", "五四", "chinese", "语文", 5)
    };

    private static string ManifestPath(string root) => Path.Combine(root, "books", "books.json");

    private static string PagesDir(string root) => Path.Combine(root, "books", "pages");

    /// <summary>全部课本，按 版次 → 年级 → 册次 → 现行版优先 排序。</summary>
    public static List<Textbook> All(string webRoot)
    {
        var path = ManifestPath(webRoot);
        var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

        lock (Gate)
        {
            if (_cacheStamp == stamp && _cache.Count > 0) return _cache;
        }

        var list = Load(webRoot, path);
        lock (Gate)
        {
            _cache = list;
            _cacheStamp = stamp;
        }
        return list;
    }

    public static Textbook? Find(string webRoot, string id) =>
        All(webRoot).FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>科目列表（语文 / 数学 …），给页面做科目标签用。</summary>
    public static List<TextbookSubject> Subjects(string webRoot)
    {
        var m = ReadManifest(ManifestPath(webRoot));
        if (m?.Subjects is { Count: > 0 })
            return m.Subjects.Select(s => new TextbookSubject(s.Id, s.Name, s.Icon)).ToList();

        // 清单是老的（没有 subjects 段）时，按现有课本反推科目，至少别让页面空着
        var books = All(webRoot);
        if (books.Count == 0) return FallbackSubjects.ToList();
        return books.Select(b => b.Subject).Distinct()
            .Select(id => new TextbookSubject(id, SubjectText(id), id == "math" ? "🔢" : "📖"))
            .ToList();
    }

    /// <summary>版本列表（含归属科目与最大年级），给页面做版本芯片用。</summary>
    public static List<TextbookEdition> Editions(string webRoot)
    {
        var m = ReadManifest(ManifestPath(webRoot));
        if (m?.Editions is { Count: > 0 })
        {
            var names = SubjectNames(m);
            return m.Editions.Select(e => new TextbookEdition(
                e.Id,
                e.Name,
                string.IsNullOrWhiteSpace(e.Short) ? e.Name : e.Short,
                string.IsNullOrWhiteSpace(e.Subject) ? "chinese" : e.Subject,
                names.TryGetValue(string.IsNullOrWhiteSpace(e.Subject) ? "chinese" : e.Subject, out var n)
                    ? n : SubjectText(e.Subject),
                e.MaxGrade)).ToList();
        }
        return FallbackEditions.ToList();
    }

    private static Manifest? ReadManifest(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path));
        }
        catch
        {
            return null;      // 清单坏了不影响已有页图，页面会走兜底
        }
    }

    private static Dictionary<string, string> SubjectNames(Manifest m) =>
        m.Subjects.Count > 0
            ? m.Subjects.ToDictionary(s => s.Id, s => s.Name)
            : new Dictionary<string, string> { ["chinese"] = "语文", ["math"] = "数学" };

    /// <summary>科目代号 → 中文名，清单里没写时的兜底。</summary>
    public static string SubjectText(string? id) => id switch
    {
        "chinese" or "yuwen" => "语文",
        "math" or "shuxue" => "数学",
        "english" or "yingyu" => "英语",
        null or "" => "语文",
        _ => id
    };

    private static List<Textbook> Load(string webRoot, string path)
    {
        var m = ReadManifest(path);
        if (m?.Books is null) return new List<Textbook>();

        var editionMap = m.Editions.ToDictionary(e => e.Id);
        var subjectNames = SubjectNames(m);
        var editionOrder = new Dictionary<string, int>();
        for (var i = 0; i < m.Editions.Count; i++) editionOrder[m.Editions[i].Id] = i;

        var subjectOrder = new Dictionary<string, int>();
        for (var i = 0; i < m.Subjects.Count; i++) subjectOrder[m.Subjects[i].Id] = i;

        var pagesRoot = PagesDir(webRoot);
        var result = new List<Textbook>(m.Books.Count);

        foreach (var b in m.Books)
        {
            if (string.IsNullOrWhiteSpace(b.Id)) continue;

            var dir = Path.Combine(pagesRoot, b.Id);
            var pages = Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "page-*.jpg").Count()
                : 0;
            if (pages == 0) continue;   // 没渲染出页图的书不进目录，避免点开是空白

            editionMap.TryGetValue(b.Edition, out var ed);

            // 书本自己没写科目就跟着版本走；老清单里两者都可能为空
            var subject = !string.IsNullOrWhiteSpace(b.Subject) ? b.Subject
                : !string.IsNullOrWhiteSpace(ed?.Subject) ? ed!.Subject
                : "chinese";

            var editionName = ed?.Name ?? b.Edition;
            var termName = b.Term == 2 ? "下册" : "上册";

            result.Add(new Textbook(
                Id: b.Id,
                Edition: b.Edition,
                EditionName: editionName,
                Grade: b.Grade,
                Term: b.Term,
                TermName: termName,
                Year: b.Year,
                Legacy: b.Legacy,
                Title: $"{GradeText(b.Grade)}{termName}",
                Pages: pages,
                Cover: $"/books/pages/{b.Id}/page-0001.jpg",
                PdfUrl: $"/books/pdf/{b.Id}.pdf",
                Subject: subject,
                SubjectName: subjectNames.TryGetValue(subject, out var sn) ? sn : SubjectText(subject)));
        }

        return result
            .OrderBy(b => subjectOrder.TryGetValue(b.Subject, out var si) ? si : 99)
            .ThenBy(b => editionOrder.TryGetValue(b.Edition, out var ei) ? ei : 99)
            .ThenBy(b => b.Grade)
            .ThenBy(b => b.Term)
            .ThenBy(b => b.Legacy)
            .ToList();
    }

    /// <summary>1 → 一年级 …… 6 → 六年级。</summary>
    public static string GradeText(int grade) => grade switch
    {
        1 => "一年级",
        2 => "二年级",
        3 => "三年级",
        4 => "四年级",
        5 => "五年级",
        6 => "六年级",
        _ => $"{grade}年级"
    };
}
