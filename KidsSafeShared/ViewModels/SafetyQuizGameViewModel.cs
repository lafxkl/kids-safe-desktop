using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 安全小问答：一道题的可选项（用于在 CollectionView 中绑定）。
/// </summary>
public class SafetyOption : BaseViewModel
{
    public string Text { get; }
    public bool IsAnswer { get; }
    public SafetyOption(string text, bool isAnswer)
    {
        Text = text;
        IsAnswer = isAnswer;
    }
}

/// <summary>
/// 安全小问答小游戏：从题库抽取若干题，每题给出选项，答对得分，结束计入排行榜。
/// </summary>
public class SafetyQuizGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<SafetyQuizQuestion> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

    private SafetyQuizQuestion _current = new();
    private string _questionEmoji = "🛡️";
    private string _questionText = string.Empty;
    private string _category = string.Empty;
    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private string _explainText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<SafetyOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string QuestionEmoji
    {
        get => _questionEmoji;
        set => SetProperty(ref _questionEmoji, value);
    }

    public string QuestionText
    {
        get => _questionText;
        set => SetProperty(ref _questionText, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
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

    public string ExplainText
    {
        get => _explainText;
        set => SetProperty(ref _explainText, value);
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
        var pool = SafetyQuizData.Questions
            .OrderBy(_ => _rnd.Next())
            .Take(rounds)
            .ToList();
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
        ExplainText = string.Empty;

        _current = _deck[_index];
        QuestionEmoji = _current.Emoji;
        QuestionText = _current.Question;
        Category = _current.Category;
        ProgressText = $"第 {_index + 1} / {_deck.Count} 题";

        // 打乱选项，但保持正确答案可识别
        var indexed = _current.Options
            .Select((text, i) => (text, isAnswer: i == _current.CorrectIndex))
            .OrderBy(_ => _rnd.Next())
            .ToList();

        Options.Clear();
        foreach (var (text, isAnswer) in indexed)
            Options.Add(new SafetyOption(text, isAnswer));

        _ = SpeakAsync(_current.Question);
    }

    public bool Submit(SafetyOption option)
    {
        if (IsCorrect) return true;
        var sound = ServiceProvider.Get<ISoundService>();
        if (option.IsAnswer)
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "答对啦！🌟";
            ExplainText = _current.Explain;
            _score++;
            UpdateScore();
            sound.PlayCorrect();
            _ = SpeakAsync("真棒");
            return true;
        }

        IsWrong = true;
        IsCorrect = false;
        FeedbackText = "再想想～";
        ExplainText = _current.Explain;
        sound.PlayWrong();
        return false;
    }

    public void SpeakQuestion() => _ = SpeakAsync(_current.Question);

    public void Next()
    {
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.SafetyQuiz,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _deck.Count,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? $"🎉 新纪录！你答对了 {_score} 题安全知识！"
                : $"你答对了 {_score} 题，一共 {_deck.Count} 题！安全意识真棒！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    private static Task SpeakAsync(string text) => Helpers.Tts.SpeakAsync(text);

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
