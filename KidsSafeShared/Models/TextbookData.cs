using System.Text.Json.Serialization;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一本电子课本。字段与服务端 <c>/api/books</c> 一一对应，
/// 页图地址由 <see cref="Services.ITextbookService"/> 拼出，不在这里存绝对路径。
/// </summary>
public class Textbook
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    /// <summary>版本代号（rj / rj54）。</summary>
    [JsonPropertyName("edition")] public string Edition { get; set; } = string.Empty;

    [JsonPropertyName("editionName")] public string EditionName { get; set; } = string.Empty;

    [JsonPropertyName("grade")] public int Grade { get; set; }

    /// <summary>1 = 上册，2 = 下册。</summary>
    [JsonPropertyName("term")] public int Term { get; set; }

    [JsonPropertyName("termName")] public string TermName { get; set; } = string.Empty;

    /// <summary>教材年份，如「2024秋版」；老版本可能为空。</summary>
    [JsonPropertyName("year")] public string Year { get; set; } = string.Empty;

    /// <summary>旧版存档：同年级同册有更新的版本时，旧的这册只作对照，界面上弱化显示。</summary>
    [JsonPropertyName("legacy")] public bool Legacy { get; set; }

    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;

    [JsonPropertyName("pages")] public int Pages { get; set; }

    /// <summary>封面（就是第 1 页）在服务端的相对路径。</summary>
    [JsonPropertyName("cover")] public string Cover { get; set; } = string.Empty;

    [JsonPropertyName("pdfUrl")] public string PdfUrl { get; set; } = string.Empty;

    /// <summary>科目代号：chinese / math / english。</summary>
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("subjectName")] public string SubjectName { get; set; } = string.Empty;

    /// <summary>卡片副标题，如「2024秋版 · 118 页」。</summary>
    public string SubTitle => string.IsNullOrEmpty(Year)
        ? $"{Pages} 页"
        : $"{Year} · {Pages} 页";
}

/// <summary>科目（语文 / 数学 / 英语 …）。</summary>
public class TextbookSubject
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("icon")] public string Icon { get; set; } = "📚";

    /// <summary>科目芯片上的文字，如「📖 语文」。</summary>
    public string ChipText => $"{Icon} {Name}";
}

/// <summary>一个教材版本（人教版 / 北师大版 / 沪教版五四制 ……），归属某个科目。</summary>
public class TextbookEdition
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("short")] public string Short { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("subjectName")] public string SubjectName { get; set; } = string.Empty;
    [JsonPropertyName("maxGrade")] public int MaxGrade { get; set; } = 6;
    [JsonPropertyName("books")] public List<Textbook> Books { get; set; } = new();

    /// <summary>切换芯片上的短名，如「人教」「北师大」。服务端没给就退回全名。</summary>
    public string ShortName => string.IsNullOrWhiteSpace(Short) ? Name : Short;
}

/// <summary>服务端返回的整个书架。</summary>
public class TextbookShelf
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("current")] public int Current { get; set; }
    [JsonPropertyName("subjects")] public List<TextbookSubject> Subjects { get; set; } = new();
    [JsonPropertyName("editions")] public List<TextbookEdition> Editions { get; set; } = new();

    public int GradeCount => Editions.SelectMany(e => e.Books).Select(b => b.Grade).Distinct().Count();
}
