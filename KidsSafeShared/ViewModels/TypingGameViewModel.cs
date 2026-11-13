using System.Collections.Generic;
using System.Linq;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 打字游戏：显示英文单词并朗读，儿童在输入框中把单词打出来。
/// 打完即自动判定，正确进入下一个；可「听一听」重听、可「跳过」。
/// 全部打完生成成绩报告。
/// </summary>
public class TypingGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private List<string> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

    private string _currentWord = "cat";
    private string _typedText = string.Empty;
    private string _progressText = string.Empty;
    private string _scoreText = "⭐ 0";
    private string _difficultyLabel = "中等";
    private bool _isCorrect;
    private bool _isWrong;
    private string _feedbackText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;
    private string _totalText = string.Empty;

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    /// <summary>当前要打出的英文单词。</summary>
    public string CurrentWord
    {
        get => _currentWord;
        set => SetProperty(ref _currentWord, value);
    }

    /// <summary>已输入内容（与 Entry 双向绑定）。</summary>
    public string TypedText
    {
        get => _typedText;
        set
        {
            if (!SetProperty(ref _typedText, value)) return;
            CheckMatch();
        }
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

    public string TotalText
    {
        get => _totalText;
        set => SetProperty(ref _totalText, value);
    }

    private static readonly Dictionary<GameDifficulty, List<string>> WordPool = new()
    {
        [GameDifficulty.Easy] = new() { "cat","dog","sun","bus","red","pig","cow","fox","bee","owl","fish","star","book","ball","tree","kite","duck","frog","lion","bear" },
        [GameDifficulty.Medium] = new() { "apple","banana","tiger","zebra","rabbit","school","flower","pencil","orange","monkey","yellow","purple","planet","rocket","cookie","window","garden","puppet" },
        [GameDifficulty.Hard] = new() { "elephant","dinosaur","butterfly","computer","hamburger","strawberry","umbrella","pineapple","kangaroo","helicopter","christmas","beautiful","telephone","vegetables","library","birthday" }
    };

    public void Start()
    {
        _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        DifficultyLabel = _difficulty.Label();

        var pool = (WordPool.ContainsKey(_difficulty) ? WordPool[_difficulty] : WordPool[GameDifficulty.Medium])
            .OrderBy(_ => _rnd.Next())
            .Take(_difficulty.Rounds())
            .ToList();
        _deck = pool;
        _index = 0;
        _score = 0;
        Finished = false;
        UpdateScore();
        LoadWord();
    }

    private void LoadWord()
    {
        IsCorrect = false;
        IsWrong = false;
        FeedbackText = string.Empty;
        TypedText = string.Empty;
        CurrentWord = _deck[_index];
        ProgressText = $"第 {_index + 1} / {_deck.Count} 个";
        _ = Tts.SpeakAsync(CurrentWord, "en");
    }

    private void CheckMatch()
    {
        if (IsCorrect || Finished) return;
        var typed = _typedText?.Trim() ?? string.Empty;
        if (typed.Length == 0) return;
        if (string.Equals(typed, _currentWord, System.StringComparison.OrdinalIgnoreCase))
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "太棒了！🌟";
            _score++;
            UpdateScore();
            ServiceProvider.Get<ISoundService>().PlayCorrect();
            Device.StartTimer(TimeSpan.FromMilliseconds(550), () => { Next(); return false; });
        }
    }

    /// <summary>回车键触发：若还没对，则判为错并展示正确答案后进入下一题。</summary>
    public void Submit()
    {
        if (IsCorrect || Finished) return;
        var typed = _typedText?.Trim() ?? string.Empty;
        if (typed.Length == 0)
        {
            FeedbackText = "先试着打一打吧～";
            IsWrong = false;
            IsCorrect = false;
            return;
        }
        IsWrong = true;
        IsCorrect = false;
        FeedbackText = $"正确答案：{_currentWord}";
        ServiceProvider.Get<ISoundService>().PlayWrong();
        _ = Tts.SpeakAsync(_currentWord, "en");
        Device.StartTimer(TimeSpan.FromMilliseconds(900), () => { Next(); return false; });
    }

    /// <summary>「听一听」：重新朗读当前单词。</summary>
    public void SpeakCurrent() => _ = Tts.SpeakAsync(CurrentWord, "en");

    public void Next()
    {
        if (Finished) return;
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var total = _deck.Count;
            TotalText = $"共 {total} 个单词，打对 {_score} 个";
            ResultText = _score == total
                ? "🎉 全部打对啦！打字小高手！"
                : $"你打对了 {_score} 个单词，继续练习会更棒！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadWord();
    }

    public void Replay() => Start();

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
