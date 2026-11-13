using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class LockoutPage : ContentPage
{
    public LockoutPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    protected override bool OnBackButtonPressed() => true;

    private async void OnParentEnter(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ParentGatePage());
    }
}
