using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;

namespace KidsSafeDesktop.Pages;

public partial class AccountPage : ContentPage
{
    private readonly AccountPageViewModel _vm;

    public AccountPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new AccountPageViewModel();
        BindingContext = _vm;
        // 账号服务变化（如服务器同步改了昵称）时刷新界面
        ServiceProvider.Get<IAccountService>().Changed += (_, _) => _vm.Refresh();
    }

    private void OnAvatarPick(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is string emoji)
        {
            _vm.Avatar = emoji;
            ServiceProvider.Get<ISoundService>().PlayClick();
        }
    }

    private async void OnSaveProfile(object sender, EventArgs e)
    {
        await _vm.SaveProfileAsync();
        ServiceProvider.Get<ISoundService>().PlayCorrect();
    }

    private async void OnConnect(object sender, EventArgs e)
    {
        await _vm.ConnectAsync();
        if (!_vm.IsOffline)
            ServiceProvider.Get<ISoundService>().PlayCorrect();
    }

    private async void OnDisconnect(object sender, EventArgs e)
        => await _vm.DisconnectAsync();

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
