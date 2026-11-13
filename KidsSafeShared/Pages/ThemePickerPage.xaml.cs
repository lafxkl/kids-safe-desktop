using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

public partial class ThemePickerPage : ContentPage
{
    public ThemePickerPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ThemeService.Instance.Init();
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var id = ThemeService.Instance.CurrentTheme.Id;
        CardSunshine.Stroke = id == "sunshine" ? Color.FromArgb("#FB8C00") : Colors.Transparent;
        CardSky.Stroke = id == "sky" ? Color.FromArgb("#0288D1") : Colors.Transparent;
        CardRainbow.Stroke = id == "rainbow" ? Color.FromArgb("#8E24AA") : Colors.Transparent;
        CheckSunshine.IsVisible = id == "sunshine";
        CheckSky.IsVisible = id == "sky";
        CheckRainbow.IsVisible = id == "rainbow";
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        var id = e.Parameter?.ToString();
        if (string.IsNullOrEmpty(id)) return;

        ThemeService.Instance.SetTheme(id);
        ServiceProvider.Get<ISoundService>().PlayCorrect();
        RefreshSelection();

        // 让小朋友看到新皮肤被选中，再返回主页
        await Task.Delay(450);
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
