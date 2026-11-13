using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class BodyEncyclopediaPage : ContentPage
{
    private readonly BodyEncyclopediaViewModel _vm;
    private readonly List<Button> _catButtons = new();

    private static readonly Color ChipBg = Color.FromArgb("#FFE0B2");
    private static readonly Color ChipBgSel = Color.FromArgb("#EF6C00");
    private static readonly Color ChipText = Color.FromArgb("#E65100");
    private static readonly Color ChipTextSel = Colors.White;

    public BodyEncyclopediaPage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "身体百科", "🧍", "body", 2, "在身体百科里认识身体");
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new BodyEncyclopediaViewModel();
        BindingContext = _vm;
        BuildChips();
    }

    private void BuildChips()
    {
        CategoryStack.Children.Clear();
        _catButtons.Clear();
        foreach (var name in _vm.Categories)
        {
            var b = new Button
            {
                Text = name,
                TextColor = ChipText,
                BackgroundColor = ChipBg,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                CornerRadius = 20,
                Padding = new Thickness(16, 8),
                BorderWidth = 0,
                CommandParameter = name
            };
            b.Clicked += OnCategoryTapped;
            CategoryStack.Children.Add(b);
            _catButtons.Add(b);
        }
        UpdateChipUI();
    }

    private void UpdateChipUI()
    {
        foreach (var b in _catButtons)
        {
            var sel = b.CommandParameter is string s && s == _vm.SelectedCategory;
            b.BackgroundColor = sel ? ChipBgSel : ChipBg;
            b.TextColor = sel ? ChipTextSel : ChipText;
        }
    }

    private void OnCategoryTapped(object sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string name)
        {
            _vm.SelectedCategory = name;
            UpdateChipUI();
        }
    }

    private void OnPartTapped(object sender, EventArgs e)
    {
        if ((sender as View)?.BindingContext is BodyPart part)
            _vm.SelectPart(part);
    }

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
