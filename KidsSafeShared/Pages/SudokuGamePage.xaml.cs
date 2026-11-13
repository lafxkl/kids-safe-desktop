using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class SudokuGamePage : ContentPage
{
    private readonly SudokuGameViewModel _vm;

    public SudokuGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new SudokuGameViewModel();
        BindingContext = _vm;
        _vm.Start();
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    private void OnCellTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is SudokuCell cell)
            _vm.SelectCell(cell);
    }

    private void OnNumber(object sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out var n))
            _vm.Input(n);
    }

    private void OnErase(object sender, EventArgs e) => _vm.Erase();

    private void OnNew(object sender, EventArgs e) => _vm.Start();

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
