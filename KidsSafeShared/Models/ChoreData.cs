using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>家务任务的难度（与游戏难度无关，只用于分档与积分建议）。</summary>
public enum ChoreDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>一件家务任务。</summary>
public class ChoreTask
{
    public string Id { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🧹";
    public string Name { get; set; } = string.Empty;
    public int Points { get; set; } = 5;
    public ChoreDifficulty Difficulty { get; set; } = ChoreDifficulty.Easy;
    public bool Custom { get; set; }

    [JsonIgnore] public string DifficultyLabel => Difficulty switch
    {
        ChoreDifficulty.Easy => "简单",
        ChoreDifficulty.Medium => "中等",
        _ => "困难"
    };

    [JsonIgnore] public string DifficultyEmoji => Difficulty switch
    {
        ChoreDifficulty.Easy => "🟢",
        ChoreDifficulty.Medium => "🟡",
        _ => "🔴"
    };

    [JsonIgnore] public Color DifficultyColor => Difficulty switch
    {
        ChoreDifficulty.Easy => Color.FromArgb("#4A8A4A"),
        ChoreDifficulty.Medium => Color.FromArgb("#B8860B"),
        _ => Color.FromArgb("#C04848")
    };

    [JsonIgnore] public Color DifficultyBg => Difficulty switch
    {
        ChoreDifficulty.Easy => Color.FromArgb("#E0F5E0"),
        ChoreDifficulty.Medium => Color.FromArgb("#FFF0D6"),
        _ => Color.FromArgb("#FFE0E0")
    };
}

/// <summary>可以用家庭积分兑换的奖励。</summary>
public class ChoreReward
{
    public string Id { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎁";
    public string Name { get; set; } = string.Empty;
    public int Points { get; set; } = 20;
    public string Description { get; set; } = string.Empty;
    public bool Custom { get; set; }
}

/// <summary>打卡状态：待家长确认 / 已确认 / 已驳回。</summary>
public enum ChoreStatus
{
    Pending,
    Confirmed,
    Rejected
}

/// <summary>一次家务打卡记录。</summary>
public class ChoreRecord
{
    public string Id { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string TaskEmoji { get; set; } = "🧹";
    public int Points { get; set; }
    public ChoreDifficulty Difficulty { get; set; }
    public string Date { get; set; } = string.Empty;      // yyyy-MM-dd
    public string Time { get; set; } = string.Empty;      // HH:mm
    public ChoreStatus Status { get; set; } = ChoreStatus.Pending;
    public string RejectReason { get; set; } = string.Empty;

    [JsonIgnore] public DateTime Day => DateTime.TryParse(Date, out var d) ? d.Date : DateTime.Today;
}

/// <summary>一次奖励兑换记录。</summary>
public class ChoreRedemption
{
    public string Id { get; set; } = string.Empty;
    public string RewardId { get; set; } = string.Empty;
    public string RewardName { get; set; } = string.Empty;
    public string RewardEmoji { get; set; } = "🎁";
    public int Points { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

/// <summary>家庭积分账本。</summary>
public class ChorePoints
{
    public int Current { get; set; }
    public int Total { get; set; }
    public int Exchanged { get; set; }
}

/// <summary>小小家务英雄的全部本地数据。</summary>
public class ChoreStore
{
    public List<ChoreTask> Tasks { get; set; } = new();
    public List<ChoreReward> Rewards { get; set; } = new();
    public List<ChoreRecord> Records { get; set; } = new();
    public List<ChoreRedemption> Redemptions { get; set; } = new();
    public ChorePoints Points { get; set; } = new();
    /// <summary>最近一次「今日全部完成」奖励的日期，防止重复发放。</summary>
    public string LastBonusDate { get; set; } = string.Empty;
    /// <summary>最近一次「连续 7 天」奖励的日期。</summary>
    public string LastWeekBonusDate { get; set; } = string.Empty;
    /// <summary>孩子/家长模式（只是本页的显示状态）。</summary>
    public bool ParentMode { get; set; }
}
