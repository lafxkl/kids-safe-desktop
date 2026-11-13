using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class SafetyKnowledgePage : ContentPage
{
    private readonly SafetyKnowledgeViewModel _vm;

    public SafetyKnowledgePage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "安全知识", "🛡️", "safety", 2, "在安全知识里学自护");
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new SafetyKnowledgeViewModel();
        BindingContext = _vm;
    }

    private async void OnTopicTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is SafetyTopic topic)
        {
            await Navigation.PushAfterPressAsync(view, () => new SafetyKnowledgeDetailPage(topic));
        }
    }

    private async void OnQuiz(object sender, EventArgs e)
        => await Navigation.PushAsync(new SafetyQuizGamePage());

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

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
