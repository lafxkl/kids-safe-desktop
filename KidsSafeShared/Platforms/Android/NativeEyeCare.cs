#if ANDROID
// 说明：Android 与 MAUI 存在同名类型（View 等），这里统一用别名引用，避免 CS0104 歧义。
using AndroidColor = Android.Graphics.Color;
using AndroidView = Android.Views.View;
using AndroidViewGroup = Android.Views.ViewGroup;
using AndroidViewStates = Android.Views.ViewStates;
using PlatformCompat = Microsoft.Maui.ApplicationModel.Platform;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// Android 平台护眼遮罩（蓝光过滤）。
/// 在 DecorView 之上叠一层暖色 View，Clickable/Focusable 均为 false，
/// 触摸事件会穿透到下层的游戏页面；失败时静默降级。
/// </summary>
public static class NativeEyeCare
{
    private static AndroidView? _view;

    public static void Apply(object? windowObj, bool on, double opacity)
    {
        try
        {
            var activity = PlatformCompat.CurrentActivity;
            if (activity?.Window?.DecorView is not AndroidViewGroup root) return;

            if (_view is null)
            {
                _view = new AndroidView(activity)
                {
                    Clickable = false,
                    Focusable = false
                };
                _view.SetBackgroundColor(AndroidColor.Argb(255, 255, 154, 60));
            }

            if (_view.Parent is null)
            {
                root.AddView(_view, new AndroidViewGroup.LayoutParams(
                    AndroidViewGroup.LayoutParams.MatchParent,
                    AndroidViewGroup.LayoutParams.MatchParent));
            }

            _view.BringToFront();
            _view.Alpha = (float)opacity;
            _view.Visibility = on ? AndroidViewStates.Visible : AndroidViewStates.Gone;
        }
        catch
        {
            // 静默降级
        }
    }
}
#endif
