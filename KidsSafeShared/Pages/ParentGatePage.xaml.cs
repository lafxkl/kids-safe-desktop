using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class ParentGatePage : ContentPage
{
    private readonly ParentGateViewModel _vm;

    public ParentGatePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new ParentGateViewModel();
        BindingContext = _vm;
        _vm.Success += OnSuccess;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Init();

        // 尚未设置密码：直接进入后台以引导家长设置
        if (!_vm.HasPin)
            _ = NavigateToDashboard();
    }

    private async void OnSubmit(object sender, EventArgs e)
    {
        if (_vm.TrySubmit())
            await NavigateToDashboard();
    }

    private async void OnBackToHome(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async Task NavigateToDashboard()
    {
        await Navigation.PushAsync(new ParentDashboardPage());
    }

    private async void OnSuccess(object? sender, EventArgs e)
    {
        await NavigateToDashboard();
    }
}
