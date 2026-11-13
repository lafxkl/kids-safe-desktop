using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>一个选项（选择题的 A-D，或判断题的对/错）。</summary>
public class ExamOption : BaseViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Display { get; init; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// 模拟考试：一次一题，可以前后翻、可以改答案，交卷后统一批改并逐题给解析。
/// 刻意不像口算那样「答一题报一次对错」，考试场景要保留检查的余地。
/// </summary>
public class ExamViewModel : BaseViewModel
{
    private ExamPaper? _paper;
    private List<ExamQuestion> _questions = new();
    private string[] _answers = Array.Empty<string>();
    private int _index;
    private long _startTicks;
    private int _elapsedSeconds;

    // ---------- 试卷信息 ----------

    private string _titleText = string.Empty;
    public string TitleText
    {
        get => _titleText;
        private set => SetProperty(ref _titleText, value);
    }

    private string _paperInfo = string.Empty;
    public string PaperInfo
    {
        get => _paperInfo;
        private set => SetProperty(ref _paperInfo, value);
    }

    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    private string _elapsedText = "⏱ 00:00";
    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    private string _answeredText = string.Empty;
    public string AnsweredText
    {
        get => _answeredText;
        private set => SetProperty(ref _answeredText, value);
    }

    // ---------- 当前题目 ----------

    private string _stem = string.Empty;
    public string Stem
    {
        get => _stem;
        private set => SetProperty(ref _stem, value);
    }

    private string _kindText = string.Empty;
    public string KindText
    {
        get => _kindText;
        private set => SetProperty(ref _kindText, value);
    }

    private bool _showOptions;
    public bool ShowOptions
    {
        get => _showOptions;
        private set => SetProperty(ref _showOptions, value);
    }

    private bool _showFill;
    public bool ShowFill
    {
        get => _showFill;
        private set => SetProperty(ref _showFill, value);
    }

    private string _fillInput = string.Empty;
    public string FillInput
    {
        get => _fillInput;
        set
        {
            if (!SetProperty(ref _fillInput, value)) return;
            if (_index >= 0 && _index < _answers.Length)
            {
                _answers[_index] = value ?? string.Empty;
                UpdateAnswered();
            }
        }
    }

    private string _fillHint = "在这里写答案";
    public string FillHint
    {
        get => _fillHint;
        private set => SetProperty(ref _fillHint, value);
    }

    public ObservableCollection<ExamOption> Options { get; } = new();

    private bool _canPrev;
    public bool CanPrev
    {
        get => _canPrev;
        private set => SetProperty(ref _canPrev, value);
    }

    private string _nextText = "下一题 ➡️";
    public string NextText
    {
        get => _nextText;
        private set => SetProperty(ref _nextText, value);
    }

    /// <summary>当前是不是最后一道题（最后一道时「下一题」即交卷）。</summary>
    public bool IsLastQuestion => _questions.Count > 0 && _index >= _questions.Count - 1;

    // ---------- 结果 ----------

    private bool _finished;
    public bool Finished
    {
        get => _finished;
        private set
        {
            if (SetProperty(ref _finished, value))
                OnPropertyChanged(nameof(NotFinished));
        }
    }

    public bool NotFinished => !_finished;

    private string _scoreText = string.Empty;
    public string ScoreText
    {
        get => _scoreText;
        private set => SetProperty(ref _scoreText, value);
    }

    private string _accuracyText = string.Empty;
    public string AccuracyText
    {
        get => _accuracyText;
        private set => SetProperty(ref _accuracyText, value);
    }

    private string _timeText = string.Empty;
    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    private string _resultText = string.Empty;
    public string ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    private string _resultEmoji = "📝";
    public string ResultEmoji
    {
        get => _resultEmoji;
        private set => SetProperty(ref _resultEmoji, value);
    }

    public ObservableCollection<ExamReviewItem> Review { get; } = new();

    public ExamPaper? Paper => _paper;

    // ---------- 流程 ----------

    public void Start(ExamPaper paper)
    {
        _paper = paper;
        _questions = QuestionBank.Instance.Questions(paper.Id);
        _answers = new string[_questions.Count];
        _index = 0;
        _startTicks = DateTime.Now.Ticks;
        _elapsedSeconds = 0;
        ElapsedText = "⏱ 00:00";

        Review.Clear();
        Finished = false;
        TitleText = paper.Title;
        PaperInfo = $"{paper.SubjectEmoji} {paper.SubjectName} · {paper.Subtitle}";

        if (_questions.Count == 0)
        {
            // 题库读不出来时不让孩子面对一个空页面
            ResultText = "这套卷子暂时打不开，回头再试试吧～";
            ScoreText = string.Empty;
            AccuracyText = string.Empty;
            TimeText = string.Empty;
            Finished = true;
            return;
        }

        LoadQuestion();
    }

    private ExamQuestion? Current => _index >= 0 && _index < _questions.Count ? _questions[_index] : null;

    private void LoadQuestion()
    {
        var q = Current;
        if (q == null) return;

        Stem = q.Stem;
        KindText = $"{q.KindEmoji} {q.KindName}";
        ProgressText = $"第 {_index + 1} / {_questions.Count} 题";

        Options.Clear();
        ShowOptions = q.Kind != ExamQuestionKind.Fill;
        ShowFill = q.Kind == ExamQuestionKind.Fill;
        FillHint = q.Kind == ExamQuestionKind.Fill ? "在这里写答案" : string.Empty;

        if (q.Kind == ExamQuestionKind.Choice)
        {
            for (var i = 0; i < q.Options.Count && i < 4; i++)
            {
                var key = ((char)('A' + i)).ToString();
                Options.Add(new ExamOption
                {
                    Key = key,
                    Display = $"{key}. {q.Options[i]}",
                    IsSelected = _answers[_index] == key
                });
            }
        }
        else if (q.Kind == ExamQuestionKind.Judge)
        {
            Options.Add(new ExamOption { Key = "对", Display = "✅ 对", IsSelected = _answers[_index] == "对" });
            Options.Add(new ExamOption { Key = "错", Display = "❌ 错", IsSelected = _answers[_index] == "错" });
        }

        FillInput = _answers[_index] ?? string.Empty;

        CanPrev = _index > 0;
        NextText = _index == _questions.Count - 1 ? "📊 交卷" : "下一题 ➡️";
        OnPropertyChanged(nameof(IsLastQuestion));
        UpdateAnswered();
    }

    /// <summary>点某个选项：单选语义，点新的就取消旧的。</summary>
    public void SelectOption(ExamOption option)
    {
        foreach (var o in Options) o.IsSelected = false;
        option.IsSelected = true;
        if (_index >= 0 && _index < _answers.Length)
            _answers[_index] = option.Key;
        UpdateAnswered();
    }

    /// <summary>把当前填空内容立刻落盘（Entry 失焦时调用，防止直接点「下一题」丢字符）。</summary>
    public void CommitFill() => FillInput = FillInput ?? string.Empty;

    public void Prev()
    {
        if (_index <= 0) return;
        _index--;
        LoadQuestion();
    }

    /// <summary>下一题；最后一道题时等同于交卷。</summary>
    public void Next()
    {
        if (_index >= _questions.Count - 1)
        {
            Submit();
            return;
        }
        _index++;
        LoadQuestion();
    }

    public void Submit()
    {
        if (Finished || _questions.Count == 0) return;

        var correct = 0;
        Review.Clear();

        for (var i = 0; i < _questions.Count; i++)
        {
            var q = _questions[i];
            var user = _answers[i] ?? string.Empty;
            var ok = ExamGrader.IsCorrect(q, user);
            if (ok) correct++;

            Review.Add(new ExamReviewItem
            {
                OrdinalText = $"第 {q.Ordinal} 题 · {q.KindName}",
                Stem = q.Stem,
                YourAnswer = DescribeAnswer(q, user),
                CorrectAnswer = DescribeCorrect(q),
                Explain = q.Explain,
                IsCorrect = ok
            });
        }

        var total = _questions.Count;
        var score = (int)System.Math.Round(100.0 * correct / total, MidpointRounding.AwayFromZero);
        var acc = (int)System.Math.Round(100.0 * correct / total, MidpointRounding.AwayFromZero);
        var secs = (DateTime.Now.Ticks - _startTicks) / TimeSpan.TicksPerSecond;

        ScoreText = $"{score} 分";
        AccuracyText = $"答对 {correct} / {total} 题 · 正确率 {acc}%";
        TimeText = $"⏱ 用时 {secs / 60:D2}:{secs % 60:D2}";

        if (score >= 90)
        {
            ResultEmoji = "🏆";
            ResultText = "太厉害了！这份卷子几乎全对，可以去挑战下一套了。";
        }
        else if (score >= 75)
        {
            ResultEmoji = "🌟";
            ResultText = "不错哦！把错题弄懂，下次就能冲 90 分。";
        }
        else if (score >= 60)
        {
            ResultEmoji = "💪";
            ResultText = "及格啦，错题下面的解析要认真看一遍。";
        }
        else
        {
            ResultEmoji = "📚";
            ResultText = "别灰心，先把错题的知识点补上，再来一次会好很多。";
        }

        Finished = true;
    }

    /// <summary>交卷前提示还剩几题没答（由页面决定是否弹确认框）。</summary>
    public int UnansweredCount
    {
        get
        {
            var n = 0;
            for (var i = 0; i < _answers.Length; i++)
                if (string.IsNullOrWhiteSpace(_answers[i])) n++;
            return n;
        }
    }

    /// <summary>页面每秒调用一次，更新计时文字。</summary>
    public void Tick()
    {
        if (Finished) return;
        _elapsedSeconds++;
        ElapsedText = $"⏱ {_elapsedSeconds / 60:D2}:{_elapsedSeconds % 60:D2}";
    }

    private void UpdateAnswered()
    {
        var done = 0;
        foreach (var a in _answers)
            if (!string.IsNullOrWhiteSpace(a)) done++;

        AnsweredText = $"已答 {done} / {_answers.Length}";
        OnPropertyChanged(nameof(UnansweredCount));
    }

    // ---------- 回顾文案 ----------

    private static string DescribeAnswer(ExamQuestion q, string user)
    {
        if (string.IsNullOrWhiteSpace(user)) return "（未作答）";

        if (q.Kind == ExamQuestionKind.Choice)
        {
            var i = user[0] - 'A';
            return i >= 0 && i < q.Options.Count ? $"{user}. {q.Options[i]}" : user;
        }
        return user;
    }

    private static string DescribeCorrect(ExamQuestion q)
    {
        if (q.Kind == ExamQuestionKind.Choice)
        {
            var key = q.Answer.Trim();
            var i = key.Length > 0 ? key[0] - 'A' : -1;
            return i >= 0 && i < q.Options.Count ? $"{key}. {q.Options[i]}" : key;
        }

        if (q.Kind == ExamQuestionKind.Fill)
            return q.Answer.Split('|', StringSplitOptions.RemoveEmptyEntries)[0];

        return q.Answer;
    }
}
