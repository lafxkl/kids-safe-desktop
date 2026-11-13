using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

public partial class WhackMoleGamePage : ContentPage
{
    private readonly WhackMoleGameViewModel _vm;
    private readonly WhackMoleDrawable _drawable = new();
    private readonly IDispatcherTimer _timer;

    public WhackMoleGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new WhackMoleGameViewModel();
        BindingContext = _vm;
        Canvas.Drawable = _drawable;
        _drawable.Vm = _vm;

        _timer = this.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_vm.GameOver) return;
        _vm.SetSize((float)Canvas.Width, (float)Canvas.Height);
        _vm.Update(0.033f);
        Canvas.Invalidate();
    }

    private void OnPointer(object? sender, TouchEventArgs e)
    {
        var p = e.Touches.First();
        _vm.OnTap(new PointF((float)p.X, (float)p.Y));
    }

    private async void OnReplay(object sender, EventArgs e) => _vm.Replay();

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.SetSize((float)Canvas.Width, (float)Canvas.Height);
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
