namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 阿拉伯数字转中文读数（用于口算题的语音播报，让 TTS 读出"二十三"而非"2 3"）。
/// 支持范围约 ±9999 亿，覆盖小学口算全部题型。
/// </summary>
public static class ChineseNumber
{
    private static readonly string[] D = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] U = { "", "十", "百", "千" };
    private static readonly string[] BIG = { "", "万", "亿", "万亿" };

    public static string ToChinese(int n)
    {
        if (n == 0) return "零";
        if (n < 0) return "负" + ToChinese(-n);

        var s = n.ToString();
        var len = s.Length;
        var groups = new List<(int value, int level)>();
        var g = (len + 3) / 4;
        for (var i = 0; i < g; i++)
        {
            var start = len - (i + 1) * 4;
            var end = len - i * 4;
            if (start < 0) start = 0;
            var val = int.Parse(s.Substring(start, end - start));
            groups.Add((val, g - 1 - i));
        }

        var sb = new System.Text.StringBuilder();
        foreach (var (val, level) in groups)
        {
            if (val == 0) continue;
            sb.Append(Four(val));
            if (level > 0) sb.Append(BIG[level]);
        }
        return sb.ToString();
    }

    /// <summary>转换 1~9999 的整数（组内的四位）。</summary>
    private static string Four(int n)
    {
        var c = n.ToString().ToCharArray();
        var len = c.Length;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < len; i++)
        {
            var digit = c[i] - '0';
            var pos = len - 1 - i; // 0..3
            if (digit == 0)
            {
                var afterNonZero = false;
                for (var j = i + 1; j < len; j++)
                    if (c[j] != '0') { afterNonZero = true; break; }
                if (afterNonZero && (sb.Length == 0 || sb[sb.Length - 1] != '零'))
                    sb.Append('零');
            }
            else
            {
                // "十" 开头时不说"一十"，只说"十"
                if (digit == 1 && pos == 1 && sb.Length == 0)
                    sb.Append(U[pos]);
                else
                    sb.Append(D[digit]).Append(U[pos]);
            }
        }
        return sb.ToString();
    }
}
