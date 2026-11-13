using System.Collections.ObjectModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 口算练习 + 批改：按年级/题型出题，儿童输入答案，即时批改并生成成绩报告。
/// </summary>
public class OralPracticeViewModel : BaseViewModel
{
    private List<OralProblem> _deck = new();
    private int _index;
    private int _score;
    private int _correct;
    private bool _currentLocked;
    private string _lastInput = string.Empty;

    private MathGrade _grade = MathGrade.Grade1;
    private OralTopic _topic = OralTopic.All;

    public MathGrade Grade
    {
        get => _grade;
        set => SetProperty(ref _grade, value);
    }

    public string GradeTitle => _grade.GradeTitle();

    private string _expression = string.Empty;
    public string Expression
    {
        get => _expression;
        set => SetProperty(ref _expression, value);
    }

    private string _category = string.Empty;
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    private string _userInput = string.Empty;
    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    private string _scoreText = "⭐ 0";
    public string ScoreText
    {
        get => _scoreText;
        set => SetProperty(ref _scoreText, value);
    }

    private bool _isCorrect;
    public bool IsCorrect
    {
        get => _isCorrect;
        set => SetProperty(ref _isCorrect, value);
    }

    private bool _isWrong;
    public bool IsWrong
    {
        get => _isWrong;
        set => SetProperty(ref _isWrong, value);
    }

    private string _feedback = string.Empty;
    public string Feedback
    {
        get => _feedback;
        set => SetProperty(ref _feedback, value);
    }

    private bool _showAnswer;
    public bool ShowAnswer
    {
        get => _showAnswer;
        set => SetProperty(ref _showAnswer, value);
    }

    private string _correctAnswerText = string.Empty;
    public string CorrectAnswerText
    {
        get => _correctAnswerText;
        set => SetProperty(ref _correctAnswerText, value);
    }

    public bool HasFeedback => IsCorrect || IsWrong;

    private bool _finished;
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

    private string _resultText = string.Empty;
    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    private string _accuracyText = string.Empty;
    public string AccuracyText
    {
        get => _accuracyText;
        set => SetProperty(ref _accuracyText, value);
    }

    private string _timeText = string.Empty;
    public string TimeText
    {
        get => _timeText;
        set => SetProperty(ref _timeText, value);
    }

    private string _totalText = string.Empty;
    public string TotalText
    {
        get => _totalText;
        set => SetProperty(ref _totalText, value);
    }

    public ObservableCollection<WrongRecord> WrongRecords { get; } = new();

    private long _startTicks;

    public void Start(MathGrade grade, OralTopic topic)
    {
        _grade = grade;
        _topic = topic;
        Begin();
    }

    public void Start()
    {
        // 沿用当前年级/题型重新开始
        Begin();
    }

    private void Begin()
    {
        WrongRecords.Clear();
        var difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        var count = difficulty.Rounds(); // 简单 6 / 中等 8 / 困难 10

        // 一年级～四年级是纯四则，程序现场出题（无限不重复）；
        // 五年级起题型扩展到小数/分数/有理数/方程/根式，改由 SQL 题库供题。
        _deck = _grade >= MathGrade.Grade5
            ? QuestionBank.Instance.PickMath((int)_grade + 1, _topic, count)
            : OralGenerator.Generate(_grade, _topic, count);

        // 题库不可用（极端情况）时退回程序出题，至少能玩
        if (_deck.Count == 0)
            _deck = OralGenerator.Generate(MathGrade.Grade4, _topic, count);
        _index = 0;
        _score = 0;
        _correct = 0;
        OnPropertyChanged(nameof(GradeTitle));
        Finished = false;
        UpdateScore();
        _startTicks = System.DateTime.Now.Ticks;
        LoadProblem();
    }

    private void LoadProblem()
    {
        IsCorrect = false;
        IsWrong = false;
        Feedback = string.Empty;
        ShowAnswer = false;
        CorrectAnswerText = string.Empty;
        _currentLocked = false;
        UserInput = string.Empty;
        _lastInput = string.Empty;

        var p = _deck[_index];
        Expression = p.Expression;
        Category = p.Category;
        ProgressText = $"第 {_index + 1} / {_deck.Count} 题 · {p.Category}";
        // TTS 实时语音：把算式用中文数字朗读出来（如「二十三 加 五 等于 多少」）
        _ = SpeakAsync(BuildSpoken(p.Expression));
    }

    /// <summary>
    /// 把算式转成中文语音，例如 "23 + 5 = ?" → "二十三 加 五 等于 多少"。
    /// 五～九年级的算式更复杂（负数、平方、根号、括号、小数），按字符逐个数：
    /// 「-」在数字/括号后面读「减」，在开头或括号内读「负」。
    /// </summary>
    private static string BuildSpoken(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return "算一算";

        var sb = new System.Text.StringBuilder();
        var num = new System.Text.StringBuilder();
        var prev = ' ';

        void FlushNum()
        {
            if (num.Length == 0) return;
            var t = num.ToString();
            var dot = t.IndexOf('.');
            if (dot < 0)
            {
                if (int.TryParse(t, out var n)) sb.Append(ChineseNumber.ToChinese(n));
                else sb.Append(t);
            }
            else
            {
                if (int.TryParse(t[..dot], out var i)) sb.Append(ChineseNumber.ToChinese(i));
                sb.Append('点');
                foreach (var ch in t[(dot + 1)..])
                    sb.Append(ChineseNumber.ToChinese(ch - '0'));
            }
            num.Clear();
        }

        // 上一个字符是不是「一个数/字母」的结尾：决定 - 读「减」还是「负」
        static bool EndsOperand(char c)
            => char.IsDigit(c) || c == ')' || c == '²' || c == '³' || c == '°'
               || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == 'π';

        foreach (var ch in expr)
        {
            if (char.IsDigit(ch) || ch == '.')
            {
                num.Append(ch);
                continue;
            }
            FlushNum();

            switch (ch)
            {
                case ' ' or '，' or '、': sb.Append(' '); break;
                case '+': sb.Append(" 加 "); break;
                case '-': sb.Append(EndsOperand(prev) ? " 减 " : "负"); break;
                case '×' or '·': sb.Append(" 乘 "); break;
                case '÷' or '/': sb.Append(" 除以 "); break;
                case '=': sb.Append(" 等于 "); break;
                case '?': sb.Append(" 多少 "); break;
                case '(': sb.Append("括号"); break;
                case ')': sb.Append("括号 "); break;
                case '²': sb.Append("的平方 "); break;
                case '³': sb.Append("的立方 "); break;
                case '√': sb.Append("根号 "); break;
                case '∛': sb.Append("三次根号 "); break;
                case '°': sb.Append("度 "); break;
                case '|': sb.Append("绝对值 "); break;
                case ':': sb.Append("比"); break;
                case '≤': sb.Append("小于等于"); break;
                case '≥': sb.Append("大于等于"); break;
                case '<': sb.Append("小于"); break;
                case '>': sb.Append("大于"); break;
                default: sb.Append(ch); break;
            }
            prev = ch;
        }
        FlushNum();

        var text = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return string.IsNullOrEmpty(text) ? "算一算" : text;
    }

    public void Submit()
    {
        if (_currentLocked || Finished) return;
        var text = (UserInput ?? string.Empty).Trim().Replace('−', '-').Replace("。", ".");
        if (string.IsNullOrEmpty(text)
            || !double.TryParse(text, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var val))
        {
            Feedback = "先写一个数字吧～";
            IsWrong = false;
            IsCorrect = false;
            return;
        }

        var p = _deck[_index];
        if (System.Math.Abs(val - p.Answer) < 0.0001)
        {
            _correct++;
            _score += 10;
            IsCorrect = true;
            IsWrong = false;
            Feedback = "答对啦！🌟";
            _currentLocked = true;
            UpdateScore();
            ServiceProvider.Get<ISoundService>().PlayCorrect();
            _ = SpeakAsync("真棒");
        }
        else
        {
            IsWrong = true;
            IsCorrect = false;
            Feedback = "再想想～ 正确答案在下面哦";
            ShowAnswer = true;
            CorrectAnswerText = "正确答案：" + p.AnswerText;
            _lastInput = text;
            ServiceProvider.Get<ISoundService>().PlayWrong();
            _ = SpeakAsync("不对哦");
        }
    }

    public void Next()
    {
        if (Finished) return;
        // 没做对（答错或未作答）记入错题
        if (!_currentLocked)
        {
            var p = _deck[_index];
            WrongRecords.Add(new WrongRecord
            {
                Expression = p.Expression,
                CorrectText = p.AnswerText,
                YourText = string.IsNullOrEmpty(_lastInput) ? "（未作答）" : _lastInput
            });
        }

        _index++;
        if (_index >= _deck.Count)
        {
            Finish();
            return;
        }
        LoadProblem();
    }

    private void Finish()
    {
        Finished = true;
        var total = _deck.Count;
        var acc = total == 0 ? 0 : (int)(100.0 * _correct / total);
        var secs = (System.DateTime.Now.Ticks - _startTicks) / System.TimeSpan.TicksPerSecond;
        TotalText = $"共 {total} 题，答对 {_correct} 题";
        AccuracyText = $"正确率 {acc}%";
        ScoreText = $"⭐ {_score}";
        TimeText = $"⏱ 用时 {secs} 秒";
        var sound = ServiceProvider.Get<ISoundService>();
        if (_correct == total)
        {
            ResultText = "🎉 全对啦！你是口算小状元！";
            sound.PlayWin();
        }
        else if (acc >= 80)
        {
            ResultText = "🌟 很棒！再练练就能全对！";
            sound.PlayWin();
        }
        else
        {
            ResultText = "💪 不错哦，多练习会更厉害！";
            sound.PlayCorrect();
        }
    }

    public void Replay() => Start();

    /// <summary>重新朗读当前题目（供「🔊 读题」按钮调用）。</summary>
    public void SpeakCurrent() => _ = SpeakAsync(BuildSpoken(_expression));

    public void SetTopic(OralTopic topic)
    {
        _topic = topic;
        Start();
    }

    private static System.Threading.Tasks.Task SpeakAsync(string text)
    {
        return Helpers.Tts.SpeakAsync(text, "zh");
    }

    private void UpdateScore() => ScoreText = $"⭐ {_score}";
}
