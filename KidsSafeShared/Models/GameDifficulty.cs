namespace KidsSafeDesktop.Models;

/// <summary>游戏难度，由家长/儿童在大厅选择，持久化在设置中。</summary>
public enum GameDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>难度的展示文字与各项参数（轮数、选项数、计分系数等）。</summary>
public static class DifficultyHelper
{
    public static string Label(this GameDifficulty d) => d switch
    {
        GameDifficulty.Easy => "简单",
        GameDifficulty.Medium => "中等",
        GameDifficulty.Hard => "困难",
        _ => "中等"
    };

    /// <summary>一轮/一局的总题数（看图识字、数一数、找颜色、听音选物）。</summary>
    public static int Rounds(this GameDifficulty d) => d switch
    {
        GameDifficulty.Easy => 6,
        GameDifficulty.Medium => 8,
        GameDifficulty.Hard => 10,
        _ => 8
    };

    /// <summary>选择题每题的选项数量。</summary>
    public static int OptionCount(this GameDifficulty d) => d switch
    {
        GameDifficulty.Easy => 3,
        GameDifficulty.Medium => 4,
        GameDifficulty.Hard => 5,
        _ => 4
    };

    /// <summary>数一数：出现的最大数量。</summary>
    public static int MaxCount(this GameDifficulty d) => d switch
    {
        GameDifficulty.Easy => 5,
        GameDifficulty.Medium => 9,
        GameDifficulty.Hard => 15,
        _ => 9
    };

    /// <summary>拼图配对：牌的对数。</summary>
    public static int PairCount(this GameDifficulty d) => d switch
    {
        GameDifficulty.Easy => 4,
        GameDifficulty.Medium => 6,
        GameDifficulty.Hard => 8,
        _ => 6
    };

    /// <summary>计分系数：难度越高，同样答对得分的权重越大。</summary>
    public static int Factor(this GameDifficulty d) => d switch
    {
        GameDifficulty.Easy => 1,
        GameDifficulty.Medium => 2,
        GameDifficulty.Hard => 3,
        _ => 2
    };
}
