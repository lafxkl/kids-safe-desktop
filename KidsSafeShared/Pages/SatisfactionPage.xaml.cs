using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;

namespace KidsSafeDesktop.Pages;

public partial class SatisfactionPage : ContentPage
{
    private readonly SatisfactionViewModel _vm;

    public SatisfactionPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new SatisfactionViewModel(ServiceProvider.GetOrDefault<IFeedbackService>() ?? new FeedbackService());
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }

    private void OnBack(object sender, EventArgs e) => Navigation.PopSafe();

    private void OnOptionTap(object sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: PickOption option })
            option.Pick();
    }

    private async void OnSubmit(object sender, EventArgs e)
    {
        await _vm.SendAsync();
    }
}
