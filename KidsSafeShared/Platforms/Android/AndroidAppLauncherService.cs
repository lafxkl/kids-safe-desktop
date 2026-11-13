#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.ApplicationModel;

namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// 通过包名启动 Android 应用。
/// </summary>
public class AndroidAppLauncherService : IAppLauncherService
{
    public Task LaunchAsync(AppItem app)
    {
        if (string.IsNullOrEmpty(app.PackageName))
            return Task.CompletedTask;

        var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
        var intent = ctx.PackageManager?.GetLaunchIntentForPackage(app.PackageName);
        if (intent is null)
            return Task.CompletedTask;

        // 若开启了「锁定模式」（屏幕固定），先解除固定，否则系统会阻止跳转到其他应用。
        // 家长既然放行了这个应用，就应当允许离开锁定——直到下次启动再恢复。
        try
        {
            if (Platform.CurrentActivity is Activity activity)
            {
                var am = activity.GetSystemService(Context.ActivityService) as global::Android.App.ActivityManager;
                if (am is not null && am.LockTaskModeState != global::Android.App.LockTaskMode.None)
                    activity.StopLockTask();
            }
        }
        catch
        {
            // 未固定时忽略
        }

        // 家长放行了这个应用 → 短暂关闭「自动回前台」，否则刚跳过去就被拉回来。
        // 只在这次 OnStop 的窗口内生效；用户回到本应用时（OnResume）会自动重新武装。
        AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromSeconds(6));

        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
        return Task.CompletedTask;
    }
}
#endif
