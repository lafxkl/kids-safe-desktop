using Android.App;
using Android.Content.PM;
using Android.OS;

namespace KidsSafeParent;

/// <summary>
/// 家长端安卓入口：普通单 Activity，不接管系统按键、不进 Kiosk、
/// 不注册推送（那套是孩子端设备才需要的）。
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
                         | ConfigChanges.UiMode | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
