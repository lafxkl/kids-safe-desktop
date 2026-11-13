using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>算术题的一个可选项（用于在 CollectionView 中绑定，区分对错）。</summary>
public class MathOption : BaseViewModel
{
    public string Text { get; }
    public bool IsAnswer { get; }
    public MathOption(string text, bool isAnswer)
    {
        Text = text;
        IsAnswer = isAnswer;
    }
}

/// <summary>一道生成的算术题。</summary>
public class MathProblem
{
    public string Expression { get; init; } = string.Empty; // 例如 "3 + 4 = ?"
    public int Answer { get; init; }
    public string Category { get; init; } = "算术";
    public string Explain { get; init; } = string.Empty;
}

/// <summary>
/// 算术乐园：按难度动态生成加、减、乘、除算式，给出选项让小朋友选，答对得分，结束计入排行榜。
/// </summary>
public class MathGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<MathProblem> _deck = new();
    private int _index;
    private int _score;
    private GameDifficulty _difficulty = GameDifficulty.Medium;

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

    public ObservableCollection<MathOption> Options { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
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
        for (var i = 0; i < rounds; i++)
            _deck.Add(GenerateProblem());
        _index = 0;
        _score = 0;
        Finished = false;
        UpdateScore();
        LoadRound();
    }

    /// <summary>根据难度随机生成一道算式题。</summary>
    private MathProblem GenerateProblem()
    {
        // 不同难度支持的不同运算
        var ops = _difficulty switch
        {
            GameDifficulty.Easy => new[] { '+', '-' },
            GameDifficulty.Medium => new[] { '+', '-', '×' },
            _ => new[] { '+', '-', '×', '÷' }
        };
        var op = ops[_rnd.Next(ops.Length)];

        int a, b, ans;
        string cat;
        switch (op)
        {
            case '+':
            {
                var max = _difficulty == GameDifficulty.Easy ? 10 : _difficulty == GameDifficulty.Medium ? 20 : 50;
                a = _rnd.Next(1, max + 1);
                b = _rnd.Next(1, max + 1);
                ans = a + b;
                cat = "加法";
                break;
            }
            case '-':
            {
                var max = _difficulty == GameDifficulty.Easy ? 10 : _difficulty == GameDifficulty.Medium ? 20 : 50;
                a = _rnd.Next(1, max + 1);
                b = _rnd.Next(0, a + 1); // 保证不出现负数
                ans = a - b;
                cat = "减法";
                break;
            }
            case '×':
            {
                var max = _difficulty == GameDifficulty.Easy ? 5 : 9;
                a = _rnd.Next(2, max + 1);
                b = _rnd.Next(2, max + 1);
                ans = a * b;
                cat = "乘法";
                break;
            }
            default: // ÷
            {
                var max = _difficulty == GameDifficulty.Easy ? 5 : 9;
                b = _rnd.Next(2, max + 1);
                var q = _rnd.Next(2, max + 1);
                a = b * q; // 保证整除
                ans = q;
                cat = "除法";
                break;
            }
        }

        var opText = op == '×' ? "×" : op == '÷' ? "÷" : op.ToString();
        var explain = op == '÷'
            ? $"{a} ÷ {b} = {ans}，想口诀：{b} 乘 {ans} 等于 {a}。"
            : op == '×'
                ? $"{a} × {b} = {ans}，背乘法口诀就很快啦。"
                : $"{a} {opText} {b} = {ans}。";

        return new MathProblem
        {
            Expression = $"{a} {opText} {b} = ?",
            Answer = ans,
            Category = cat,
            Explain = explain
        };
    }

    private void LoadRound()
    {
        IsCorrect = false;
        IsWrong = false;
        FeedbackText = string.Empty;
        ExplainText = string.Empty;

        var p = _deck[_index];
        QuestionText = p.Expression;
        Category = p.Category;
        ProgressText = $"第 {_index + 1} / {_deck.Count} 题";

        // 生成选项：正确答案 + 干扰项
        var count = _difficulty.OptionCount();
        var set = new HashSet<int> { p.Answer };
        var guard = 0;
        while (set.Count < count && guard++ < 100)
        {
            var delta = _rnd.Next(1, 4) * (_rnd.Next(0, 2) == 0 ? 1 : -1);
            var wrong = p.Answer + delta;
            if (wrong >= 0 && wrong != p.Answer)
                set.Add(wrong);
        }
        // 如果干扰项不够（例如答案很小），用更大的偏移补充
        var extra = 4;
        while (set.Count < count)
        {
            var wrong = p.Answer + extra;
            if (wrong != p.Answer) set.Add(wrong);
            extra++;
        }

        var indexed = set
            .OrderBy(_ => _rnd.Next())
            .Select(v => new MathOption(v.ToString(), v == p.Answer))
            .ToList();

        Options.Clear();
        foreach (var opt in indexed)
            Options.Add(opt);

        _ = SpeakAsync(p.Expression.Replace("=", "等于多少"));
    }

    public bool Submit(MathOption option)
    {
        if (IsCorrect) return true;
        var sound = ServiceProvider.Get<ISoundService>();
        if (option.IsAnswer)
        {
            IsCorrect = true;
            IsWrong = false;
            FeedbackText = "答对啦！🌟";
            ExplainText = _deck[_index].Explain;
            _score++;
            UpdateScore();
            sound.PlayCorrect();
            _ = SpeakAsync("真棒");
            return true;
        }

        IsWrong = true;
        IsCorrect = false;
        FeedbackText = "再想想～";
        ExplainText = _deck[_index].Explain;
        sound.PlayWrong();
        return false;
    }

    public void Next()
    {
        _index++;
        if (_index >= _deck.Count)
        {
            Finished = true;
            var best = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.Arithmetic,
                Difficulty = _difficulty,
                Score = _score,
                MaxScore = _deck.Count,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = best
                ? $"🎉 新纪录！你算对了 {_score} 道题！"
                : $"你算对了 {_score} 道，一共 {_deck.Count} 道！数学小能手！";
            ServiceProvider.Get<ISoundService>().PlayWin();
            return;
        }
        LoadRound();
    }

    public void Replay() => Start();

    private static Task SpeakAsync(string text) => Helpers.Tts.SpeakAsync(text);

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
