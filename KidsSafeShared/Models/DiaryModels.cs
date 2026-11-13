using System.Globalization;
using System.Text;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一篇日记。
///
/// 刻意只存「日期 / 心情 / 标题 / 正文」四样东西：不记定位、不记天气、不记设备型号。
/// 孩子写下的东西越多，将来泄露的面越大——功能少一点，反而更安全。
/// </summary>
public class DiaryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>写作日期 yyyy-MM-dd，用来排序与去重判断。</summary>
    public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    /// <summary>写作时刻 HH:mm。</summary>
    public string Time { get; set; } = DateTime.Now.ToString("HH:mm");

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>可选心情，空字符串表示这篇没选心情。</summary>
    public string MoodId { get; set; } = string.Empty;

    public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public MoodKind? Mood => string.IsNullOrEmpty(MoodId) ? null : CareCatalog.MoodOf(MoodId);

    public int Words => DiaryCatalog.CountWords(Body);

    /// <summary>「11 月 5 日 · 周三」这种给孩子看的日期。</summary>
    public string DateLabel
    {
        get
        {
            if (!DateTime.TryParse(Date, out var d)) return Date;
            var week = "日一二三四五六"[(int)d.DayOfWeek];
            return d.Year == DateTime.Today.Year
                ? $"{d.Month} 月 {d.Day} 日 · 周{week}"
                : $"{d.Year} 年 {d.Month} 月 {d.Day} 日 · 周{week}";
        }
    }

    public string TitleOrFallback => string.IsNullOrWhiteSpace(Title) ? "无题" : Title.Trim();

    /// <summary>列表里的预览：换行压成空格，免得一条日记把整个列表撑长。</summary>
    public string Preview
    {
        get
        {
            var text = (Body ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return string.IsNullOrWhiteSpace(text) ? "（这篇还没写内容）" : text;
        }
    }
}

/// <summary>日记用到的固定文案与几个小工具。</summary>
public static class DiaryCatalog
{
    /// <summary>不知道写什么时给的引子。孩子比大人更需要一个「开头」。</summary>
    public static readonly string[] Prompts =
    {
        "今天最开心的一件事是……",
        "今天有一点点难过，因为……",
        "如果有人能听我说，我想说……",
        "今天我做到了……（哪怕很小很小）",
        "今天我想谢谢……",
        "如果明天可以重来一次，我想……",
        "今天我最想告诉爸爸妈妈的是……",
        "有件事我一直没说出来……",
        "今天我学到的一个新东西是……",
        "我想成为那种……的人",
        "今天让我生气的是……",
        "给一个月后的自己留一句话："
    };

    private static int _promptCursor;

    /// <summary>首页那句引子：同一天永远是同一句，孩子会觉得这句是今天专属的。</summary>
    public static string PromptOfDay()
    {
        var seed = DateTime.Today.DayOfYear + DateTime.Today.Year;
        return Prompts[Math.Abs(seed) % Prompts.Length];
    }

    /// <summary>换一句，避免每次都从头开始。</summary>
    public static string NextPrompt()
    {
        _promptCursor = (_promptCursor + 1) % Prompts.Length;
        var seed = DateTime.Today.DayOfYear + DateTime.Today.Year;
        return Prompts[(Math.Abs(seed) + _promptCursor) % Prompts.Length];
    }

    /// <summary>单篇正文上限，防止一次粘贴把文件撑爆。</summary>
    public const int MaxBodyLength = 20000;

    /// <summary>
    /// 中文字数：汉字按 1 个字算，连续的英文/数字算 1 个词，标点、空白、emoji 都不算。
    ///
    /// 用 Rune 枚举而不是 char：emoji 是代理对，按 char 数会把一个 😄 算成 2 个字，
    /// 孩子写满一行表情就看到「20 字」，很出戏。
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var count = 0;
        var inLatin = false;

        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);

            var skip = Rune.IsWhiteSpace(rune) || category switch
            {
                UnicodeCategory.SpaceSeparator => true,
                UnicodeCategory.LineSeparator => true,
                UnicodeCategory.ParagraphSeparator => true,
                UnicodeCategory.Control => true,
                UnicodeCategory.Format => true,
                UnicodeCategory.DashPunctuation
                    or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
                    or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
                    or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.OtherPunctuation => true,
                UnicodeCategory.MathSymbol or UnicodeCategory.CurrencySymbol
                    or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol => true,
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                    or UnicodeCategory.EnclosingMark => true,
                _ => false
            };

            if (skip)
            {
                inLatin = false;
                continue;
            }

            if (rune.Value < 128)
            {
                if (!inLatin) { count++; inLatin = true; }
            }
            else
            {
                count++;
                inLatin = false;
            }
        }

        return count;
    }
}
