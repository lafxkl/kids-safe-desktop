using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 隐私政策全文。正文来自 <see cref="ComplianceContent.PolicySections"/>，
/// 与 Docs/隐私政策.md 内容一致（商店提交用那个文件，应用内展示用这份）。
/// </summary>
public partial class PrivacyPolicyPage : ContentPage
{
    public PrivacyPolicyPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        // 先给模板再喂数据：顺序反了的话子项会按「数据对象本身」生成，模板不再生效
        BindableLayout.SetItemTemplate(SectionHost, (DataTemplate)Resources["SectionCard"]);
        BindableLayout.SetItemsSource(SectionHost, ComplianceContent.PolicySections);
    }

    private async void OnBack(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnOpenSdk(object? sender, EventArgs e)
        => await Navigation.PushAsync(new ThirdPartySdkPage());
}
