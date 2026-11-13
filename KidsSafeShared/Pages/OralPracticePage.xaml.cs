using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class OralPracticePage : ContentPage
{
    private readonly OralPracticeViewModel _vm;

    public OralPracticePage(MathGrade grade, OralTopic topic)
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "口才练习", "🎤", "oral", 2, "完成一次口才练习");
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new OralPracticeViewModel();
        BindingContext = _vm;
        TitleLabel.Text = $"{grade.GradeTitle()} · 口算练习";
        _vm.Start(grade, topic);
    }

    // 便于 XAML 设计器/无参创建的安全构造
    public OralPracticePage() : this(MathGrade.Grade1, OralTopic.All) { }

    private void OnSubmit(object sender, EventArgs e) => _vm.Submit();

    private void OnEntryCompleted(object sender, EventArgs e) => _vm.Submit();

    private void OnSpeak(object sender, EventArgs e) => _vm.SpeakCurrent();

    /// <summary>切换正负号：答案可能是负数（初一有理数运算），数字键盘不一定带负号。</summary>
    private void OnToggleSign(object sender, EventArgs e)
    {
        var text = (_vm.UserInput ?? string.Empty).Trim();
        _vm.UserInput = text.StartsWith('-') ? text[1..] : "-" + text;
        AnswerEntry.Focus();
    }

    private void OnNext(object sender, EventArgs e) => _vm.Next();

    private void OnReplay(object sender, EventArgs e) => _vm.Replay();

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    private void OnTopic(object sender, EventArgs e)
    {
        OralTopic topic = OralTopic.All;
        if (sender == BtnAdd) topic = OralTopic.Add;
        else if (sender == BtnSub) topic = OralTopic.Sub;
        else if (sender == BtnMul) topic = OralTopic.Mul;
        else if (sender == BtnDiv) topic = OralTopic.Div;
        else if (sender == BtnMix) topic = OralTopic.Mixed;
        _vm.SetTopic(topic);
    }

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
