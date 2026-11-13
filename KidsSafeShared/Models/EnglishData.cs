using System.Collections.Generic;
using System.Linq;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Models;

/// <summary>英语学习的年级分层：小学一年级～初中三年级（共 9 个年级）。</summary>
public enum EnglishGrade
{
    Grade1,
    Grade2,
    Grade3,
    Grade4,
    Grade5,
    Grade6,
    Grade7,
    Grade8,
    Grade9
}

/// <summary>一个英语单词卡片（英文 / 中文释义 / 配图 emoji / 音标）。</summary>
public class EnglishWord
{
    public string Word { get; init; } = string.Empty;
    public string Meaning { get; init; } = string.Empty;
    public string Emoji { get; init; } = string.Empty;
    public string Phonetic { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public EnglishGrade Grade { get; init; }
}

/// <summary>某一类单词（如「动物」「水果」），用于学习模式的分类浏览。</summary>
public class EnglishCategory
{
    public string Name { get; init; } = string.Empty;
    public List<EnglishWord> Words { get; init; } = new();
}

/// <summary>一道选择题（看英文选中文释义）。</summary>
public class EnglishQuizQuestion
{
    public EnglishWord Word { get; init; } = null!;
    public List<string> Options { get; init; } = new();
    public int CorrectIndex { get; init; }
    public bool Answered { get; set; }
    public int Chosen { get; set; } = -1;
    public bool IsCorrect => Answered && Chosen == CorrectIndex;
}

/// <summary>
/// 英语词库与出题器。
///
/// 词库本身存在 SQLite（<see cref="QuestionBank"/>）里，随包发布 seed_english.sql，
/// 覆盖一年级～初三；这里只负责「按年级取词 / 按主题分组 / 生成选择题」。
/// 选择题从同年级词库随机抽正确项 + 3 个干扰项，保证唯一正确。
/// </summary>
public static class EnglishData
{
    private static readonly System.Random R = new();

    public static string GradeTitle(this EnglishGrade g) => g switch
    {
        EnglishGrade.Grade1 => "一年级",
        EnglishGrade.Grade2 => "二年级",
        EnglishGrade.Grade3 => "三年级",
        EnglishGrade.Grade4 => "四年级",
        EnglishGrade.Grade5 => "五年级",
        EnglishGrade.Grade6 => "六年级",
        EnglishGrade.Grade7 => "初一",
        EnglishGrade.Grade8 => "初二",
        EnglishGrade.Grade9 => "初三",
        _ => "一年级"
    };

    /// <summary>该年级按主题分类的单词列表（题库不可用时返回空表，页面显示空态）。</summary>
    public static List<EnglishCategory> Categories(EnglishGrade g)
    {
        return Words(g)
            .GroupBy(w => w.Category)
            .Select(grp => new EnglishCategory { Name = grp.Key, Words = grp.ToList() })
            .ToList();
    }

    /// <summary>该年级全部单词（扁平列表）。</summary>
    public static List<EnglishWord> Words(EnglishGrade g)
        => QuestionBank.Instance.Words((int)g + 1);

    /// <summary>从同年级词库生成 count 道选择题（正确项 + 3 个不重复干扰项）。</summary>
    public static List<EnglishQuizQuestion> BuildQuiz(EnglishGrade g, int count)
    {
        var pool = Words(g).ToList();
        var result = new List<EnglishQuizQuestion>();
        if (pool.Count == 0) return result;

        // 打乱
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = R.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var take = System.Math.Min(count, pool.Count);
        for (var i = 0; i < take; i++)
        {
            var target = pool[i];
            var used = new HashSet<string> { target.Meaning };
            var options = new List<string> { target.Meaning };

            // 干扰项：先找同年级，不够再跨年级补充
            var candidates = pool.Where(x => x.Meaning != target.Meaning && !used.Contains(x.Meaning)).ToList();
            if (candidates.Count < 3)
            {
                candidates.AddRange(QuestionBank.Instance.AllWords()
                    .Where(x => x.Grade != g && x.Meaning != target.Meaning && !used.Contains(x.Meaning)));
            }

            for (var k = candidates.Count - 1; k > 0 && options.Count < 4; k--)
            {
                var c = R.Next(k + 1);
                if (!used.Contains(candidates[c].Meaning))
                {
                    used.Add(candidates[c].Meaning);
                    options.Add(candidates[c].Meaning);
                }
                (candidates[k], candidates[c]) = (candidates[c], candidates[k]);
            }

            // 打乱选项顺序
            for (var m = options.Count - 1; m > 0; m--)
            {
                var n = R.Next(m + 1);
                (options[m], options[n]) = (options[n], options[m]);
            }

            result.Add(new EnglishQuizQuestion
            {
                Word = target,
                Options = options,
                CorrectIndex = options.IndexOf(target.Meaning)
            });
        }
        return result;
    }
}
