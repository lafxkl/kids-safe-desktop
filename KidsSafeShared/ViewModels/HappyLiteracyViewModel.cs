using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 开心识字游戏（看字选图）：显示一个汉字/词语，让小朋友从若干 emoji 图画里
/// 选出与之对应的那一张。与「看图识字」（看图画选字）方向相反，强化「字 → 图」的认读。
/// </summary>
public class HappyLiteracyViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<PictureWordCard> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

    private string _questionWord = "猫";
    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<ChoiceOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    /// <summary>当前题目要认读的汉字/词语。</summary>
    public string QuestionWord
    {
        get => _questionWord;
        set => SetProperty(ref _questionWord, value);
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
        var pool = GamesData.WordCards
            .Where(c => !string.IsNullOrEmpty(c.Emoji) && !string.IsNullOrEmpty(c.Word))
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

        var card = _deck[_index];
        QuestionWord = card.Word;
        ProgressText = $"第 {_index + 1} / {_deck.Count} 张";

        // 正确项（emoji 图画）+ 干扰图画，打乱顺序
        var answers = new List<string> { card.Emoji };
        var distractors = GamesData.WordCards
            .Where(c => !string.IsNullOrEmpty(c.Emoji) && c.Emoji != card.Emoji)
            .Select(c => c.Emoji)
            .Distinct()
            .OrderBy(_ => _rnd.Next())
            .Take(_difficulty.OptionCount() - 1)
            .ToList();
        answers.AddRange(distractors);
        answers = answers.OrderBy(_ => _rnd.Next()).ToList();

        Options.Clear();
        foreach (var a in answers)
            Options.Add(new ChoiceOption(a, a == card.Emoji, 56));
    }

    /// <summary>提交一个选项。返回是否答对。</summary>
    public bool Submit(ChoiceOption option)
    {
        if (IsCorrect) return true; // 本轮已答对，忽略重复点击
        if (option.IsAnswer)
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "真棒！🌟";
            _score++;
            UpdateScore();
            ServiceProvider.Get<ISoundService>().PlayCorrect();
            return true;
        }

        IsWrong = true;
        IsCorrect = false;
        FeedbackText = "再看看～";
        ServiceProvider.Get<ISoundService>().PlayWrong();
        return false;
    }

    public void Next()
    {
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.HappyLiteracy,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _deck.Count,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? $"🎉 新纪录！你认识了 {_score} 个字！"
                : $"你认识了 {_score} 个字，一共 {_deck.Count} 张！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    /// <summary>朗读当前汉字/词语（供「🔊 听一听」按钮调用）。</summary>
    public void SpeakCurrent() => _ = Tts.SpeakAsync(_questionWord, "zh");

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
