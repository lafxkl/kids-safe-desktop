using Microsoft.UI.Xaml;

namespace KidsSafeParent.WinUI;

/// <summary>
/// 家长端 Windows 入口。注意与孩子端的区别：这里**不做**任务栏隐藏 / Kiosk 恢复，
/// 家长端是普通桌面应用，不应干扰系统外壳（那套逻辑只在孩子端 App 里调用）。
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
