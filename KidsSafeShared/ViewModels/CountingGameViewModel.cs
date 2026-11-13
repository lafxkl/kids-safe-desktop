using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 数一数游戏：显示若干相同的 emoji，让小朋友数出数量并从 3 个数字里选对。
/// </summary>
public class NumberOption : BaseViewModel
{
    public int Number { get; }
    public bool IsAnswer { get; }
    public NumberOption(int number, bool isAnswer)
    {
        Number = number;
        IsAnswer = isAnswer;
    }
}

public class CountingGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private int _target;
    private int _roundIndex;
    private int _rounds;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

    private string _emojiRow = string.Empty;
    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<NumberOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string EmojiRow
    {
        get => _emojiRow;
        set => SetProperty(ref _emojiRow, value);
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

        _rounds = _difficulty.Rounds();
        _roundIndex = 0;
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

        // 难度越高，出现的数量上限越大
        var max = _difficulty.MaxCount();
        _target = _rnd.Next(1, max + 1);
        var emoji = GamesData.CountingEmojis[_rnd.Next(GamesData.CountingEmojis.Count)];
        EmojiRow = string.Join("  ", Enumerable.Repeat(emoji, _target));
        ProgressText = $"第 {_roundIndex + 1} / {_rounds} 题";

        // 正确数字 + 相近干扰项（数量随难度变化），再打乱
        var answers = new List<int> { _target };
        while (answers.Count < _difficulty.OptionCount())
        {
            var d = _rnd.Next(1, max + 1);
            if (d != _target && !answers.Contains(d))
                answers.Add(d);
        }
        answers = answers.OrderBy(_ => _rnd.Next()).ToList();

        Options.Clear();
        foreach (var n in answers)
            Options.Add(new NumberOption(n, n == _target));
    }

    public bool Submit(NumberOption option)
    {
        if (IsCorrect) return true;
        if (option.IsAnswer)
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "数得真对！🌟";
            _score++;
            UpdateScore();
            ServiceProvider.Get<ISoundService>().PlayCorrect();
            return true;
        }

        IsWrong = true;
        IsCorrect = false;
        FeedbackText = "再数一数～";
        ServiceProvider.Get<ISoundService>().PlayWrong();
        return false;
    }

    public void Next()
    {
        _roundIndex++;
        if (_roundIndex >= _rounds)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.Counting,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _rounds,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? $"🎉 新纪录！你答对了 {_score} 题！"
                : $"你答对了 {_score} 题，一共 {_rounds} 题！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
