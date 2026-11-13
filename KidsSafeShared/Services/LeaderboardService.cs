using KidsSafeDesktop.Models;
using System.Collections.Generic;
using System.Linq;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 排行榜实现：成绩存在 ChildSettings.HighScores 中（随设置一起持久化）。
/// </summary>
public class LeaderboardService : ILeaderboardService
{
    private readonly ISettingsService _settings;

    public LeaderboardService(ISettingsService settings) => _settings = settings;

    public bool Record(ScoreEntry entry)
    {
        var list = _settings.Current.HighScores;
        var prevBest = BestScore(entry.Game, entry.Difficulty);

        list.Add(entry);

        // 控制体积：每个游戏最多保留近 50 条，避免无限增长。
        if (list.Count > 50)
        {
            var trimmed = list
                .GroupBy(e => e.Game)
                .SelectMany(g => g.OrderByDescending(e => e.Points)
                                   .ThenByDescending(e => e.Date)
                                   .Take(50))
                .ToList();
            list.Clear();
            list.AddRange(trimmed);
        }

        _ = _settings.SaveAsync();
        return entry.Score > prevBest;
    }

    public IReadOnlyList<ScoreEntry> GetAll() => _settings.Current.HighScores.ToList();

    public IReadOnlyList<ScoreEntry> GetTop(GameType game, int n = 10)
        => _settings.Current.HighScores
            .Where(e => e.Game == game)
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.Date)
            .Take(n)
            .ToList();

    public int BestScore(GameType game, GameDifficulty difficulty)
    {
        var best = _settings.Current.HighScores
            .Where(e => e.Game == game && e.Difficulty == difficulty)
            .OrderByDescending(e => e.Score)
            .FirstOrDefault();
        return best?.Score ?? 0;
    }

    public void Clear()
    {
        _settings.Current.HighScores.Clear();
        _ = _settings.SaveAsync();
    }
}
