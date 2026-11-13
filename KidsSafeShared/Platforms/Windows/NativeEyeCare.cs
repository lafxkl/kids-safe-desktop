#if WINDOWS
// 说明：WinUI 与 MAUI 存在同名类型（Grid / Visibility / HorizontalAlignment / SolidColorBrush 等），
// 因此这里不使用 using 导入 WinUI 命名空间，而是统一用别名引用，避免 CS0104 歧义。
using WinBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinColor = Windows.UI.Color;
using WinGrid = Microsoft.UI.Xaml.Controls.Grid;
using WinRect = Microsoft.UI.Xaml.Shapes.Rectangle;
using WinStretch = Microsoft.UI.Xaml.HorizontalAlignment;
using WinVStretch = Microsoft.UI.Xaml.VerticalAlignment;
using WinVisibility = Microsoft.UI.Xaml.Visibility;
using WinWindow = Microsoft.UI.Xaml.Window;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// Windows 平台护眼遮罩（蓝光过滤）。
///
/// 实现说明：MAUI 的布局容器（Grid 等）只接受 View，不接受 Page，
/// 因此无法把 NavigationPage 塞进 Grid 来做全局遮罩（运行时会抛异常）。
/// 这里改为在原生窗口层接管：用一层 Grid 包住 MAUI 原有的窗口根内容，
/// 再在其上叠一层暖色 Rectangle（IsHitTestVisible=false，不拦截任何交互）。
/// 任意步骤失败都会静默降级——最坏情况是遮罩不显示，主流程不受影响。
/// </summary>
public static class NativeEyeCare
{
    private static WinGrid? _host;
    private static WinRect? _rect;

    public static void Apply(object? windowObj, bool on, double opacity)
    {
        try
        {
            if (windowObj is not Microsoft.Maui.Controls.Window window) return;

            var native = window.Handler?.PlatformView as WinWindow;
            if (native?.Content is null) return;

            _rect ??= new WinRect
            {
                IsHitTestVisible = false,
                Fill = new WinBrush(WinColor.FromArgb(255, 255, 154, 60)),
                Opacity = 0,
                Visibility = WinVisibility.Collapsed,
                HorizontalAlignment = WinStretch.Stretch,
                VerticalAlignment = WinVStretch.Stretch
            };

            // 首次：用一层 Grid 接管窗口根内容（原内容在下、遮罩在上）
            if (_host is null || !ReferenceEquals(native.Content, _host))
            {
                var original = native.Content;
                var grid = new WinGrid();
                native.Content = grid;
                grid.Children.Add(original);
                grid.Children.Add(_rect);
                _host = grid;
            }

            _rect.Opacity = opacity;
            _rect.Visibility = on ? WinVisibility.Visible : WinVisibility.Collapsed;
        }
        catch
        {
            // 静默降级：护眼遮罩不可用不影响正常使用
        }
    }
}
#endif
