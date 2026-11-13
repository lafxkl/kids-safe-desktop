using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class PictureWordGamePage : ContentPage
{
    private readonly PictureWordGameViewModel _vm;

    public PictureWordGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new PictureWordGameViewModel();
        BindingContext = _vm;
        _vm.Start();
    }

    private void OnOptionClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not WordOption opt)
            return;
        if (_vm.IsCorrect) return; // 本轮已答对，等待自动进入下一张

        // 同步提交，反馈即时出现；答对后用极短的定时器进入下一张，
        // 避免原来 900ms 的 Task.Delay 让人觉得卡顿。
        if (_vm.Submit(opt))
            Device.StartTimer(TimeSpan.FromMilliseconds(500), () => { _vm.Next(); return false; });
    }

    private async void OnReplay(object sender, EventArgs e) => _vm.Replay();

    private void OnSpeak(object sender, EventArgs e) => _vm.SpeakCurrent();

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
