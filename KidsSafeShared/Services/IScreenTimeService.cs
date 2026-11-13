namespace KidsSafeDesktop.Services;

/// <summary>
/// 屏幕时间管控接口。
/// </summary>
public interface IScreenTimeService
{
    /// <summary>剩余可用时长</summary>
    TimeSpan Remaining { get; }

    /// <summary>是否已超时锁定</summary>
    bool IsLockedOut { get; }

    /// <summary>达到每日上限时触发</summary>
    event EventHandler? LimitReached;

    /// <summary>开始计时</summary>
    void Start();

    /// <summary>停止计时并持久化已用时长</summary>
    void Stop();

    /// <summary>清空当天已用时长（家长延长时使用）</summary>
    void ResetToday();
}
