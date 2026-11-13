using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 看图识字游戏：显示一个 emoji「图片」，让小朋友从 3 个词里选出正确的字。
/// 答对有鼓励，答错可重试；一轮若干张，结束后给出成绩。
/// </summary>
public class WordOption : BaseViewModel
{
    public string Text { get; }
    public bool IsAnswer { get; }
    public WordOption(string text, bool isAnswer)
    {
        Text = text;
        IsAnswer = isAnswer;
    }
}

public class PictureWordGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<PictureWordCard> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

    private string _emoji = "🐱";
    private string _currentImage = string.Empty;
    private bool _showImage;
    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<WordOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string Emoji
    {
        get => _emoji;
        set => SetProperty(ref _emoji, value);
    }

    /// <summary>当前题目的正确字词（用于语音朗读）。</summary>
    public string CurrentWord
    {
        get => _currentWord;
        set => SetProperty(ref _currentWord, value);
    }
    private string _currentWord = string.Empty;

    /// <summary>图片资源文件名（如 "pw_sun.png"）；为空表示使用 Emoji。</summary>
    public string CurrentImage
    {
        get => _currentImage;
        set => SetProperty(ref _currentImage, value);
    }

    /// <summary>是否使用图片（而非 Emoji）作为题目图。</summary>
    public bool ShowImage
    {
        get => _showImage;
        set => SetProperty(ref _showImage, value);
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
        var pool = GamesData.WordCards.OrderBy(_ => _rnd.Next()).Take(rounds).ToList();
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
        Emoji = card.Emoji;
        CurrentImage = card.Image;
        ShowImage = card.HasImage;
        CurrentWord = card.Word;
        ProgressText = $"第 {_index + 1} / {_deck.Count} 张";

        // 正确项 + 干扰词（数量随难度变化），打乱顺序
        var answers = new List<string> { card.Word };
        var distractors = GamesData.WordCards
            .Where(c => c.Word != card.Word)
            .Select(c => c.Word)
            .Distinct()
            .OrderBy(_ => _rnd.Next())
            .Take(_difficulty.OptionCount() - 1)
            .ToList();
        answers.AddRange(distractors);
        answers = answers.OrderBy(_ => _rnd.Next()).ToList();

        Options.Clear();
        foreach (var a in answers)
            Options.Add(new WordOption(a, a == card.Word));
    }

    /// <summary>提交一个选项。返回是否答对。</summary>
    public bool Submit(WordOption option)
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
        FeedbackText = "再想想～";
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
                Game = GameType.PictureWord,
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

    /// <summary>朗读当前字词（供「🔊 听一听」按钮调用）。</summary>
    public void SpeakCurrent() => _ = Tts.SpeakAsync(_currentWord, "zh");

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
