using System.Collections.ObjectModel;
using KidsSafeDesktop.Controls;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

public partial class EnglishWorldPage : ContentPage
{
    private readonly EnglishWorldViewModel _vm;
    private readonly ObservableCollection<LauncherItem> _tiles = new();

    public EnglishWorldPage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "英语学习", "🔤", "english", 2, "在英语学习里学单词");
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new EnglishWorldViewModel();
        BindingContext = _vm;
        BuildTiles();
        GradesGrid.ItemsSource = _tiles;
    }

    private void BuildTiles()
    {
        _tiles.Clear();
        foreach (var g in _vm.Grades)
        {
            var grade = g.Grade;
            _tiles.Add(new LauncherItem
            {
                Name = g.Title,
                Emoji = g.Emoji,
                Accent = Color.FromArgb(g.Color),
                Launch = () => Nav(new EnglishStudyPage(grade))
            });
        }
    }

    private void Nav(Page page) => Navigation.Push(page);

    private void OnGradeTapped(object sender, LauncherItem item)
        => item.Launch?.Invoke();

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
