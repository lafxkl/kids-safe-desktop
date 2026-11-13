using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 首屏隐私同意页（合规闸门）。
///
/// 这一页是 Launcher 之后的第一个界面，在它之前只允许做两件不涉及个人信息的事：
/// 1. 读本地主题/语言偏好；
/// 2. 挂护眼遮罩（纯本地绘制，不采集）。
///
/// 因此这里刻意不调用：SettingsService 的服务器同步、LiveSyncService 的长轮询、
/// 荣耀推送的 getPushToken、任何 Analytics。这些全部发生在 SplashPage 里，
/// 只有用户点了「同意并继续」才会走到那里。
/// </summary>
public partial class PrivacyConsentPage : ContentPage
{
    public PrivacyConsentPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        DisagreeBtn.TextColor = Color.FromArgb("#78909C");
    }

    protected override bool OnBackButtonPressed() => true;   // 首屏禁止用返回键跳过

    private async void OnOpenPolicy(object? sender, TappedEventArgs e)
        => await Navigation.PushAsync(new PrivacyPolicyPage());

    private async void OnOpenSdk(object? sender, TappedEventArgs e)
        => await Navigation.PushAsync(new ThirdPartySdkPage());

    private async void OnAgree(object? sender, EventArgs e)
    {
        AgreeBtn.IsEnabled = false;          // 防连点导致重复切根页面

        PrivacyConsent.Agree();
        KidsSafeDesktop.Helpers.DiagLog.Append(KidsSafeDesktop.Helpers.NavFx.LogFileName,
            $"[隐私] 已同意隐私政策 v{PrivacyConsent.CurrentVersion}");

        // 同意后才进入启动页，由它完成设置读取、推送 token、服务器连接、Kiosk 等初始化
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = new SplashPage();
        });
    }

    private async void OnDisagree(object? sender, EventArgs e)
    {
        var quit = await DisplayAlert("不同意隐私政策",
            "不同意的话，我们无法开始运行——因为不读取本地设置就没办法控制时长和过滤应用。\n\n点击「退出」将关闭应用，我们不会收集或保存任何信息。",
            "退出", "再看看条款");
        if (!quit) return;

        // 不写任何偏好、不留任何痕迹，直接退出
        try
        {
            await ServiceProvider.Get<IKioskService>().ExitAppAsync();
        }
        catch
        {
            try { Application.Current?.Quit(); } catch { }
        }
    }
}
