using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;

namespace KidsSafeDesktop.Pages;

public partial class FeedbackPage : ContentPage
{
    private readonly FeedbackViewModel _vm;

    public FeedbackPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new FeedbackViewModel(ServiceProvider.GetOrDefault<IFeedbackService>() ?? new FeedbackService());
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // 进页面顺手补传上次没发出去的那几条；失败不打扰用户
        await _vm.OnAppearingAsync();
    }

    private void OnBack(object sender, EventArgs e) => Navigation.PopSafe();

    /// <summary>所有小方块共用这一个处理器：真正的选中逻辑在 ViewModel 里（互斥 / 反选各不相同）。</summary>
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
