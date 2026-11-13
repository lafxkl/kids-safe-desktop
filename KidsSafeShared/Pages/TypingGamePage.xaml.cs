using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class TypingGamePage : ContentPage
{
    private readonly TypingGameViewModel _vm;

    public TypingGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new TypingGameViewModel();
        BindingContext = _vm;
        _vm.Start();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // 自动弹出软键盘，方便直接输入
        InputEntry.Focus();
        ServiceProvider.Get<IScreenTimeService>().LimitReached += OnLimitReached;
        if (ServiceProvider.Get<IScreenTimeService>().IsLockedOut)
            _ = GoLockoutAsync();
    }

    private void OnListen(object sender, EventArgs e) => _vm.SpeakCurrent();

    private void OnEntryCompleted(object sender, EventArgs e) => _vm.Submit();

    private void OnSkip(object sender, EventArgs e) => _vm.Next();

    private void OnReplay(object sender, EventArgs e)
    {
        _vm.Replay();
        InputEntry.Focus();
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

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
