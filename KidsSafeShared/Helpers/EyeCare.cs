using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 护眼模式（Eye Care / 蓝光过滤）的判定规则。与 UI 解耦，便于复用与测试。
/// </summary>
public static class EyeCareRules
{
    /// <summary>遮罩不透明度上限：再高会让画面过暗，影响正常阅读。</summary>
    public const double MaxOpacity = 0.55;

    /// <summary>
    /// 综合「常开 + 定时自动」判断当前是否生效。定时支持跨零点（如 19:00 ~ 07:00）。
    /// </summary>
    public static bool IsEffectivelyOn(ChildSettings s)
    {
        if (s is null) return false;
        if (s.EyeCareEnabled) return true;
        if (!s.EyeCareAutoEnabled) return false;

        var hour = DateTime.Now.Hour;
        var start = s.EyeCareAutoStartHour;
        var end = s.EyeCareAutoEndHour;

        if (start == end) return true;              // 首尾相同视为全天
        return start < end
            ? hour >= start && hour < end           // 不跨零点
            : hour >= start || hour < end;          // 跨零点
    }

    /// <summary>把 0~1 的强度映射为遮罩不透明度。</summary>
    public static double ToOpacity(double intensity)
        => Math.Clamp(intensity, 0.0, 1.0) * MaxOpacity;

    /// <summary>自动时段展示文本，如「19:00 - 07:00」。</summary>
    public static string DescribeRange(ChildSettings s)
        => $"{s.EyeCareAutoStartHour:00}:00 - {s.EyeCareAutoEndHour:00}:00";
}

/// <summary>
/// 护眼遮罩管理：由 App 在窗口就绪后 Attach，设置变化时调用 Refresh。
/// 真正的绘制交给各平台的 NativeEyeCare（系统窗口层叠加暖色滤镜）。
/// </summary>
public static class EyeCare
{
    private static Microsoft.Maui.Controls.Window? _window;
    private static bool _handlerHooked;

    /// <summary>绑定窗口；窗口 Handler 可能尚未创建，这里会自动等待。</summary>
    public static void Attach(Microsoft.Maui.Controls.Window? window)
    {
        if (window is null) return;
        _window = window;

        if (window.Handler is not null)
        {
            Refresh();
            return;
        }

        if (_handlerHooked) return;
        _handlerHooked = true;
        window.HandlerChanged += OnWindowHandlerChanged;
    }

    private static void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
        if (_window?.Handler is null) return;
        _window.HandlerChanged -= OnWindowHandlerChanged;
        _handlerHooked = false;
        Refresh();
    }

    /// <summary>根据当前设置刷新遮罩。</summary>
    public static void Refresh()
    {
        var window = _window;
        if (window is null) return;

        ChildSettings s;
        try { s = ServiceProvider.Get<ISettingsService>().Current; }
        catch { return; }

        var on = EyeCareRules.IsEffectivelyOn(s);
        NativeEyeCare.Apply(window, on, EyeCareRules.ToOpacity(s.EyeCareIntensity));
    }
}
