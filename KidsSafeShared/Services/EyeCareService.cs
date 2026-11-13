using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 护眼模式服务：读写 ChildSettings 中的护眼字段，并在状态变化时通知 UI 刷新遮罩。
/// </summary>
public class EyeCareService : IEyeCareService
{
    private readonly ISettingsService _settings;

    public EyeCareService()
    {
        _settings = ServiceProvider.Get<ISettingsService>();
    }

    private ChildSettings S => _settings.Current;

    public bool IsEnabled => S.EyeCareEnabled;
    public double Intensity => S.EyeCareIntensity;
    public bool AutoEnabled => S.EyeCareAutoEnabled;
    public int AutoStartHour => S.EyeCareAutoStartHour;
    public int AutoEndHour => S.EyeCareAutoEndHour;

    public bool IsEffectivelyOn => EyeCareRules.IsEffectivelyOn(S);

    public event EventHandler? StateChanged;

    public async Task LoadAsync() => await _settings.LoadAsync();

    public async Task SetEnabledAsync(bool enabled)
    {
        S.EyeCareEnabled = enabled;
        await PersistAsync();
    }

    public async Task SetIntensityAsync(double intensity)
    {
        S.EyeCareIntensity = Math.Clamp(intensity, 0.0, 1.0);
        await PersistAsync();
    }

    public async Task SetAutoAsync(bool auto)
    {
        S.EyeCareAutoEnabled = auto;
        await PersistAsync();
    }

    public async Task SetAutoRangeAsync(int startHour, int endHour)
    {
        S.EyeCareAutoStartHour = Math.Clamp(startHour, 0, 23);
        S.EyeCareAutoEndHour = Math.Clamp(endHour, 0, 23);
        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        await _settings.SaveAsync();
        StateChanged?.Invoke(this, EventArgs.Empty);
        EyeCare.Refresh();
    }
}
