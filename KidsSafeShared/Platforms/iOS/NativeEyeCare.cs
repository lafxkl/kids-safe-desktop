using Microsoft.Maui.ApplicationModel;

namespace KidsSafeDesktop.Helpers;

#if IOS
using UIKit;

/// <summary>
/// iOS 护眼遮罩：在 key window 上盖一层暖色半透明 UIView。
///
/// 为什么和安卓/Windows 的做法不一样：
///   · 安卓可以给整个窗口加 ColorMatrix 滤镜，Windows 用 DWM 叠加层；
///   · iOS 没有应用级别的「全局滤镜」，唯一能覆盖全屏又不碰内容的办法
///     就是窗口顶部叠一层不接收触摸的视图。
///
/// 两个必须注意的点：
///   1) <c>UserInteractionEnabled = false</c> —— 否则整屏点不动，孩子会以为死机；
///   2) 用 AutoresizingMask 跟随旋转，不写死 frame，横竖屏切换后不会只盖住半屏。
/// </summary>
public static class NativeEyeCare
{
    private static UIView? _overlay;

    public static void Apply(object? windowObj, bool on, double opacity)
    {
        // 一定要在主线程：护眼设置可能从家长后台同步回来（长轮询线程）触发
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (!on)
                {
                    Remove();
                    return;
                }

                var window = CurrentWindow();
                if (window is null) return;

                var color = UIColor.FromRGBA(1.0f, 0.74f, 0.36f, (nfloat)Math.Clamp(opacity, 0, 1));

                if (_overlay is null)
                {
                    _overlay = new UIView
                    {
                        UserInteractionEnabled = false,
                        // 暖黄比纯橙更接近「护眼模式」的观感，叠加后肤色调不会明显偏红
                        BackgroundColor = color,
                        AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
                    };
                }
                else
                {
                    _overlay.BackgroundColor = color;
                }

                _overlay.Frame = window.Bounds;

                // 启动页 → 主界面的切换会重挂根控制器视图；遮罩被摘掉时重新挂上
                if (_overlay.Superview is null)
                {
                    window.AddSubview(_overlay);
                }

                window.BringSubviewToFront(_overlay);
            }
            catch
            {
                // 遮罩失败不影响使用（例如窗口尚未创建）
            }
        });
    }

    private static void Remove()
    {
        try
        {
            _overlay?.RemoveFromSuperview();
            _overlay?.Dispose();
        }
        catch { }
        finally
        {
            _overlay = null;
        }
    }

    private static UIWindow? CurrentWindow()
    {
        var app = UIApplication.SharedApplication;

        if (app.KeyWindow is not null) return app.KeyWindow;

        foreach (var w in app.Windows)
        {
            if (w.IsKeyWindow) return w;
        }

        return app.Windows.FirstOrDefault();
    }
}
#elif !ANDROID && !WINDOWS
/// <summary>
/// 其他平台（MacCatalyst）的护眼遮罩占位实现：静默降级。
/// </summary>
public static class NativeEyeCare
{
    public static void Apply(object? windowObj, bool on, double opacity)
    {
        // 暂无平台实现
    }
}
#endif
