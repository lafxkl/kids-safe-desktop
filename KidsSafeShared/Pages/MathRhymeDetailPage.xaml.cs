using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class MathRhymeDetailPage : ContentPage
{
    private readonly RhymeTopic _topic;

    public MathRhymeDetailPage(RhymeTopic topic)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _topic = topic;
        IconLabel.Text = topic.Emoji;
        TitleLabel.Text = topic.Title;
        BuildContent();
    }

    private void BuildContent()
    {
        foreach (var item in _topic.Items)
        {
            if (!string.IsNullOrEmpty(item.Title))
            {
                ContentPanel.Children.Add(new Label
                {
                    Text = item.Title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(_topic.Color),
                    Margin = new Thickness(0, 6, 0, 0)
                });
            }

            switch (item.Kind)
            {
                case "verse":
                    if (!string.IsNullOrEmpty(item.Verse))
                        ContentPanel.Children.Add(MakeCard(item.Verse, 17, false));
                    break;

                case "list":
                    if (item.Lines is not null)
                        foreach (var line in item.Lines)
                            ContentPanel.Children.Add(MakeCard("• " + line, 16, false));
                    break;

                case "grid":
                    if (item.Grid is not null)
                        ContentPanel.Children.Add(MakeGrid(item.Grid, item.Span));
                    break;
            }
        }
    }

    private static Frame MakeCard(string text, double fontSize, bool bold)
    {
        return new Frame
        {
            CornerRadius = 16,
            Padding = new Thickness(16, 12),
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            BorderColor = Color.FromArgb("#E1BEE7"),
            Content = new Label
            {
                Text = text,
                FontSize = fontSize,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                TextColor = Color.FromArgb("#37474F"),
                HorizontalTextAlignment = TextAlignment.Start
            }
        };
    }

    private View MakeGrid(List<List<string>> grid, int span)
    {
        var g = new Grid
        {
            Margin = new Thickness(0, 4),
            HorizontalOptions = LayoutOptions.Start,
            ColumnSpacing = 4,
            RowSpacing = 4,
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            Padding = new Thickness(6)
        };
        for (var c = 0; c < span; c++)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        for (var r = 0; r < grid.Count; r++)
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        for (var r = 0; r < grid.Count; r++)
            for (var c = 0; c < grid[r].Count && c < span; c++)
            {
                var cell = new Frame
                {
                    CornerRadius = 6,
                    Padding = new Thickness(1),
                    HasShadow = false,
                    BackgroundColor = (r / 2 + c / 2) % 2 == 0
                        ? Color.FromArgb("#F3E5F5")
                        : Color.FromArgb("#E8EAF6"),
                    BorderColor = Color.FromArgb("#CE93D8"),
                    Content = new Label
                    {
                        Text = grid[r][c],
                        FontSize = 11,
                        TextColor = Color.FromArgb("#4A148C"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                g.Children.Add(cell);
            }

        // 宽于屏幕时允许横向滚动
        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = g,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never
        };
        return scroll;
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
