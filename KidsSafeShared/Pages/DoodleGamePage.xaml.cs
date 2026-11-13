using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Media;
using System.IO;

namespace KidsSafeDesktop.Pages;

public partial class DoodleGamePage : ContentPage
{
    private readonly DoodleGameViewModel _vm;
    private readonly DoodleDrawable _drawable = new();
    private DoodleStroke? _current;

    public DoodleGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new DoodleGameViewModel();
        BindingContext = _vm;
        Canvas.Drawable = _drawable;
        UpdateBrushButtons();
    }

    // —— 绘制 ——
    private void OnDrawStart(object? sender, TouchEventArgs e)
    {
        var p = e.Touches.First();
        var color = _vm.IsEraser ? Colors.White : Color.FromArgb(_vm.SelectedColor);
        var width = _vm.IsEraser ? _vm.SelectedBrush * 2f : _vm.SelectedBrush;
        _current = new DoodleStroke { Color = color, Width = width };
        _current.Points.Add(new PointF((float)p.X, (float)p.Y));
        _drawable.Strokes.Add(_current);
        Canvas.Invalidate();
    }

    private void OnDrawDrag(object? sender, TouchEventArgs e)
    {
        if (_current is null) return;
        var p = e.Touches.First();
        _current.Points.Add(new PointF((float)p.X, (float)p.Y));
        Canvas.Invalidate();
    }

    private void OnDrawEnd(object? sender, TouchEventArgs e) => _current = null;

    private void OnColorTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is DoodleColorItem item)
        {
            _vm.SelectColor(item.Hex);
            if (_vm.IsEraser) _vm.ToggleEraser(); // 选色自动退出橡皮
            UpdateEraserButton();
        }
    }

    // —— 工具栏 ——
    private void OnEraser(object sender, EventArgs e)
    {
        _vm.ToggleEraser();
        UpdateEraserButton();
    }

    private void OnBrushS(object sender, EventArgs e) { _vm.SelectBrush(6); UpdateBrushButtons(); }
    private void OnBrushM(object sender, EventArgs e) { _vm.SelectBrush(12); UpdateBrushButtons(); }
    private void OnBrushL(object sender, EventArgs e) { _vm.SelectBrush(22); UpdateBrushButtons(); }
    private void OnBrushXL(object sender, EventArgs e) { _vm.SelectBrush(36); UpdateBrushButtons(); }

    private void OnUndo(object sender, EventArgs e)
    {
        if (_drawable.Strokes.Count > 0)
        {
            _drawable.Strokes.RemoveAt(_drawable.Strokes.Count - 1);
            Canvas.Invalidate();
        }
    }

    private void OnClear(object sender, EventArgs e)
    {
        _drawable.Strokes.Clear();
        Canvas.Invalidate();
    }

    private void UpdateEraserButton()
    {
        BtnEraser.BackgroundColor = _vm.IsEraser ? Color.FromArgb("#AB47BC") : Colors.White;
        BtnEraser.TextColor = _vm.IsEraser ? Colors.White : Color.FromArgb("#AB47BC");
    }

    private void UpdateBrushButtons()
    {
        var map = new (Button Btn, int Size)[]
        {
            (BtnBrushS, 6), (BtnBrushM, 12), (BtnBrushL, 22), (BtnBrushXL, 36)
        };
        foreach (var (btn, size) in map)
        {
            var on = _vm.SelectedBrush == size;
            btn.BackgroundColor = on ? Color.FromArgb("#AB47BC") : Colors.White;
            btn.TextColor = on ? Colors.White : Color.FromArgb("#AB47BC");
        }
        UpdateEraserButton();
    }

    // —— 保存 ——
    private async void OnSave(object sender, EventArgs e)
    {
        try
        {
            if (!Screenshot.Default.IsCaptureSupported)
            {
                await DisplayAlert("提示", "当前设备暂不支持保存截图。", "好的");
                return;
            }
            var result = await Screenshot.Default.CaptureAsync();
            var dir = Path.Combine(FileSystem.AppDataDirectory, "Drawings");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"doodle_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using (var dst = File.Create(file))
            {
                await result.CopyToAsync(dst);
            }
            ServiceProvider.Get<ISoundService>().PlayCorrect();
            await DisplayAlert("保存成功", "你的画作已经保存好啦！", "好的");
            await RewardForDoodleAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("保存失败", ex.Message, "好的");
        }
    }

    private bool _rewarded;

    /// <summary>涂鸦没有输赢，保存一幅作品就给结算奖励（每次进入只发一次，避免反复刷星）。</summary>
    private async Task RewardForDoodleAsync()
    {
        if (_rewarded) return;
        _rewarded = true;

        var game = GameCatalog.All.First(g => g.Type == GameType.Doodle);

        await RewardWatcher.ShowForGameAsync(
            this, game, 3,
            "完成并保存了一幅画",
            "你把脑子里的画面变成了看得见的东西，这叫创作。画画没有标准答案，怎么画都对。",
            () =>
            {
                _rewarded = false;
                return Task.CompletedTask;
            });
    }

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
