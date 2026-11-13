using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 手语图卡（聋 / 重听 · 高频词 + 手形 + 口型）。
/// 两种模式：
/// 「学一学」——逐张看 12 个生活高频词的手形与口型提示；
/// 「练一练」——只给手形描述，让孩子选出对应的词（再认任务）。
/// 依据：手势与口语共享语言脑网络，手语是完整的语言系统而非「翻译」；
/// 同时呈现字形、拼音与口型，可兼顾手语习得与唇读、构音练习。
/// </summary>
public partial class SignCardGamePage : ContentPage
{
    private const int QuizCount = 8;

    private readonly IReadOnlyList<SignWord> _words = SpecialTrainingData.SignWords;
    private readonly Random _rnd = new();

    private int _cardIndex;
    private int _quizIndex;
    private int _correct;
    private string _answer = "";
    private bool _locked;
    private bool _inQuiz;

    public SignCardGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        ShowCard(0);
    }

    // ---------------- 学一学 ----------------

    private void ShowCard(int index)
    {
        if (_words.Count == 0) return;

        // 循环浏览：到头绕回，孩子不会「卡在最后一张」
        _cardIndex = ((index % _words.Count) + _words.Count) % _words.Count;
        var w = _words[_cardIndex];

        CardEmoji.Text = w.Emoji;
        CardWord.Text = w.Word;
        CardPinyin.Text = w.Pinyin;
        CardHand.Text = w.HandShape;
        CardMouth.Text = w.MouthHint;

        ServiceProvider.Get<ISoundService>().PlayClick();
    }

    private void OnNextCard(object sender, EventArgs e) => ShowCard(_cardIndex + 1);
    private void OnPrevCard(object sender, EventArgs e) => ShowCard(_cardIndex - 1);

    // ---------------- 模式切换 ----------------

    private void OnToggleMode(object sender, EventArgs e)
    {
        if (_inQuiz) SwitchToLearn();
        else StartQuiz();
    }

    private void SwitchToLearn()
    {
        _inQuiz = false;
        LearnView.IsVisible = true;
        QuizView.IsVisible = false;
        ResultLayer.IsVisible = false;
        ModeButton.Text = "练一练";
    }

    private void OnStartQuiz(object sender, EventArgs e) => StartQuiz();

    private void OnRestartQuiz(object sender, EventArgs e) => StartQuiz();

    private void StartQuiz()
    {
        if (_words.Count < 3) return;

        _inQuiz = true;
        LearnView.IsVisible = false;
        QuizView.IsVisible = true;
        ResultLayer.IsVisible = false;
        ModeButton.Text = "看卡片";

        _quizIndex = 0;
        _correct = 0;
        QuizFeedback.Text = "";
        NextQuestion();
    }

    // ---------------- 练一练 ----------------

    private void NextQuestion()
    {
        if (_quizIndex >= QuizCount)
        {
            _ = FinishAsync();
            return;
        }

        _quizIndex++;
        QuizProgress.Text = $"第 {_quizIndex} / {QuizCount} 题";
        QuizFeedback.Text = "";

        var target = _words[_rnd.Next(_words.Count)];
        _answer = target.Word;

        // 干扰项：从词表里随机取不重复的两个
        var opts = new List<SignWord> { target };
        int guard = 0;
        while (opts.Count < 3 && guard++ < 80)
        {
            var w = _words[_rnd.Next(_words.Count)];
            if (opts.Any(o => o.Word == w.Word)) continue;
            opts.Add(w);
        }

        // 洗牌（Fisher-Yates）
        for (int i = opts.Count - 1; i > 0; i--)
        {
            int j = _rnd.Next(i + 1);
            (opts[i], opts[j]) = (opts[j], opts[i]);
        }

        QuizEmoji.Text = target.Emoji;
        QuizHand.Text = target.HandShape;
        BuildOptions(opts);
    }

    private void BuildOptions(List<SignWord> opts)
    {
        OptionsHost.Children.Clear();
        foreach (var o in opts)
        {
            var btn = new Button
            {
                Text = $"{o.Word}   {o.Pinyin}",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#00695C"),
                CornerRadius = 18,
                HeightRequest = 66,
                CommandParameter = o.Word
            };
            SemanticProperties.SetDescription(btn, o.Word);
            btn.Clicked += OnOptionClicked;
            OptionsHost.Children.Add(btn);
        }
    }

    private async void OnOptionClicked(object sender, EventArgs e)
    {
        if (_locked || sender is not Button btn) return;
        var picked = btn.CommandParameter?.ToString() ?? "";

        _locked = true;

        if (picked == _answer)
        {
            _correct++;
            A11y.Success();
            btn.BackgroundColor = Color.FromArgb("#A5D6A7");
            btn.TextColor = Colors.White;
            QuizFeedback.Text = $"✅ 对了！就是「{_answer}」";
            await A11y.SayAsync(_answer);
        }
        else
        {
            A11y.Fail();
            btn.BackgroundColor = Color.FromArgb("#FFCDD2");
            QuizFeedback.Text = $"再看一看，正确的是「{_answer}」";

            // 把正确答案直接标出来，比只说「错了」更有教学价值
            foreach (var c in OptionsHost.Children)
            {
                if (c is Button b && b.CommandParameter?.ToString() == _answer)
                {
                    b.BackgroundColor = Color.FromArgb("#C8E6C9");
                    b.TextColor = Color.FromArgb("#1B5E20");
                }
            }
        }

        await Task.Delay(1000);
        _locked = false;
        if (IsLoaded) NextQuestion();
    }

    private async Task FinishAsync()
    {
        ResultLayer.IsVisible = true;
        ResultTitle.Text = _correct >= 7 ? "认得真准！" : _correct >= 5 ? "不错，再巩固一下" : "多看看卡片再来";
        ResultDetail.Text = $"共 {QuizCount} 题，答对 {_correct} 题";

        await A11y.SayAsync($"练完啦，答对{_correct}题，一共{QuizCount}题");
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
