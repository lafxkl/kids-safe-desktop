using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>百科问答选项（用于 CollectionView 绑定）。</summary>
public class EncyclopediaOption : BaseViewModel
{
    public string Text { get; }
    public bool IsAnswer { get; }
    public EncyclopediaOption(string text, bool isAnswer)
    {
        Text = text;
        IsAnswer = isAnswer;
    }
}

/// <summary>
/// 百科问答小游戏：从知识题库抽题，每题四选一，答对得分，结束计入排行榜。
/// 每题自动用中文语音朗读，并提供「🔊 听题」按钮重听。
/// </summary>
public class EncyclopediaQuizViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<EncyclopediaQuestion> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

    private EncyclopediaQuestion _current = new();
    private string _questionEmoji = "📚";
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

    public ObservableCollection<EncyclopediaOption> Options { get; } = new();

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
        var pool = EncyclopediaData.Questions
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

        var indexed = _current.Options
            .Select((text, i) => (text, isAnswer: i == _current.CorrectIndex))
            .OrderBy(_ => _rnd.Next())
            .ToList();

        Options.Clear();
        foreach (var (text, isAnswer) in indexed)
            Options.Add(new EncyclopediaOption(text, isAnswer));

        _ = Tts.SpeakAsync(_current.Question, "zh");
    }

    public bool Submit(EncyclopediaOption option)
    {
        if (IsCorrect) return true;
        var sound = ServiceProvider.Get<ISoundService>();
        if (option.IsAnswer)
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "答对啦！🌟";
            ExplainText = "💡 " + _current.Explain;
            _score++;
            UpdateScore();
            sound.PlayCorrect();
            _ = Tts.SpeakAsync("真棒", "zh");
            return true;
        }

        IsWrong = true;
        IsCorrect = false;
        FeedbackText = "再想想～";
        ExplainText = "💡 " + _current.Explain;
        sound.PlayWrong();
        return false;
    }

    public void SpeakQuestion() => _ = Tts.SpeakAsync(_current.Question, "zh");

    public void Next()
    {
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.EncyclopediaQuiz,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _deck.Count,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? $"🎉 新纪录！你答对了 {_score} 题百科知识！"
                : $"你答对了 {_score} 题，一共 {_deck.Count} 题！真是个小学问家！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
