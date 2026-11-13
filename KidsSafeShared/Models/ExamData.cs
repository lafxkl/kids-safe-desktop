namespace KidsSafeDesktop.Models;

using Microsoft.Maui.Graphics;

/// <summary>模拟考试的三个科目。</summary>
public enum ExamSubject
{
    Chinese,
    Math,
    English
}

/// <summary>题目类型：单选 / 填空 / 判断。三种都能自动批改。</summary>
public enum ExamQuestionKind
{
    Choice,
    Fill,
    Judge
}

public static class ExamSubjectInfo
{
    /// <summary>数据库里存的小写代号。</summary>
    public static string Code(this ExamSubject s) => s switch
    {
        ExamSubject.Chinese => "chinese",
        ExamSubject.Math => "math",
        _ => "english"
    };

    public static string Name(this ExamSubject s) => s switch
    {
        ExamSubject.Chinese => "语文",
        ExamSubject.Math => "数学",
        _ => "英语"
    };

    public static string Emoji(this ExamSubject s) => s switch
    {
        ExamSubject.Chinese => "📖",
        ExamSubject.Math => "🧮",
        _ => "🔤"
    };

    /// <summary>卡片配色（与站点其他科目的蓝/绿/橙区分开，语文用红系传统色）。</summary>
    public static string Accent(this ExamSubject s) => s switch
    {
        ExamSubject.Chinese => "#EF5350",
        ExamSubject.Math => "#29B6F6",
        _ => "#66BB6A"
    };

    public static ExamSubject? FromCode(string? code) => code switch
    {
        "chinese" => ExamSubject.Chinese,
        "math" => ExamSubject.Math,
        "english" => ExamSubject.English,
        _ => null
    };
}

/// <summary>
/// 一套模拟卷。题量由子查询实时统计，所以 <see cref="QuestionCount"/> 不需要单独维护。
/// </summary>
public class ExamPaper
{
    public int Id { get; init; }
    public int Grade { get; init; }
    public ExamSubject Subject { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Term { get; init; } = string.Empty;
    public int DurationMin { get; init; } = 60;
    public int QuestionCount { get; init; }
    public string Note { get; init; } = string.Empty;

    public string SubjectName => Subject.Name();
    public string SubjectEmoji => Subject.Emoji();

    /// <summary>卡片配色。直接给出 Color，避免 XAML 里 string → Color 的隐式转换失效。</summary>
    public Color AccentColor => Color.FromArgb(Subject.Accent());

    /// <summary>卡片副标题：上册 · 12 题 · 建议 40 分钟。</summary>
    public string Subtitle => string.IsNullOrEmpty(Term)
        ? $"{QuestionCount} 题 · 建议 {DurationMin} 分钟"
        : $"{Term} · {QuestionCount} 题 · 建议 {DurationMin} 分钟";
}

/// <summary>
/// 一道题。<see cref="Options"/> 只对选择题 / 判断题有意义；
/// 判断题在视图层统一生成「对 / 错」两个选项，库里只存正确答案。
/// </summary>
public class ExamQuestion
{
    public int Id { get; init; }
    public int PaperId { get; init; }
    public int Ordinal { get; init; }
    public ExamQuestionKind Kind { get; init; }
    public string Stem { get; init; } = string.Empty;

    /// <summary>选择题的选项文本（不含 A/B/C/D 前缀），判断题与填空题为空。</summary>
    public List<string> Options { get; init; } = new();

    /// <summary>
    /// 标准答案。选择题是字母 A-D；判断题是「对」或「错」；
    /// 填空题为若干个等价写法，用 | 分隔（如 "0.75|3/4"）。
    /// </summary>
    public string Answer { get; init; } = string.Empty;

    public string Explain { get; init; } = string.Empty;

    public string KindName => Kind switch
    {
        ExamQuestionKind.Choice => "单选题",
        ExamQuestionKind.Fill => "填空题",
        _ => "判断题"
    };

    public string KindEmoji => Kind switch
    {
        ExamQuestionKind.Choice => "🔘",
        ExamQuestionKind.Fill => "✏️",
        _ => "⚖️"
    };
}

/// <summary>交卷后的一条回顾记录。</summary>
public class ExamReviewItem
{
    public string OrdinalText { get; init; } = string.Empty;
    public string Stem { get; init; } = string.Empty;
    public string YourAnswer { get; init; } = string.Empty;
    public string CorrectAnswer { get; init; } = string.Empty;
    public string Explain { get; init; } = string.Empty;
    public bool IsCorrect { get; init; }
    public string Mark => IsCorrect ? "✅" : "❌";
    public string YourText => $"你的答案：{YourAnswer}";
    public string CorrectText => $"正确答案：{CorrectAnswer}";

    /// <summary>回顾卡片的底色：对是浅绿，错是浅红。</summary>
    public Color CardColor => IsCorrect ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE");
}
