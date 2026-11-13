using System.Collections.ObjectModel;
using KidsSafeDesktop.Controls;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

public partial class LearnWorldPage : ContentPage
{
    private readonly LearnWorldViewModel _vm;
    private readonly ObservableCollection<LauncherItem> _tiles = new();

    /// <summary>false = 口算练习（默认），true = 模拟考试。</summary>
    private bool _examMode;

    public LearnWorldPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new LearnWorldViewModel();
        BindingContext = _vm;
        BuildTiles();
        GradesGrid.ItemsSource = _tiles;
        UpdateModeVisuals();
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
                Launch = _examMode
                    ? () => Nav(new ExamPaperListPage((int)grade + 1))
                    : () => Nav(new OralPracticePage(grade, OralTopic.All))
            });
        }
    }

    private void OnModeOral(object sender, EventArgs e) => SetMode(false);

    private void OnModeExam(object sender, EventArgs e) => SetMode(true);

    private void SetMode(bool exam)
    {
        if (_examMode == exam) return;
        _examMode = exam;
        BuildTiles();
        UpdateModeVisuals();
    }

    /// <summary>高亮当前模式：选中是实色底白字，未选中是浅底深字。</summary>
    private void UpdateModeVisuals()
    {
        BtnOral.BackgroundColor = _examMode ? Color.FromArgb("#E3F2FD") : Color.FromArgb("#0288D1");
        BtnOral.TextColor = _examMode ? Color.FromArgb("#0277BD") : Colors.White;
        BtnExam.BackgroundColor = _examMode ? Color.FromArgb("#EF5350") : Color.FromArgb("#FFEBEE");
        BtnExam.TextColor = _examMode ? Colors.White : Color.FromArgb("#C62828");

        HintLabel.Text = _examMode
            ? "选一个年级，进入语文 / 数学 / 英语模拟卷，交卷后自动打分讲错题～"
            : "选一个年级（一年级到初三），做完会帮你批改哦～";
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
