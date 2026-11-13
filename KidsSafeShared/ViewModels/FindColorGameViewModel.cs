using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Media;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 找颜色：先听颜色名称（语音），再从若干色块中选出对应的那一个。
/// </summary>
public class ColorOption : BaseViewModel
{
    public string Name { get; }
    public Color Swatch { get; }
    public bool IsAnswer { get; }
    public ColorOption(string name, Color swatch, bool isAnswer)
    {
        Name = name;
        Swatch = swatch;
        IsAnswer = isAnswer;
    }
}

public class FindColorGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<ColorItem> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;
    private ColorItem _target = new();

    private string _targetName = string.Empty;
    private Color _targetColor = Colors.Black;
    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<ColorOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string TargetName
    {
        get => _targetName;
        set => SetProperty(ref _targetName, value);
    }

    public Color TargetColor
    {
        get => _targetColor;
        set => SetProperty(ref _targetColor, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public string ScoreText
    {
        get => _scoreText;
        set => SetProperty(ref _scoreText, value);
    }

    public bool IsCorrect
    {
        get => _isCorrect;
        set => SetProperty(ref _isCorrect, value);
    }

    public bool IsWrong
    {
        get => _isWrong;
        set => SetProperty(ref _isWrong, value);
    }

    public string FeedbackText
    {
        get => _feedbackText;
        set => SetProperty(ref _feedbackText, value);
    }

    public bool HasFeedback => IsCorrect || IsWrong;

    public bool Finished
    {
        get => _finished;
        set
        {
            if (SetProperty(ref _finished, value))
                OnPropertyChanged(nameof(NotFinished));
        }
    }

    public bool NotFinished => !_finished;

    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    public void Start()
    {
        _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        DifficultyLabel = _difficulty.Label();

        _deck.Clear();
        var rounds = _difficulty.Rounds();
        var pool = GamesData.Colors.OrderBy(_ => _rnd.Next()).Take(rounds).ToList();
        _deck.AddRange(pool);
        _index = 0;
        _score = 0;
        Finished = false;
        UpdateScore();
        LoadRound();
    }

    private void LoadRound()
    {
        IsCorrect = false;
        IsWrong = false;
        FeedbackText = string.Empty;

        _target = _deck[_index];
        TargetName = _target.Name;
        TargetColor = Color.FromArgb(_target.Hex);
        ProgressText = $"第 {_index + 1} / {_deck.Count} 题";

        var answers = new List<ColorItem> { _target };
        var distractors = GamesData.Colors
            .Where(c => c.Hex != _target.Hex)
            .OrderBy(_ => _rnd.Next())
            .Take(_difficulty.OptionCount() - 1)
            .ToList();
        answers.AddRange(distractors);
        answers = answers.OrderBy(_ => _rnd.Next()).ToList();

        Options.Clear();
        foreach (var c in answers)
            Options.Add(new ColorOption(c.Name, Color.FromArgb(c.Hex), c.Hex == _target.Hex));

        // 听颜色名称
        _ = SpeakAsync(_target.Name);
    }

    public bool Submit(ColorOption option)
    {
        if (IsCorrect) return true;
        var sound = ServiceProvider.Get<ISoundService>();
        if (option.IsAnswer)
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "真棒！🌟";
            _score++;
            UpdateScore();
            sound.PlayCorrect();
            _ = SpeakAsync("真棒");
            return true;
        }

        IsWrong = true;
        IsCorrect = false;
        FeedbackText = "再找找～";
        sound.PlayWrong();
        return false;
    }

    public void SpeakTarget() => _ = SpeakAsync(_target.Name);

    public void Next()
    {
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.FindColor,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _deck.Count,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? $"🎉 新纪录！你找对了 {_score} 个颜色！"
                : $"你找对了 {_score} 个颜色，一共 {_deck.Count} 题！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    private static Task SpeakAsync(string text) => Helpers.Tts.SpeakAsync(text);

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
