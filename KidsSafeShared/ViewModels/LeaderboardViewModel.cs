using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>排行榜中某一游戏的分区。</summary>
public class LeaderboardSection : BaseViewModel
{
    public string GameEmoji { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public ObservableCollection<LeaderboardRow> Rows { get; } = new();
}

/// <summary>排行榜中的一行成绩。</summary>
public class LeaderboardRow : BaseViewModel
{
    public string Rank { get; set; } = string.Empty;
    public string DifficultyLabel { get; set; } = string.Empty;
    public string ScoreText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
}

public class LeaderboardViewModel : BaseViewModel
{
    public ObservableCollection<LeaderboardSection> Sections { get; } = new();

    /// <summary>榜单归属的小朋友的昵称（用于标题展示）。</summary>
    public string OwnerName => ServiceProvider.Get<ISettingsService>().Current.Nickname;

    public bool IsEmpty => Sections.Count == 0;
    public bool IsNotEmpty => !IsEmpty;

    public LeaderboardViewModel()
    {
        var lb = ServiceProvider.Get<ILeaderboardService>();

        foreach (var g in GameCatalog.All)
        {
            var top = lb.GetTop(g.Type, 5);
            if (top.Count == 0) continue;

            var section = new LeaderboardSection
            {
                GameEmoji = g.Emoji,
                GameTitle = g.Title
            };

            var rank = 1;
            foreach (var e in top)
            {
                section.Rows.Add(new LeaderboardRow
                {
                    Rank = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => rank.ToString() },
                    DifficultyLabel = e.Difficulty.Label(),
                    ScoreText = $"{e.Score} / {e.MaxScore}",
                    DateText = e.Date.ToString("MM/dd")
                });
                rank++;
            }
            Sections.Add(section);
        }
    }
}
