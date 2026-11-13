using KidsSafeDesktop.Models;
using System.Collections.Generic;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 排行榜服务：记录并读取本地游戏成绩。
/// </summary>
public interface ILeaderboardService
{
    /// <summary>记录一次成绩，返回是否刷新了该游戏（同难度）的历史最高分。</summary>
    bool Record(ScoreEntry entry);

    /// <summary>返回全部成绩（未排序）。</summary>
    IReadOnlyList<ScoreEntry> GetAll();

    /// <summary>返回某游戏按得分降序的前 n 条。</summary>
    IReadOnlyList<ScoreEntry> GetTop(GameType game, int n = 10);

    /// <summary>某游戏某难度的历史最高原始分（无记录返回 0）。</summary>
    int BestScore(GameType game, GameDifficulty difficulty);

    /// <summary>清空全部记录（家长操作）。</summary>
    void Clear();
}
