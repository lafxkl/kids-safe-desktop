using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class SafetyQuizGamePage : ContentPage
{
    private readonly SafetyQuizGameViewModel _vm;

    public SafetyQuizGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new SafetyQuizGameViewModel();
        BindingContext = _vm;
        _vm.Start();
    }

    private void OnOptionTapped(object sender, EventArgs e)
    {
        if (sender is not View view || view.BindingContext is not SafetyOption opt) return;
        if (_vm.IsCorrect) return;
        if (_vm.Submit(opt))
        {
            // 答对后展示讲解与「下一题」按钮，由按钮触发 Next
        }
    }

    private void OnNext(object sender, EventArgs e) => _vm.Next();

    private void OnSpeak(object sender, EventArgs e) => _vm.SpeakQuestion();

    private async void OnReplay(object sender, EventArgs e) => _vm.Replay();

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
