using System.Timers;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 屏幕时间管控：按天累计使用时长，超限触发锁定。
/// </summary>
public class ScreenTimeService : IScreenTimeService
{
    private System.Timers.Timer? _timer;
    private DateTime _sessionStart;
    private double _sessionAccumSeconds; // 本次会话中已累加的秒数（用于实时计算）
    private readonly object _sync = new();

    public TimeSpan Remaining { get; private set; } = TimeSpan.FromHours(1);
    public bool IsLockedOut { get; private set; }
    public event EventHandler? LimitReached;

    public void Start()
    {
        lock (_sync)
        {
            if (_timer is not null) return;

            IsLockedOut = false;
            _sessionStart = DateTime.Now;
            _sessionAccumSeconds = 0;

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTick;
            _timer.Start();
            Recompute();
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            PersistUsed();
        }
    }

    public void ResetToday()
    {
        lock (_sync)
        {
            var key = UsedKey();
            Preferences.Default.Remove(key);
            IsLockedOut = false;
            _sessionStart = DateTime.Now;
            _sessionAccumSeconds = 0;
            Recompute();
        }
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        lock (_sync)
        {
            _sessionAccumSeconds = (DateTime.Now - _sessionStart).TotalSeconds;
        }
        Recompute();
    }

    private void Recompute()
    {
        double usedSeconds;
        double limitSeconds;
        lock (_sync)
        {
            limitSeconds = TimeSpan.FromMinutes(ServiceProvider.Get<ISettingsService>().Current.DailyLimitMinutes).TotalSeconds;
            usedSeconds = LoadUsedSeconds() + _sessionAccumSeconds;
        }

        var remaining = TimeSpan.FromSeconds(Math.Max(0, limitSeconds - usedSeconds));
        Remaining = remaining;

        if (!IsLockedOut && limitSeconds > 0 && usedSeconds >= limitSeconds)
        {
            IsLockedOut = true;
            PersistUsed();
            LimitReached?.Invoke(this, EventArgs.Empty);
        }
    }

    private double LoadUsedSeconds()
    {
        var key = UsedKey();
        return Preferences.Default.Get(key, 0.0);
    }

    private void PersistUsed()
    {
        var key = UsedKey();
        var total = LoadUsedSeconds() + _sessionAccumSeconds;
        Preferences.Default.Set(key, total);
        _sessionStart = DateTime.Now;
        _sessionAccumSeconds = 0;
    }

    private static string UsedKey() => "kids_used_" + DateTime.Now.ToString("yyyyMMdd");
}
