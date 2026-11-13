using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class LeaderboardPage : ContentPage
{
    private readonly LeaderboardViewModel _vm;

    public LeaderboardPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new LeaderboardViewModel();
        BindingContext = _vm;
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();
}
