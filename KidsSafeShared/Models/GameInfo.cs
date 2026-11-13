namespace KidsSafeDesktop.Models;

/// <summary>
/// 内置小游戏的类型，用于决定点击后打开哪个页面。
/// </summary>
public enum GameType
{
    PictureWord,
    Counting,
    Matching,
    FindColor,
    Listen,
    LinkMatch,
    Maze,
    SafetyQuiz,
    Doodle,
    FishEat,
    Arithmetic,
    Sudoku,
    HappyLiteracy,
    PinyinLiteracy,
    EncyclopediaQuiz,
    WhackMole,
    Typing,
    Balloon,
    Go,
    MindRead,
    Tetris
}

/// <summary>
/// 一个内置小游戏的元信息（用于「小游戏乐园」入口展示）。
/// </summary>
public class GameInfo
{
    public GameType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Emoji { get; init; } = "🎮";
    public string Description { get; init; } = string.Empty;
    public string Color { get; init; } = "#7E57C2";
}
