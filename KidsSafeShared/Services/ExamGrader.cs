using System.Globalization;
using System.Text;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 考试答案判定。三种题型规则不同，但都要能吃下孩子的书写习惯：
/// 全角数字/字母、前后空格、句末句号、英文大小写、数值等值（0.5 与 0.50 与 .5）。
/// 判分只做「对不对」，不猜意图；多选题超出范围一律算错。
/// </summary>
public static class ExamGrader
{
    /// <summary>
    /// 判断孩子的答案是否正确。<paramref name="userAnswer"/> 对选择题是字母，
    /// 判断题是「对/错」，填空题是原始输入文本。
    /// </summary>
    public static bool IsCorrect(ExamQuestion question, string? userAnswer)
    {
        var user = Normalize(userAnswer);
        if (user.Length == 0) return false;

        return question.Kind switch
        {
            ExamQuestionKind.Fill => MatchFill(question.Answer, user),
            _ => Normalize(question.Answer) == user
        };
    }

    /// <summary>
    /// 填空题：标准答案可能给了多个等价写法（用 | 分隔），命中任意一个即算对。
    /// 先比字符串，再比数值（"0.75" 与 "3/4" 要靠人写全，所以数值兜底只处理小数写法差异）。
    /// </summary>
    private static bool MatchFill(string standard, string user)
    {
        foreach (var raw in standard.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Normalize(raw);
            if (candidate.Length == 0) continue;
            if (candidate == user) return true;

            if (TryNumber(candidate, out var a) && TryNumber(user, out var b)
                && System.Math.Abs(a - b) < 1e-6)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 归一化：全角转半角、去空白、去句末标点、英文转小写。
    /// 「−」「—」这类长得像减号的字符统一成 ASCII 减号。
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var c = ch;

            // 全角字母数字 / 全角标点 → 半角
            if (c >= '\uFF01' && c <= '\uFF5E') c = (char)(c - 0xFEE0);
            else if (c == '\u3000') continue;

            switch (c)
            {
                case '−' or '–' or '—' or '－': c = '-'; break;
                case '×' or '＊': c = '*'; break;
                case '÷': c = '/'; break;
                case '，' or '、': c = ','; break;
                case '。' or '．': c = '.'; break;
                case '：': c = ':'; break;
                case '；': c = ';'; break;
                case '「' or '」' or '“' or '”' or '"' or '\'' or '‘' or '’': continue;
                case ' ' or '\t' or '\r' or '\n': continue;
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        var result = sb.ToString();

        // 填空题答案末尾的句号经常多打，去掉；小数中间的 . 不动
        return result.TrimEnd('.');
    }

    private static bool TryNumber(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
