using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 全部内置小游戏的清单（大厅与家长后台共用）。
/// 新增游戏时在这里加一条，并在 GamesHubPage 的导航里加对应分支即可。
/// </summary>
public static class GameCatalog
{
    public static readonly List<GameInfo> All = new()
    {
        new GameInfo { Type = GameType.PictureWord, Title = "看图识字", Emoji = "📖", Description = "看图片，选出正确的字", Color = "#7E57C2" },
        new GameInfo { Type = GameType.Counting,     Title = "数一数",   Emoji = "🔢", Description = "数数有几个，选对数字", Color = "#26A69A" },
        new GameInfo { Type = GameType.Matching,     Title = "拼图配对", Emoji = "🧩", Description = "翻开卡片，找出相同的一对", Color = "#EF6C00" },
        new GameInfo { Type = GameType.FindColor,    Title = "找颜色",   Emoji = "🎨", Description = "听名字，找出对应的颜色", Color = "#3949AB" },
        new GameInfo { Type = GameType.Listen,       Title = "听音选物", Emoji = "🔊", Description = "听一听，选出正确的东西", Color = "#D81B60" },
        new GameInfo { Type = GameType.LinkMatch,    Title = "连连看",   Emoji = "🔗", Description = "找出相同的一对，连起来", Color = "#0097A7" },
        new GameInfo { Type = GameType.Maze,         Title = "走迷宫",   Emoji = "🌀", Description = "带着熊猫走出迷宫吧", Color = "#43A047" },
        new GameInfo { Type = GameType.SafetyQuiz,   Title = "安全小问答", Emoji = "🛡️", Description = "安全知识闯关，学会保护自己", Color = "#E53935" },
        new GameInfo { Type = GameType.Doodle,       Title = "快乐涂鸦", Emoji = "🎨", Description = "用手指画画，发挥想象力", Color = "#AB47BC" },
        new GameInfo { Type = GameType.FishEat,      Title = "大鱼吃小鱼", Emoji = "🐟", Description = "吃掉小鱼快快长大", Color = "#039BE5" },
        new GameInfo { Type = GameType.Arithmetic,   Title = "算术乐园",   Emoji = "🔢", Description = "加减乘除我来算", Color = "#00897B" },
        new GameInfo { Type = GameType.Sudoku,       Title = "数独小游戏", Emoji = "🧠", Description = "填数字，每行每列不重复", Color = "#5E35B1" },
        new GameInfo { Type = GameType.HappyLiteracy,Title = "开心识字",   Emoji = "😊", Description = "看汉字，选出对应图画", Color = "#66BB6A" },
        new GameInfo { Type = GameType.PinyinLiteracy,Title = "拼音识字",  Emoji = "🔤", Description = "看汉字，选出正确拼音", Color = "#26C6DA" },
        new GameInfo { Type = GameType.EncyclopediaQuiz, Title = "百科问答", Emoji = "📚", Description = "有趣的知识闯关问答", Color = "#00897B" },
        new GameInfo { Type = GameType.WhackMole, Title = "打地鼠", Emoji = "🔨", Description = "地鼠冒头快点它", Color = "#6D4C41" },
        new GameInfo { Type = GameType.Typing, Title = "打字游戏", Emoji = "⌨️", Description = "跟着读音把单词打出来", Color = "#3949AB" },
        new GameInfo { Type = GameType.Balloon, Title = "欢乐打气球", Emoji = "🎈", Description = "气球飞起来，点破它", Color = "#EC407A" },
        new GameInfo { Type = GameType.Go, Title = "围棋小课堂", Emoji = "⚫", Description = "9 路小棋盘，和电脑下一盘", Color = "#5D4037" },
        new GameInfo { Type = GameType.MindRead, Title = "心灵感应魔法", Emoji = "🔮", Description = "你在心里想，我能猜出来", Color = "#7C5CFF" },
        new GameInfo { Type = GameType.Tetris, Title = "俄罗斯方块", Emoji = "🧱", Description = "方块落下来，排满一行就消掉", Color = "#5E35B1" }
    };
}
