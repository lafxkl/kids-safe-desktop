using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class EncyclopediaQuizGamePage : ContentPage
{
    private readonly EncyclopediaQuizViewModel _vm;
    private readonly List<Button> _optButtons = new();

    public EncyclopediaQuizGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new EncyclopediaQuizViewModel();
        BindingContext = _vm;
        _vm.Start();
        BuildOptionButtons();
    }

    private void BuildOptionButtons()
    {
        OptionsStack.Children.Clear();
        _optButtons.Clear();
        for (var i = 0; i < 4; i++)
        {
            var b = new Button
            {
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#004D40"),
                BackgroundColor = Color.FromArgb("#E0F2F1"),
                CornerRadius = 18,
                HeightRequest = 56,
                BorderWidth = 0
            };
            var idx = i;
            b.Clicked += (_, _) => OnOption(idx);
            OptionsStack.Children.Add(b);
            _optButtons.Add(b);
        }
        LoadOptionsUI();
    }

    private void LoadOptionsUI()
    {
        var opts = _vm.Options;
        for (var i = 0; i < _optButtons.Count; i++)
        {
            if (i < opts.Count)
            {
                _optButtons[i].IsVisible = true;
                _optButtons[i].Text = opts[i].Text;
                _optButtons[i].CommandParameter = opts[i];
                _optButtons[i].IsEnabled = true;
                _optButtons[i].BackgroundColor = Color.FromArgb("#E0F2F1");
                _optButtons[i].TextColor = Color.FromArgb("#004D40");
            }
            else
            {
                _optButtons[i].IsVisible = false;
            }
        }
    }

    private void OnOption(int idx)
    {
        if (_vm.IsCorrect) return;
        var opt = _vm.Options[idx];
        var correct = _vm.Submit(opt);
        // 着色反馈
        for (var i = 0; i < _vm.Options.Count; i++)
        {
            _optButtons[i].IsEnabled = false;
            if (_vm.Options[i].IsAnswer)
            {
                _optButtons[i].BackgroundColor = Color.FromArgb("#2E7D32");
                _optButtons[i].TextColor = Colors.White;
            }
            else if (i == idx && !correct)
            {
                _optButtons[i].BackgroundColor = Color.FromArgb("#E53935");
                _optButtons[i].TextColor = Colors.White;
            }
        }
    }

    private void OnSpeak(object sender, EventArgs e) => _vm.SpeakQuestion();

    private void OnNext(object sender, EventArgs e)
    {
        _vm.Next();
        if (!_vm.Finished) LoadOptionsUI();
    }

    private void OnReplay(object sender, EventArgs e)
    {
        _vm.Replay();
        LoadOptionsUI();
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
