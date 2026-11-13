using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Media;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 听音选物：播放一个词的读音（语音合成），让小朋友从 3 个图标里选出对应的东西。
/// </summary>
public class ListenOption : BaseViewModel
{
    public string Emoji { get; }
    public string Word { get; }
    public bool IsAnswer { get; }
    public ListenOption(string emoji, string word, bool isAnswer)
    {
        Emoji = emoji;
        Word = word;
        IsAnswer = isAnswer;
    }
}

public class ListenGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<PictureWordCard> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;
    private PictureWordCard _current = new();

    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<ListenOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
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

        _current = _deck[_index];
        ProgressText = $"第 {_index + 1} / {_deck.Count} 题";

        var answers = new List<string> { _current.Emoji };
        var distractors = GamesData.WordCards
            .Where(c => c.Emoji != _current.Emoji)
            .Select(c => c.Emoji)
            .Distinct()
            .OrderBy(_ => _rnd.Next())
            .Take(_difficulty.OptionCount() - 1)
            .ToList();
        answers.AddRange(distractors);
        answers = answers.OrderBy(_ => _rnd.Next()).ToList();

        Options.Clear();
        foreach (var e in answers)
            Options.Add(new ListenOption(e, _current.Word, e == _current.Emoji));

        // 听一听
        _ = SpeakAsync(_current.Word);
    }

    public bool Submit(ListenOption option)
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
        FeedbackText = "再听一次～";
        sound.PlayWrong();
        _ = SpeakAsync(_current.Word);
        return false;
    }

    public void SpeakCurrent() => _ = SpeakAsync(_current.Word);

    public void Next()
    {
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.Listen,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _deck.Count,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? $"🎉 新纪录！你答对了 {_score} 个！"
                : $"你答对了 {_score} 个，一共 {_deck.Count} 题！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    private static Task SpeakAsync(string text) => Helpers.Tts.SpeakAsync(text);

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
