using KidsSafeDesktop.Pages;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeParent;

/// <summary>
/// 家长端外壳。与孩子端共用 KidsSafeShared 里的全部页面与服务，
/// 区别只在入口：孩子端进 SplashPage → 桌面，家长端直接进家长中心。
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // 直接进家长中心：家长端没有「儿童桌面」这层概念，
        // 但仍走 ParentGate（PIN 校验），避免任何人拿起家长手机就能改配置。
        MainPage = new NavigationPage(new ParentGatePage());
    }

    /// <summary>护眼遮罩等窗口级效果与孩子端保持一致（家长端也读同一份设置）。</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        KidsSafeDesktop.Helpers.EyeCare.Attach(window);
        return window;
    }

    protected override void OnResume()
    {
        base.OnResume();
        KidsSafeDesktop.Helpers.EyeCare.Refresh();
    }
}
