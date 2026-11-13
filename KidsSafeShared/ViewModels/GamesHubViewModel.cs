using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 小游戏乐园：列出家长已开启的内置益智小游戏，并提供难度选择。
/// </summary>
public class GameDifficultyItem : BaseViewModel
{
    public GameDifficulty Value { get; }
    public string Label => Value.Label();
    public GameDifficultyItem(GameDifficulty value) => Value = value;
}

public class GamesHubViewModel : BaseViewModel
{
    private readonly ISettingsService _settings;
    private GameDifficulty _difficulty;

    public ObservableCollection<GameInfo> Games { get; } = new();
    public ObservableCollection<GameDifficultyItem> Difficulties { get; } = new();

    public GameDifficulty Difficulty
    {
        get => _difficulty;
        set
        {
            if (SetProperty(ref _difficulty, value))
            {
                _settings.Current.Difficulty = value;
                _ = _settings.SaveAsync();
            }
        }
    }

    public GamesHubViewModel()
    {
        _settings = ServiceProvider.Get<ISettingsService>();
        _difficulty = _settings.Current.Difficulty;

        foreach (var g in GameCatalog.All.Where(g => _settings.Current.IsGameEnabled(g.Type)))
            Games.Add(g);

        Difficulties.Add(new GameDifficultyItem(GameDifficulty.Easy));
        Difficulties.Add(new GameDifficultyItem(GameDifficulty.Medium));
        Difficulties.Add(new GameDifficultyItem(GameDifficulty.Hard));
    }

    /// <summary>当前小朋友的昵称（用于大厅欢迎语）。</summary>
    public string Nickname => _settings.Current.Nickname;
}
