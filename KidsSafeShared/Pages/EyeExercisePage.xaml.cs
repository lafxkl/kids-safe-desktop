using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace KidsSafeDesktop.Pages;

public partial class EyeExercisePage : ContentPage
{
    private readonly EyeExerciseViewModel _vm;
    private readonly IDispatcherTimer _timer;

    public EyeExercisePage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "眼保健操", "👀", "eye", 1, "做了一次眼保健操");
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new EyeExerciseViewModel();
        BindingContext = _vm;

        _timer = this.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += (_, _) => _vm.Tick(0.1f);
    }

    private void OnToggle(object sender, EventArgs e) => _vm.TogglePlay();

    private void OnPrev(object sender, EventArgs e) => _vm.Prev();

    private void OnNext(object sender, EventArgs e) => _vm.Next();

    private void OnRestart(object sender, EventArgs e) => _vm.Restart();

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Start();
        _timer.Start();
        ServiceProvider.Get<IScreenTimeService>().LimitReached += OnLimitReached;
        if (ServiceProvider.Get<IScreenTimeService>().IsLockedOut)
            _ = GoLockoutAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached;
    }

    private async void OnLimitReached(object? sender, EventArgs e) => await GoLockoutAsync();

    private async Task GoLockoutAsync()
    {
        if (Navigation.NavigationStack.LastOrDefault() is LockoutPage) return;
        await Navigation.PushAsync(new LockoutPage());
    }
}
