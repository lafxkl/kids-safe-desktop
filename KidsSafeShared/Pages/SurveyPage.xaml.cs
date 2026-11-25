using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;

namespace KidsSafeDesktop.Pages;

public partial class SurveyPage : ContentPage
{
    private readonly SurveyViewModel _vm;

    public SurveyPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new SurveyViewModel(ServiceProvider.GetOrDefault<IFeedbackService>() ?? new FeedbackService());
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }

    private void OnBack(object sender, EventArgs e) => Navigation.PopSafe();

    private void OnQuestionTap(object sender, TappedEventArgs e)
    {
        if (sender is not Border { BindingContext: PickOption option }) return;
        option.Pick();
        // 答完一题要刷新「已答 x / 6 题」和提交按钮的可用状态
        _vm.RefreshProgress();
    }

    private async void OnSubmit(object sender, EventArgs e)
    {
        await _vm.SendAsync();
    }
}
