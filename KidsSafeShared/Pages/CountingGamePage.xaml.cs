using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class CountingGamePage : ContentPage
{
    private readonly CountingGameViewModel _vm;

    public CountingGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new CountingGameViewModel();
        BindingContext = _vm;
        _vm.Start();
    }

    private void OnOptionClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not NumberOption opt)
            return;
        if (_vm.IsCorrect) return;

        if (_vm.Submit(opt))
            Device.StartTimer(TimeSpan.FromMilliseconds(500), () => { _vm.Next(); return false; });
    }

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
