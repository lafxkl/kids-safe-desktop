using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class MathGamePage : ContentPage
{
    private readonly MathGameViewModel _vm;

    public MathGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new MathGameViewModel();
        BindingContext = _vm;
        _vm.Start();
    }

    private async void OnOptionTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is MathOption opt)
        {
            view.Pop();
            _vm.Submit(opt);
        }
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    private void OnNext(object sender, EventArgs e) => _vm.Next();

    private void OnReplay(object sender, EventArgs e) => _vm.Replay();

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
