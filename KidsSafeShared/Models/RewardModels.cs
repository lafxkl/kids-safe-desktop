namespace KidsSafeDesktop.Models;

/// <summary>
/// 一次获得星星的记录（本地保存，最多保留 <see cref="Services.StarCenter"/> 里的上限条数）。
/// </summary>
public class StarRecord
{
    public DateTime Time { get; set; } = DateTime.Now;
    /// <summary>来源标识：游戏用 GameType 名，学习页用页面 KEY，家务用任务 id。</summary>
    public string SourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = "⭐";
    public int Stars { get; set; }
    /// <summary>game / learn / chore / story / care。</summary>
    public string Kind { get; set; } = "game";
    public string Note { get; set; } = string.Empty;

    public string TimeText => Time.ToString("MM-dd HH:mm");
    public string StarsText => Stars > 0 ? $"+{Stars}" : Stars.ToString();
    public bool IsPositive => Stars > 0;
}

/// <summary>勋章。解锁状态在每次获得星星时重算。</summary>
public class StarBadge
{
    public string Id { get; init; } = string.Empty;
    public string Emoji { get; init; } = "🎖️";
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>进度文案，例如「3 / 7 天」。</summary>
    public string Progress { get; set; } = string.Empty;
    public bool Unlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }

    public string LockEmoji => Unlocked ? Emoji : "🔒";
    public string StateText => Unlocked ? "已获得" : Progress;
}

/// <summary>获得星星后的结果，供结算页 / 激励页展示。</summary>
public class AwardResult
{
    public int Stars { get; init; }
    public int Total { get; init; }
    public int Today { get; init; }
    public int Streak { get; init; }
    public bool IsFirstAwardToday { get; init; }
    public List<StarBadge> NewBadges { get; init; } = new();
}

/// <summary>
/// 结算/激励页要展示的内容。游戏结算与学习激励共用一套页面骨架，
/// 区别只在 <see cref="Kind"/> 决定的文案与按钮。
/// </summary>
public class RewardOutcome
{
    /// <summary>game / learn / story / chore / care。</summary>
    public string Kind { get; init; } = "game";
    public string Emoji { get; init; } = "🎉";
    /// <summary>主标题，如游戏名或学科名。</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>大字夸奖，如「太棒了！」。</summary>
    public string Headline { get; init; } = "太棒了！";
    /// <summary>成绩说明，如「答对 8 / 10 题」。</summary>
    public string Detail { get; init; } = string.Empty;
    /// <summary>星级（0–5），由成绩换算，用于画星星。</summary>
    public int Stars { get; init; } = 3;
    /// <summary>本次获得的星星币。</summary>
    public int Award { get; init; } = 1;
    /// <summary>一句具体、可执行的鼓励（避免空泛的「你真棒」）。</summary>
    public string Encourage { get; init; } = string.Empty;
    /// <summary>给孩子的下一步建议。</summary>
    public string NextTip { get; init; } = string.Empty;
    /// <summary>详情页数据键（学习页用来回到原页面）。</summary>
    public string SourceId { get; init; } = string.Empty;

    public string StarRow => BuildStars(Stars);
    public string AwardText => Award > 0 ? $"+{Award}" : "0";
    public bool IsGame => Kind == "game";
    public bool HasAward => Award > 0;

    private static string BuildStars(int n)
    {
        n = Math.Clamp(n, 0, 5);
        return new string('⭐', n) + new string('☆', 5 - n);
    }
}
