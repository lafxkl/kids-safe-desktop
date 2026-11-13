using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Media;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 英语学习：学习模式（看词卡 + 听发音）与练习模式（看英文选中文，即时批改 + 成绩报告）。
/// </summary>
public class EnglishStudyViewModel : BaseViewModel
{
    private readonly EnglishGrade _grade;

    public EnglishStudyViewModel(EnglishGrade grade)
    {
        _grade = grade;
        GradeTitle = grade.GradeTitle();
        var cats = EnglishData.Categories(grade);
        Categories = new ObservableCollection<EnglishCategory>(cats);
        CategoryNames.Add("全部");
        foreach (var c in cats) CategoryNames.Add(c.Name);
        SelectedCategory = "全部";
        ApplyCategory("全部");
    }

    public string GradeTitle { get; }

    // ===== 模式切换 =====
    private bool _isQuiz;
    public bool IsQuiz
    {
        get => _isQuiz;
        set
        {
            if (SetProperty(ref _isQuiz, value))
                OnPropertyChanged(nameof(IsLearn));
        }
    }
    public bool IsLearn => !_isQuiz;

    // ===== 学习模式 =====
    public ObservableCollection<EnglishCategory> Categories { get; }

    /// <summary>分类名称列表（含「全部」），供学习模式分类筛选。</summary>
    public ObservableCollection<string> CategoryNames { get; } = new();

    private string _selectedCategory = "全部";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                ApplyCategory(value);
        }
    }

    public ObservableCollection<EnglishWord> LearnWords { get; } = new();

    private int _learnIndex;
    private EnglishWord? _learnWord;
    public EnglishWord? LearnWord
    {
        get => _learnWord;
        private set => SetProperty(ref _learnWord, value);
    }

    private bool _showMeaning;
    public bool ShowMeaning
    {
        get => _showMeaning;
        set => SetProperty(ref _showMeaning, value);
    }

    public string LearnProgress => LearnWords.Count == 0
        ? "暂无单词"
        : $"第 {_learnIndex + 1} / {LearnWords.Count} 个";

    public bool LearnHasPrev => _learnIndex > 0;
    public bool LearnHasNext => _learnIndex < LearnWords.Count - 1;

    private void ApplyCategory(string category)
    {
        LearnWords.Clear();
        var list = category == "全部"
            ? EnglishData.Words(_grade)
            : EnglishData.Categories(_grade).FirstOrDefault(c => c.Name == category)?.Words ?? EnglishData.Words(_grade);
        foreach (var w in list) LearnWords.Add(w);
        _learnIndex = 0;
        ShowMeaning = false;
        LoadLearnWord();
    }

    private void LoadLearnWord()
    {
        LearnWord = LearnWords.Count == 0 ? null : LearnWords[_learnIndex];
        ShowMeaning = false;
        OnPropertyChanged(nameof(LearnProgress));
        OnPropertyChanged(nameof(LearnHasPrev));
        OnPropertyChanged(nameof(LearnHasNext));
    }

    public void LearnPrev()
    {
        if (_learnIndex > 0) { _learnIndex--; LoadLearnWord(); }
    }

    public void LearnNext()
    {
        if (_learnIndex < LearnWords.Count - 1) { _learnIndex++; LoadLearnWord(); }
    }

    public void ToggleMeaning() => ShowMeaning = !ShowMeaning;

    public void SpeakCurrent()
    {
        if (LearnWord != null) _ = SpeakAsync(LearnWord.Word);
    }

    // ===== 练习模式（选择题 + 批改）=====
    public ObservableCollection<EnglishQuizQuestion> QuizQuestions { get; } = new();
    private int _quizIndex;
    private EnglishQuizQuestion? _current;
    public EnglishQuizQuestion? CurrentQuestion
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    private int _quizScore;
    public string QuizScoreText
    {
        get => $"⭐ {_quizScore}";
        private set => OnPropertyChanged(nameof(QuizScoreText));
    }

    private string _quizProgress = string.Empty;
    public string QuizProgress
    {
        get => _quizProgress;
        private set => SetProperty(ref _quizProgress, value);
    }

    private bool _quizCorrect;
    public bool QuizCorrect
    {
        get => _quizCorrect;
        set => SetProperty(ref _quizCorrect, value);
    }

    private bool _quizWrong;
    public bool QuizWrong
    {
        get => _quizWrong;
        set => SetProperty(ref _quizWrong, value);
    }

    private string _quizFeedback = string.Empty;
    public string QuizFeedback
    {
        get => _quizFeedback;
        set => SetProperty(ref _quizFeedback, value);
    }

    public bool HasQuizFeedback => QuizCorrect || QuizWrong;

    private bool _quizFinished;
    public bool QuizFinished
    {
        get => _quizFinished;
        set
        {
            if (SetProperty(ref _quizFinished, value))
                OnPropertyChanged(nameof(QuizNotFinished));
        }
    }
    public bool QuizNotFinished => !_quizFinished;

    private string _quizResult = string.Empty;
    public string QuizResultText
    {
        get => _quizResult;
        private set => SetProperty(ref _quizResult, value);
    }
    private string _quizAccuracy = string.Empty;
    public string QuizAccuracyText
    {
        get => _quizAccuracy;
        private set => SetProperty(ref _quizAccuracy, value);
    }
    private string _quizTime = string.Empty;
    public string QuizTimeText
    {
        get => _quizTime;
        private set => SetProperty(ref _quizTime, value);
    }
    private string _quizTotal = string.Empty;
    public string QuizTotalText
    {
        get => _quizTotal;
        private set => SetProperty(ref _quizTotal, value);
    }

    public ObservableCollection<WrongRecord> QuizWrongRecords { get; } = new();

    private long _quizStartTicks;
    private int _quizCorrectCount;

    public void StartQuiz()
    {
        var difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        var count = difficulty.Rounds(); // 简单 6 / 中等 8 / 困难 10
        var qs = EnglishData.BuildQuiz(_grade, count);
        // 题库不可用（极端情况）时给个提示，别让页面空转
        if (qs.Count == 0)
        {
            QuizFeedback = "词库还在准备，稍后再试一次吧～";
            return;
        }
        QuizQuestions.Clear();
        foreach (var q in qs) QuizQuestions.Add(q);
        _quizIndex = 0;
        _quizScore = 0;
        _quizCorrectCount = 0;
        QuizWrongRecords.Clear();
        QuizFinished = false;
        QuizCorrect = false;
        QuizWrong = false;
        QuizFeedback = string.Empty;
        OnPropertyChanged(nameof(QuizScoreText));
        _quizStartTicks = System.DateTime.Now.Ticks;
        LoadQuizQuestion();
    }

    private void LoadQuizQuestion()
    {
        QuizCorrect = false;
        QuizWrong = false;
        QuizFeedback = string.Empty;
        CurrentQuestion = QuizQuestions[_quizIndex];
        QuizProgress = $"第 {_quizIndex + 1} / {QuizQuestions.Count} 题";
    }

    public void ChooseOption(int idx)
    {
        var q = CurrentQuestion;
        if (q == null || q.Answered) return;
        q.Chosen = idx;
        q.Answered = true;
        if (idx == q.CorrectIndex)
        {
            _quizCorrectCount++;
            _quizScore += 10;
            QuizCorrect = true;
            QuizWrong = false;
            QuizFeedback = "答对啦！🌟";
            OnPropertyChanged(nameof(QuizScoreText));
            ServiceProvider.Get<ISoundService>().PlayCorrect();
            _ = SpeakAsync("Good job");
        }
        else
        {
            QuizWrong = true;
            QuizCorrect = false;
            QuizFeedback = "再看看～ 正确答案在下面哦";
            ServiceProvider.Get<ISoundService>().PlayWrong();
            _ = SpeakAsync("Try again");
        }
    }

    public void QuizNext()
    {
        var q = CurrentQuestion;
        if (q != null && (!q.Answered || (q.Answered && q.Chosen != q.CorrectIndex)))
        {
            // 未作答或答错 → 记错题
            QuizWrongRecords.Add(new WrongRecord
            {
                Expression = $"{q.Word.Emoji} {q.Word.Word}",
                CorrectText = q.Word.Meaning,
                YourText = q.Chosen >= 0 && q.Chosen < q.Options.Count ? q.Options[q.Chosen] : "（未作答）"
            });
        }

        _quizIndex++;
        if (_quizIndex >= QuizQuestions.Count)
        {
            FinishQuiz();
            return;
        }
        LoadQuizQuestion();
    }

    private void FinishQuiz()
    {
        QuizFinished = true;
        var total = QuizQuestions.Count;
        var acc = total == 0 ? 0 : (int)(100.0 * _quizCorrectCount / total);
        var secs = (System.DateTime.Now.Ticks - _quizStartTicks) / System.TimeSpan.TicksPerSecond;
        QuizTotalText = $"共 {total} 题，答对 {_quizCorrectCount} 题";
        QuizAccuracyText = $"正确率 {acc}%";
        OnPropertyChanged(nameof(QuizScoreText));
        QuizTimeText = $"⏱ 用时 {secs} 秒";
        var sound = ServiceProvider.Get<ISoundService>();
        if (_quizCorrectCount == total)
        {
            QuizResultText = "🎉 全对啦！你是英语小能手！";
            sound.PlayWin();
        }
        else if (acc >= 80)
        {
            QuizResultText = "🌟 很棒！再多练练就全对！";
            sound.PlayWin();
        }
        else
        {
            QuizResultText = "💪 不错哦，多听多读会更厉害！";
            sound.PlayCorrect();
        }
    }

    public void Replay() => StartQuiz();

    public void SwitchToQuiz()
    {
        IsQuiz = true;
        if (QuizQuestions.Count == 0) StartQuiz();
    }

    public void SwitchToLearn() => IsQuiz = false;

    // ===== 英文语音播报（交由统一 Tts 助手处理，自动选英文语音包，失败回退默认）=====
    private static async Task SpeakAsync(string text)
        => await Helpers.Tts.SpeakAsync(text, "en");
}
