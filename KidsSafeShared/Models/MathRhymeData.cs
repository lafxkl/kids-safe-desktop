using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>口诀内容的一项（诗歌 / 列表 / 表格）。</summary>
public class RhymeItem
{
    /// <summary>verse=儿歌或说明文字（可含换行）；list=逐条小贴士；grid=乘法/加法/除法表。</summary>
    public string Kind { get; init; } = "verse";
    public string? Title { get; init; }
    public string? Verse { get; init; }
    public List<string>? Lines { get; init; }
    public List<List<string>>? Grid { get; init; }
    public int Span { get; init; } = 9;
}

/// <summary>一个口诀主题（如乘法口诀）。</summary>
public class RhymeTopic
{
    public string Emoji { get; init; } = "📚";
    public string Title { get; init; } = string.Empty;
    public string Color { get; init; } = "#7E57C2";
    public List<RhymeItem> Items { get; init; } = new();
}

/// <summary>
/// 口诀乐园的静态内容（离线内置，启动时生成）：乘法口诀、加减法口诀、除法口诀。
/// </summary>
public static class MathRhymeData
{
    private static readonly string[] D = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    private static string Prod(int p)
    {
        if (p < 10) return D[p];
        var t = p / 10;
        var o = p % 10;
        if (t == 1) return "十" + (o == 0 ? "" : D[o]);      // 10→十, 12→十二
        return D[t] + "十" + (o == 0 ? "" : D[o]);            // 25→二十五, 20→二十
    }

    private static string Phrase(int a, int b) => D[a] + D[b] + Prod(a * b);

    public static readonly List<RhymeTopic> Topics = new()
    {
        BuildMultiplication(),
        BuildAddSub(),
        BuildDivision()
    };

    private static RhymeTopic BuildMultiplication()
    {
        // 9×9 乘法表（供查表）
        var grid = new List<List<string>>();
        for (var a = 1; a <= 9; a++)
        {
            var row = new List<string>();
            for (var b = 1; b <= 9; b++)
                row.Add($"{a}×{b}={a * b}");
            grid.Add(row);
        }

        // 小九九口诀（三角，每行为一个乘数的口诀串）
        var lines = new List<string>();
        for (var a = 1; a <= 9; a++)
        {
            var parts = new List<string>();
            for (var b = 1; b <= a; b++)
                parts.Add(Phrase(a, b));
            lines.Add(string.Join("  ", parts));
        }

        return new RhymeTopic
        {
            Emoji = "📚",
            Title = "乘法口诀",
            Color = "#8E24AA",
            Items = new()
            {
                new RhymeItem { Kind = "verse", Verse = "从「一一得一」开始，一句一句背下来，算题又快又准！" },
                new RhymeItem { Kind = "grid", Title = "九九乘法表", Grid = grid, Span = 9 },
                new RhymeItem { Kind = "verse", Title = "乘法口诀（小九九）", Verse = string.Join("\n", lines) }
            }
        };
    }

    private static RhymeTopic BuildAddSub()
    {
        // 10 以内加法表
        var add = new List<List<string>>();
        for (var a = 1; a <= 9; a++)
        {
            var row = new List<string>();
            for (var b = 1; b <= 9; b++)
                row.Add($"{a}+{b}={a + b}");
            add.Add(row);
        }

        return new RhymeTopic
        {
            Emoji = "➕",
            Title = "加减法口诀",
            Color = "#00897B",
            Items = new()
            {
                new RhymeItem
                {
                    Kind = "verse",
                    Title = "凑十歌",
                    Verse = "一九一九好朋友，\n二八二八手拉手，\n三七三七真亲密，\n四六四六一起走，\n五五凑成一双手。\n\n看到 9 想 1，看到 8 想 2，看到 7 想 3，看到 6 想 4，看到 5 想 5。"
                },
                new RhymeItem { Kind = "grid", Title = "10 以内加法表", Grid = add, Span = 9 },
                new RhymeItem
                {
                    Kind = "list",
                    Title = "减法小窍门",
                    Lines = new()
                    {
                        "减号前面叫「被减数」，减号后面叫「减数」，得数叫「差」。",
                        "从大数里去掉小数，剩下多少就是差。",
                        "10 减几：10-1=9，10-2=8，10-3=7，10-4=6，10-5=5，10-6=4，10-7=3，10-8=2，10-9=1。",
                        "9 减几：9-1=8，9-2=7，9-3=6，9-4=5，9-5=4，9-6=3，9-7=2，9-8=1。",
                        "加法和减法是好朋友：知道 3+5=8，就能算出 8-5=3、8-3=5。"
                    }
                }
            }
        };
    }

    private static RhymeTopic BuildDivision()
    {
        // 除法表（a÷b=q，其中 a=b×q）
        var div = new List<List<string>>();
        for (var q = 1; q <= 9; q++)
        {
            var row = new List<string>();
            for (var b = 1; b <= 9; b++)
                row.Add($"{b * q}÷{b}={q}");
            div.Add(row);
        }

        return new RhymeTopic
        {
            Emoji = "➗",
            Title = "除法口诀",
            Color = "#3949AB",
            Items = new()
            {
                new RhymeItem
                {
                    Kind = "verse",
                    Title = "一句话记住除法",
                    Verse = "除法是乘法的好朋友！\n会背乘法口诀，除法也就出来啦——\n因为 3×4=12，所以 12÷3=4、12÷4=3。"
                },
                new RhymeItem { Kind = "grid", Title = "除法表", Grid = div, Span = 9 },
                new RhymeItem
                {
                    Kind = "list",
                    Title = "除法小窍门",
                    Lines = new()
                    {
                        "除号前面叫「被除数」，后面叫「除数」，得数叫「商」。",
                        "从大往小试：想「几乘除数等于被除数」，那个「几」就是商。",
                        "15÷5=3 是因为 5×3=15；20÷4=5 是因为 4×5=20。",
                        "平均分最直观：把 12 块糖平均分给 3 个小朋友，每人 4 块，就是 12÷3=4。"
                    }
                }
            }
        };
    }
}
