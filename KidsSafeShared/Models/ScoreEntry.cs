using System.Text.Json.Serialization;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一条游戏成绩记录（本地存储，用于排行榜）。
/// </summary>
public class ScoreEntry
{
    /// <summary>对应游戏类型</summary>
    public GameType Game { get; set; }

    /// <summary>难度</summary>
    public GameDifficulty Difficulty { get; set; }

    /// <summary>答对数量（或配对成功的对数）</summary>
    public int Score { get; set; }

    /// <summary>总题数（或总对数）</summary>
    public int MaxScore { get; set; }

    /// <summary>完成时间</summary>
    public System.DateTime Date { get; set; }

    /// <summary>游玩者名字（默认“小朋友”）</summary>
    public string Player { get; set; } = "小朋友";

    /// <summary>
    /// 加权得分：原始分 × 难度系数。排行榜按此排序，
    /// 这样困难难度的好成绩会排在简单难度之前。仅用于排序，不持久化。
    /// </summary>
    [JsonIgnore]
    public int Points => Score * Difficulty.Factor();
}
