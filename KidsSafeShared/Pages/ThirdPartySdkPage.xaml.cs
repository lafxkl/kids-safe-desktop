using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 第三方 SDK 清单。国内安卓商店（华为/小米/OPPO/vivo/应用宝）审核时都会
/// 逐条核对「隐私政策里列的 SDK」与「APK 里实际引入的 SDK」是否一致，
/// 所以这份清单的条目必须与 csproj 的 PackageReference、Platforms/Android/libs/*.aar 严格对应。
/// </summary>
public partial class ThirdPartySdkPage : ContentPage
{
    public ThirdPartySdkPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BindableLayout.SetItemTemplate(SdkHost, (DataTemplate)Resources["SdkCard"]);
        BindableLayout.SetItemsSource(SdkHost, ComplianceContent.SdkItems);
        BuildNotIncluded();
    }

    /// <summary>「未接入」列表用简单行渲染，避免为几行文字再写一个模板。</summary>
    private void BuildNotIncluded()
    {
        NotIncludedHost.Clear();
        var cross = Color.FromArgb("#C62828");
        var text = Color.FromArgb("#455A64");
        foreach (var line in ComplianceContent.NotIncluded)
        {
            var row = new HorizontalStackLayout { Spacing = 6 };
            row.Add(new Label
            {
                Text = "✕",
                FontSize = 12,
                TextColor = cross,
                VerticalOptions = LayoutOptions.Start
            });
            row.Add(new Label
            {
                Text = line,
                FontSize = 12.5,
                TextColor = text,
                LineBreakMode = LineBreakMode.WordWrap
            });
            NotIncludedHost.Add(row);
        }
    }

    private async void OnBack(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
        else
            await Navigation.PushAsync(new PrivacyPolicyPage());
    }

    private async void OnOpenPolicy(object? sender, EventArgs e)
    {
        // 从隐私政策页跳过来时直接退回，避免栈里堆两层同样的页面
        if (Navigation.NavigationStack.Count > 1
            && Navigation.NavigationStack[^2] is PrivacyPolicyPage)
        {
            await Navigation.PopAsync();
            return;
        }
        await Navigation.PushAsync(new PrivacyPolicyPage());
    }

    private async void OnOpenSdkPolicy(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not SdkItem item) return;
        if (string.IsNullOrWhiteSpace(item.PolicyUrl) || item.PolicyUrl == "—")
        {
            await DisplayAlert("说明", "这一项不是第三方 SDK，由你自己部署和管理，没有官方隐私政策页面。", "知道了");
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(item.PolicyUrl);
        }
        catch
        {
            await DisplayAlert("无法打开浏览器",
                "请手动访问：\n\n" + item.PolicyUrl, "好的");
        }
    }
}
