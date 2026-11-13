using System.Collections.Generic;
using System.Linq;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 心灵感应魔法的三个等级。数值就是「能猜到的最大数字」，也决定了要用几张卡片。
/// 15 → 4 张，31 → 5 张，60 → 6 张（原版街边魔术的标准范围）。
/// </summary>
public enum MindReadLevel
{
    Easy = 15,
    Medium = 31,
    Hard = 60
}

/// <summary>
/// 「心灵感应魔法」的纯逻辑：二进制猜数。
///
/// <para>
/// <b>原理</b>：任何 1~63 的整数都能唯一写成 1、2、4、8、16、32 中若干个之和。
/// 把第 k 张卡片定义为「二进制第 k 位为 1 的数字的集合」，那么
/// 观众回答「在」就等价于该位是 1，把对应位权加起来就是原数。
/// </para>
///
/// <para>
/// <b>为什么一定是唯一解</b>：二进制表示本身唯一，且卡片定义与位权一一绑定，
/// 所以只要观众的回答完全如实，六次回答必然收敛到唯一数字——
/// 这不是概率，是可证明的。开发时不要图省事用随机数或整除技巧，
/// 那会破坏「每个回答都用到」的性质，孩子一旦发现就会觉得被骗。
/// </para>
/// </summary>
public static class MindReadEngine
{
    /// <summary>第 k 张卡（从 0 数）承载的位权，同时也是这张卡的「代号」。</summary>
    public static readonly int[] Weights = { 1, 2, 4, 8, 16, 32 };

    /// <summary>覆盖 1~max 需要几张卡片。</summary>
    public static int CardCountFor(int max)
    {
        var count = 1;
        while (count < Weights.Length && Weights[count] <= max) count++;
        return count;
    }

    public static int CardCountFor(MindReadLevel level) => CardCountFor((int)level);

    /// <summary>
    /// 生成卡片内容。第 i 张卡包含 1~max 中所有「二进制第 i 位为 1」的数字。
    /// 卡片内部按从小到大排列，方便孩子一行行找。
    /// </summary>
    public static int[][] CardsFor(int max)
    {
        var count = CardCountFor(max);
        var cards = new int[count][];

        for (var i = 0; i < count; i++)
        {
            var weight = Weights[i];
            var list = new List<int>();
            for (var n = 1; n <= max; n++)
                if ((n & weight) != 0) list.Add(n);
            cards[i] = list.ToArray();
        }

        return cards;
    }

    public static int[][] CardsFor(MindReadLevel level) => CardsFor((int)level);

    /// <summary>把「在 / 不在」的回答序列还原成数字。</summary>
    public static int Reveal(IReadOnlyList<bool> answers)
    {
        var number = 0;
        for (var i = 0; i < answers.Count && i < Weights.Length; i++)
            if (answers[i]) number += Weights[i];
        return number;
    }

    /// <summary>把一个数字拆成位权（降序），例如 37 → 32, 4, 1。</summary>
    public static List<int> Decompose(int number)
    {
        var parts = new List<int>();
        for (var i = 0; i < Weights.Length; i++)
            if ((number & Weights[i]) != 0) parts.Add(Weights[i]);
        parts.Reverse();
        return parts;
    }

    /// <summary>「32 + 4 + 1」这样的算式文本。</summary>
    public static string SumText(int number)
    {
        var parts = Decompose(number);
        return parts.Count == 0 ? "0" : string.Join(" + ", parts);
    }

    /// <summary>
    /// 回答是否可能来自一个合法数字。
    /// 全答「不在」等于 0，或超过本等级上限，都说明中途记混了。
    /// </summary>
    public static bool IsPlausible(int number, MindReadLevel level)
        => number >= 1 && number <= (int)level;

    public static string LevelName(MindReadLevel level) => level switch
    {
        MindReadLevel.Easy => "小魔法",
        MindReadLevel.Medium => "中魔法",
        _ => "大魔法"
    };

    public static string LevelRangeText(MindReadLevel level)
        => $"1 ~ {(int)level} · {CardCountFor(level)} 张卡片";

    /// <summary>
    /// 给孩子的解释：把六张卡片说成「代号 1、2、4、8、16、32 的卡片」。
    /// 刻意不提「二进制」三个字作为主词——3~8 岁听不懂，
    /// 但「代号相加」这个说法当场就能验证，孩子会自己去试。
    /// </summary>
    public static string SecretIntro(MindReadLevel level)
        => $"每张卡片都有一个代号：1、2、4、8、16、32。" +
           $"你想的数字在哪几张卡片上，就把那几张的代号加起来。" +
           $"{(int)level} 以内最多用 {CardCountFor(level)} 张卡，所以一定加得出来。";
}
