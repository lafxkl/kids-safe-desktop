using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class EnglishStudyPage : ContentPage
{
    private readonly EnglishStudyViewModel _vm;
    private readonly List<Button> _catButtons = new();
    private readonly Button[] _optButtons = new Button[4];

    private static readonly Color OptBg = Color.FromArgb("#E1F5FE");
    private static readonly Color OptBgSel = Color.FromArgb("#7E57C2");
    private static readonly Color OptText = Color.FromArgb("#311B92");
    private static readonly Color CorrectBg = Color.FromArgb("#2E7D32");
    private static readonly Color WrongBg = Color.FromArgb("#E53935");

    public EnglishStudyPage(EnglishGrade grade)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new EnglishStudyViewModel(grade);
        BindingContext = _vm;
        TitleLabel.Text = $"{grade.GradeTitle()} · 英语学习";

        BuildCategoryChips();
        _optButtons[0] = BtnOpt0;
        _optButtons[1] = BtnOpt1;
        _optButtons[2] = BtnOpt2;
        _optButtons[3] = BtnOpt3;
        UpdateModeUI();
    }

    // 便于 XAML 设计器/无参创建的安全构造
    public EnglishStudyPage() : this(EnglishGrade.Grade1) { }

    private void BuildCategoryChips()
    {
        foreach (var name in _vm.CategoryNames)
        {
            var b = new Button
            {
                Text = name,
                TextColor = OptText,
                BackgroundColor = OptBg,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                CornerRadius = 20,
                Padding = new Thickness(16, 8),
                BorderWidth = 0,
                CommandParameter = name
            };
            b.Clicked += OnCategoryTapped;
            CategoryStack.Children.Add(b);
            _catButtons.Add(b);
        }
        UpdateCategoryUI();
    }

    private void UpdateCategoryUI()
    {
        foreach (var b in _catButtons)
        {
            var sel = b.CommandParameter is string s && s == _vm.SelectedCategory;
            b.BackgroundColor = sel ? OptBgSel : OptBg;
            b.TextColor = sel ? Colors.White : OptText;
        }
    }

    private void OnCategoryTapped(object sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string name)
        {
            _vm.SelectedCategory = name;
            UpdateCategoryUI();
        }
    }

    // ===== 模式切换 =====
    private void OnModeLearn(object sender, EventArgs e)
    {
        _vm.SwitchToLearn();
        UpdateModeUI();
    }

    private void OnModeQuiz(object sender, EventArgs e)
    {
        _vm.SwitchToQuiz();
        UpdateModeUI();
        LoadQuizQuestionUI();
    }

    private void UpdateModeUI()
    {
        var learn = _vm.IsLearn;
        BtnLearn.BackgroundColor = learn ? OptBgSel : Color.FromArgb("#EDE7F6");
        BtnLearn.TextColor = learn ? Colors.White : OptText;
        BtnQuiz.BackgroundColor = !learn ? OptBgSel : Color.FromArgb("#EDE7F6");
        BtnQuiz.TextColor = !learn ? Colors.White : OptText;
        if (!learn) LoadQuizQuestionUI();
    }

    // ===== 学习模式 =====
    private void OnToggleMeaning(object sender, EventArgs e) => _vm.ToggleMeaning();

    private void OnLearnPrev(object sender, EventArgs e) => _vm.LearnPrev();

    private void OnLearnNext(object sender, EventArgs e) => _vm.LearnNext();

    private void OnSpeakCurrent(object sender, EventArgs e)
    {
        if (_vm.IsQuiz && _vm.CurrentQuestion != null)
            _ = SpeakAsync(_vm.CurrentQuestion.Word.Word);
        else if (_vm.LearnWord != null)
            _ = SpeakAsync(_vm.LearnWord.Word);
    }

    // ===== 练习模式 =====
    private void LoadQuizQuestionUI()
    {
        var q = _vm.CurrentQuestion;
        if (q == null) return;
        for (var i = 0; i < _optButtons.Length; i++)
        {
            _optButtons[i].Text = i < q.Options.Count ? q.Options[i] : string.Empty;
            _optButtons[i].BackgroundColor = OptBg;
            _optButtons[i].TextColor = OptText;
            _optButtons[i].IsEnabled = true;
        }
    }

    private void OnOption0(object sender, EventArgs e) => OnOptionTapped(0);
    private void OnOption1(object sender, EventArgs e) => OnOptionTapped(1);
    private void OnOption2(object sender, EventArgs e) => OnOptionTapped(2);
    private void OnOption3(object sender, EventArgs e) => OnOptionTapped(3);

    private void OnOptionTapped(int idx)
    {
        var q = _vm.CurrentQuestion;
        if (q == null || q.Answered) return;
        _vm.ChooseOption(idx);
        // 着色：正确项绿色，选错项红色
        for (var i = 0; i < _optButtons.Length; i++)
        {
            _optButtons[i].IsEnabled = false;
            if (i == q.CorrectIndex)
            {
                _optButtons[i].BackgroundColor = CorrectBg;
                _optButtons[i].TextColor = Colors.White;
            }
            else if (i == q.Chosen)
            {
                _optButtons[i].BackgroundColor = WrongBg;
                _optButtons[i].TextColor = Colors.White;
            }
        }
    }

    private void OnQuizSpeak(object sender, EventArgs e)
    {
        if (_vm.CurrentQuestion != null)
            _ = SpeakAsync(_vm.CurrentQuestion.Word.Word);
    }

    private void OnQuizNext(object sender, EventArgs e)
    {
        _vm.QuizNext();
        if (!_vm.QuizFinished) LoadQuizQuestionUI();
    }

    private void OnQuizReplay(object sender, EventArgs e)
    {
        _vm.Replay();
        LoadQuizQuestionUI();
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    private static async Task SpeakAsync(string text)
        => await Helpers.Tts.SpeakAsync(text, "en");

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ServiceProvider.Get<IScreenTimeService>().LimitReached += OnLimitReached;
        if (ServiceProvider.Get<IScreenTimeService>().IsLockedOut)
            _ = GoLockoutAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached;
    }

    private async void OnLimitReached(object? sender, EventArgs e) => await GoLockoutAsync();

    private async Task GoLockoutAsync()
    {
        if (Navigation.NavigationStack.LastOrDefault() is LockoutPage) return;
        await Navigation.PushAsync(new LockoutPage());
    }
}
