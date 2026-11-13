using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class SafetyKnowledgeDetailPage : ContentPage
{
    private readonly SafetyTopic _topic;

    public SafetyKnowledgeDetailPage(SafetyTopic topic)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _topic = topic;
        IconLabel.Text = topic.Emoji;
        TitleLabel.Text = topic.Title;

        foreach (var tip in topic.Tips)
        {
            var card = new Frame
            {
                CornerRadius = 18,
                Padding = new Thickness(16, 14),
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                BorderColor = Color.FromArgb(topic.Color),
                Content = new HorizontalStackLayout
                {
                    Spacing = 14,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = tip.Emoji, FontSize = 36, VerticalOptions = LayoutOptions.Center },
                        new Label
                        {
                            Text = tip.Text,
                            FontSize = 18,
                            TextColor = Color.FromArgb("#37474F"),
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalTextAlignment = TextAlignment.Start,
                            HorizontalOptions = LayoutOptions.Fill
                        }
                    }
                }
            };
            TipsPanel.Children.Add(card);
        }
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
