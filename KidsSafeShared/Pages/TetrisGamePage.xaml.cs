using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 俄罗斯方块。棋盘绘制在 <see cref="TetrisDrawable"/>，规则在 <see cref="TetrisGameViewModel"/>。
/// 操作既可以用底部四个大按钮（手机），也可以点棋盘左右两侧（习惯滑动的人会顺手去点）。
/// </summary>
public partial class TetrisGamePage : ContentPage
{
    private readonly TetrisGameViewModel _vm = new();
    private readonly TetrisDrawable _drawable = new();
    private readonly IDispatcherTimer _timer;

    public TetrisGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BindingContext = _vm;
        _drawable.Vm = _vm;
        Canvas.Drawable = _drawable;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _vm.Update(0.033f);
        Canvas.Invalidate();
    }

    private void OnLeft(object sender, EventArgs e)
    {
        _vm.MoveLeft();
        Canvas.Invalidate();
    }

    private void OnRight(object sender, EventArgs e)
    {
        _vm.MoveRight();
        Canvas.Invalidate();
    }

    private void OnRotate(object sender, EventArgs e)
    {
        _vm.Rotate();
        Canvas.Invalidate();
    }

    private void OnHardDrop(object sender, EventArgs e)
    {
        _vm.HardDrop();
        Canvas.Invalidate();
    }

    private void OnReplay(object sender, EventArgs e)
    {
        _vm.Replay();
        Canvas.Invalidate();
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Start();
        Canvas.Invalidate();
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
