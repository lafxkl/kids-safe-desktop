using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>年级（口算练习的难度分层）：小学一年级～初中三年级。</summary>
public enum MathGrade
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

/// <summary>口算练习的题型筛选。</summary>
public enum OralTopic
{
    All,
    Add,
    Sub,
    Mul,
    Div,
    Mixed
}

/// <summary>一道口算题（出题时计算好正确答案，避免字符串解析出错）。</summary>
public class OralProblem
{
    public string Expression { get; init; } = string.Empty;

    /// <summary>正确答案。小学是整数，五～九年级可能是小数或负数。</summary>
    public double Answer { get; init; }
    public string Category { get; init; } = string.Empty;
    public bool HasRemainder { get; init; }
    public int Remainder { get; init; }

    /// <summary>正确结果的可读形式（含「余 N」说明）。</summary>
    public string AnswerText => HasRemainder
        ? $"{Format(Answer)} 余 {Remainder}"
        : Format(Answer);

    /// <summary>数字转文本：整数不带小数点，小数最多保留两位（0.50 → 0.5）。</summary>
    public static string Format(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>错题记录（用于练习结束后的批改回顾）。</summary>
public class WrongRecord
{
    public string Expression { get; init; } = string.Empty;
    public string CorrectText { get; init; } = string.Empty;
    public string YourText { get; init; } = string.Empty;
}

/// <summary>
/// 一年级～四年级数学口算出题器。
/// 按年级与题型生成符合课标难度的算式，正确答案在生成时直接计算，保证批改准确。
/// </summary>
public static class OralGenerator
{
    private static readonly System.Random R = new();
    private static int RInt(int min, int max) => R.Next(min, max + 1);

    public static string GradeTitle(this MathGrade g) => g switch
    {
        MathGrade.Grade1 => "一年级",
        MathGrade.Grade2 => "二年级",
        MathGrade.Grade3 => "三年级",
        MathGrade.Grade4 => "四年级",
        MathGrade.Grade5 => "五年级",
        MathGrade.Grade6 => "六年级",
        MathGrade.Grade7 => "初一",
        MathGrade.Grade8 => "初二",
        MathGrade.Grade9 => "初三",
        _ => "一年级"
    };

    public static List<OralProblem> Generate(MathGrade grade, OralTopic topic, int count)
    {
        var list = new List<OralProblem>(count);
        for (var i = 0; i < count; i++)
            list.Add(GenOne(grade, topic));
        return list;
    }

    private static OralProblem GenOne(MathGrade grade, OralTopic topic)
    {
        var kinds = KindsFor(grade);
        var filtered = topic switch
        {
            OralTopic.Add => kinds.FindAll(k => k.Cat.Contains("加法")),
            OralTopic.Sub => kinds.FindAll(k => k.Cat.Contains("减法")),
            OralTopic.Mul => kinds.FindAll(k => k.Cat == "乘法"),
            OralTopic.Div => kinds.FindAll(k => k.Cat.Contains("除法")),
            OralTopic.Mixed => kinds.FindAll(k => k.Cat == "混合"),
            _ => kinds
        };
        if (filtered.Count == 0) filtered = kinds;
        var kind = filtered[RInt(0, filtered.Count - 1)];
        var (expr, ans, rem) = kind.Gen();
        return new OralProblem
        {
            Expression = expr,
            Answer = ans,
            Category = kind.Cat,
            HasRemainder = rem.HasValue,
            Remainder = rem ?? 0
        };
    }

    private class Kind
    {
        public string Cat = string.Empty;
        public Func<(string expr, int ans, int? rem)> Gen = null!;
    }

    private static Kind K(string cat, Func<(string, int, int?)> gen) => new() { Cat = cat, Gen = gen };

    private static List<Kind> KindsFor(MathGrade g) => g switch
    {
        MathGrade.Grade1 => new()
        {
            K("加法(不进位)", Add20),
            K("加法", Add100NC),
            K("减法", Sub20),
            K("减法(不借位)", Sub100NB)
        },
        MathGrade.Grade2 => new()
        {
            K("加法(进位)", AddCarry),
            K("减法(退位)", SubCarry),
            K("乘法", MulTable),
            K("除法", DivTable),
            K("除法", DivRem),
            K("混合", Mix2AddMul),
            K("混合", Mix2MulAdd),
            K("混合", Mix2MulSub),
            K("混合", Mix2DivAdd)
        },
        MathGrade.Grade3 => new()
        {
            K("加法", AddBig),
            K("减法", SubBig),
            K("乘法", MulDigit),
            K("乘法", Mul2x2),
            K("除法", Div1DigitExact),
            K("除法", Div1DigitRem),
            K("混合", Mix3ParenMul),
            K("混合", Mix3AddMul)
        },
        MathGrade.Grade4 => new()
        {
            K("加法", AddBig),
            K("减法", SubBig),
            K("乘法", Mul3x2),
            K("除法", Div2DigitExact),
            K("除法", Div2DigitRem),
            K("混合", Mix4ParenMul),
            K("混合", Mix4ParenSubMul),
            K("混合", Mix4MulSub),
            K("混合", Mix4AddMul),
            K("混合", Mix4DivAdd)
        },
        _ => new() { K("加法", Add20), K("减法", Sub20) }
    };

    // ===== 一年级：20 以内 + 100 以内不进位/不借位 =====
    private static (string, int, int?) Add20()
    {
        var a = RInt(1, 19);
        var b = RInt(1, 20 - a);
        return ($"{a} + {b} = ?", a + b, null);
    }

    private static (string, int, int?) Add100NC()
    {
        var t1 = RInt(1, 8); var o1 = RInt(0, 9);
        var t2 = RInt(1, 9 - t1); var o2 = RInt(0, 9 - o1);
        var a = 10 * t1 + o1; var b = 10 * t2 + o2;
        return ($"{a} + {b} = ?", a + b, null);
    }

    private static (string, int, int?) Sub20()
    {
        var a = RInt(2, 20);
        var b = RInt(1, a);
        return ($"{a} - {b} = ?", a - b, null);
    }

    private static (string, int, int?) Sub100NB()
    {
        var ta = RInt(1, 9); var oa = RInt(0, 9);
        var tb = RInt(0, ta);
        var ob = tb < ta ? RInt(0, 9) : RInt(0, oa);
        var a = 10 * ta + oa; var b = 10 * tb + ob;
        return ($"{a} - {b} = ?", a - b, null);
    }

    // ===== 二年级：100 以内进退位、表内乘除、有余数、两步混合 =====
    private static (string, int, int?) AddCarry()
    {
        var a = RInt(10, 89);
        var b = RInt(10, 100 - a);
        return ($"{a} + {b} = ?", a + b, null);
    }

    private static (string, int, int?) SubCarry()
    {
        var a = RInt(20, 99);
        var b = RInt(11, a - 1);
        return ($"{a} - {b} = ?", a - b, null);
    }

    private static (string, int, int?) MulTable()
    {
        var a = RInt(2, 9); var b = RInt(2, 9);
        return ($"{a} × {b} = ?", a * b, null);
    }

    private static (string, int, int?) DivTable()
    {
        var q = RInt(2, 9); var b = RInt(2, 9);
        return ($"{q * b} ÷ {b} = ?", q, null);
    }

    private static (string, int, int?) DivRem()
    {
        var divisor = RInt(2, 9); var q = RInt(1, 9);
        var rem = RInt(1, divisor - 1);
        return ($"{divisor * q + rem} ÷ {divisor} = ?", q, rem);
    }

    private static (string, int, int?) Mix2AddMul()
    {
        var a = RInt(2, 9); var b = RInt(2, 9); var c = RInt(1, 20);
        return ($"{a} × {b} + {c} = ?", a * b + c, null);
    }

    private static (string, int, int?) Mix2MulAdd()
    {
        var a = RInt(2, 40); var b = RInt(2, 9); var c = RInt(1, 20);
        return ($"{a} + {b} × {c} = ?", a + b * c, null);
    }

    private static (string, int, int?) Mix2MulSub()
    {
        var a = RInt(2, 9); var b = RInt(2, 9);
        var p = a * b; var c = RInt(1, p);
        return ($"{a} × {b} - {c} = ?", p - c, null);
    }

    private static (string, int, int?) Mix2DivAdd()
    {
        var n = RInt(2, 9); var q = RInt(2, 9);
        var m = n * q; var k = RInt(1, 20);
        return ($"{m} ÷ {n} + {k} = ?", q + k, null);
    }

    // ===== 三年级：万以内加减、多位数乘、除数是一位数、两步混合 =====
    private static (string, int, int?) AddBig()
    {
        var a = RInt(100, 9999); var b = RInt(10, 9999);
        return ($"{a} + {b} = ?", a + b, null);
    }

    private static (string, int, int?) SubBig()
    {
        var a = RInt(100, 9999); var b = RInt(10, a);
        return ($"{a} - {b} = ?", a - b, null);
    }

    private static (string, int, int?) MulDigit()
    {
        var a = RInt(10, 999); var b = RInt(2, 9);
        return ($"{a} × {b} = ?", a * b, null);
    }

    private static (string, int, int?) Mul2x2()
    {
        var a = RInt(10, 99); var b = RInt(10, 99);
        return ($"{a} × {b} = ?", a * b, null);
    }

    private static (string, int, int?) Div1DigitExact()
    {
        var b = RInt(2, 9); var q = RInt(2, 99);
        return ($"{b * q} ÷ {b} = ?", q, null);
    }

    private static (string, int, int?) Div1DigitRem()
    {
        var divisor = RInt(2, 9); var q = RInt(2, 30);
        var rem = RInt(1, divisor - 1);
        return ($"{divisor * q + rem} ÷ {divisor} = ?", q, rem);
    }

    private static (string, int, int?) Mix3ParenMul()
    {
        var a = RInt(2, 40); var b = RInt(2, 40); var c = RInt(2, 9);
        return ($"({a} + {b}) × {c} = ?", (a + b) * c, null);
    }

    private static (string, int, int?) Mix3AddMul()
    {
        var a = RInt(10, 99); var b = RInt(2, 9); var c = RInt(1, 30);
        return ($"{a} + {b} × {c} = ?", a + b * c, null);
    }

    // ===== 四年级：大数、三位数乘两位数、除数是两位数、四则混合 =====
    private static (string, int, int?) Mul3x2()
    {
        var a = RInt(100, 999); var b = RInt(10, 99);
        return ($"{a} × {b} = ?", a * b, null);
    }

    private static (string, int, int?) Div2DigitExact()
    {
        var b = RInt(11, 99); var q = RInt(2, 12);
        return ($"{b * q} ÷ {b} = ?", q, null);
    }

    private static (string, int, int?) Div2DigitRem()
    {
        var divisor = RInt(11, 99); var q = RInt(2, 12);
        var rem = RInt(1, divisor - 1);
        return ($"{divisor * q + rem} ÷ {divisor} = ?", q, rem);
    }

    private static (string, int, int?) Mix4ParenMul()
    {
        var a = RInt(2, 50); var b = RInt(2, 50); var c = RInt(2, 9);
        return ($"({a} + {b}) × {c} = ?", (a + b) * c, null);
    }

    private static (string, int, int?) Mix4ParenSubMul()
    {
        var a = RInt(20, 99); var b = RInt(2, a - 1); var c = RInt(2, 9);
        return ($"({a} - {b}) × {c} = ?", (a - b) * c, null);
    }

    private static (string, int, int?) Mix4MulSub()
    {
        var a = RInt(10, 99); var b = RInt(2, 9);
        var p = a * b; var c = RInt(1, p);
        return ($"{a} × {b} - {c} = ?", p - c, null);
    }

    private static (string, int, int?) Mix4AddMul()
    {
        var a = RInt(50, 999); var b = RInt(2, 12); var c = RInt(1, 50);
        return ($"{a} + {b} × {c} = ?", a + b * c, null);
    }

    private static (string, int, int?) Mix4DivAdd()
    {
        var n = RInt(11, 99); var q = RInt(2, 12);
        var m = n * q; var k = RInt(1, 30);
        return ($"{m} ÷ {n} + {k} = ?", q + k, null);
    }
}
