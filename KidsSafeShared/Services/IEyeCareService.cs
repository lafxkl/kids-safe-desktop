using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 护眼模式：以全局暖色遮罩降低屏幕蓝光，支持常开与定时自动生效。
/// 具体渲染由 <see cref="Helpers.EyeCare"/> 持有的遮罩完成，本服务只负责状态与持久化。
/// </summary>
public interface IEyeCareService
{
    /// <summary>是否「常开」。</summary>
    bool IsEnabled { get; }

    /// <summary>强度 0.0~1.0。</summary>
    double Intensity { get; }

    /// <summary>是否启用定时自动护眼。</summary>
    bool AutoEnabled { get; }

    /// <summary>自动时段开始小时（0~23）。</summary>
    int AutoStartHour { get; }

    /// <summary>自动时段结束小时（0~23）。</summary>
    int AutoEndHour { get; }

    /// <summary>综合「常开 + 定时」后，当前实际是否生效。</summary>
    bool IsEffectivelyOn { get; }

    /// <summary>状态发生变化（外部需刷新遮罩）。</summary>
    event EventHandler? StateChanged;

    Task LoadAsync();

    Task SetEnabledAsync(bool enabled);

    Task SetIntensityAsync(double intensity);

    Task SetAutoAsync(bool auto);

    Task SetAutoRangeAsync(int startHour, int endHour);
}
