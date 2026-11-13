namespace KidsSafeServer;

/// <summary>
/// 配置变更信号：家长端每保存一次配置，该账号的版本号 +1，并唤醒该账号下正在长轮询的儿童设备。
///
/// 为什么不用 WebSocket / MQTT：
///  1. 这个服务是「家长在家里电脑上双击运行」的形态，越简单越好，不能要求装消息中间件；
///  2. 长轮询（long polling）本质是「挂起一个 HTTP 请求，有事就立刻返回」，
///     穿透 NAT、过家用路由、断网重连都比长连接省心，标准库即可实现；
///  3. 儿童桌面本身就在前台常驻，挂一个 25 秒的请求对耗电几乎没有影响。
///
/// 加了账号体系之后，版本号**按账号分桶**：A 家保存配置不能把 B 家的设备唤醒，
/// 否则同一个服务器上多个家庭会互相打扰（也白白耗电）。
///
/// 实现要点：每个账号一个 TaskCompletionSource 做「等待被唤醒」，
/// Pulse 时把旧 waiter 完成掉并换一个新的，保证不会漏掉并发保存。
///
/// 版本号为什么不用 0 起算：服务端一重启就会从 0 重新计数，
/// 而客户端记得的是重启前的大数字（例如 7）。此时服务端返回 0，
/// 看起来像「版本倒退」，客户端会把它当成一次变更，多拉一次配置。
/// 因此以「启动时刻的毫秒时间戳」为基准，重启后版本号只增不减，
/// 客户端的 since 语义在任何时刻都成立。
/// </summary>
public sealed class WatchSignal
{
    private sealed class Bucket
    {
        public long Pulses;
        public TaskCompletionSource<long> Waiter = NewWaiter();
    }

    private readonly object _lock = new();
    private readonly long _base = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

    private long _totalPulses;

    /// <summary>最近一次变更时间（UTC），供诊断接口显示。</summary>
    public DateTimeOffset? LastPulseUtc { get; private set; }

    private static TaskCompletionSource<long> NewWaiter()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Bucket BucketFor(string key)
    {
        if (!_buckets.TryGetValue(key, out var b))
        {
            b = new Bucket();
            _buckets[key] = b;
        }
        return b;
    }

    /// <summary>某个账号的当前版本号（启动基准 + 该账号保存次数），单调递增。</summary>
    public long VersionFor(string key)
    {
        lock (_lock) return _base + BucketFor(key).Pulses;
    }

    /// <summary>所有账号累计的变更次数（健康检查展示用）。</summary>
    public long PulseCount
    {
        get { lock (_lock) return _totalPulses; }
    }

    /// <summary>通知某个账号下所有等待者「有变化了」。</summary>
    public void Pulse(string key)
    {
        TaskCompletionSource<long> waiter;
        long v;
        lock (_lock)
        {
            var b = BucketFor(key);
            b.Pulses++;
            _totalPulses++;
            v = _base + b.Pulses;
            LastPulseUtc = DateTimeOffset.UtcNow;
            waiter = b.Waiter;
            b.Waiter = NewWaiter();
        }
        waiter.TrySetResult(v);
    }

    /// <summary>
    /// 等到该账号有变化，或超时返回。返回当前的版本号；客户端断开（<paramref name="ct"/> 取消）时返回
    /// <c>null</c>，由调用方结束请求。
    ///
    /// 这里刻意不抛 <see cref="OperationCanceledException"/>：长轮询超时或浏览器关页面是**正常**
    /// 流程，一分钟内会发生好几次。若用异常表达，调试器的「异常设置」一勾「引发时中断」就会
    /// 每次都停下来，且停在异常展开的栈帧上，局部变量窗口只会显示「在异常调用堆栈中不可用」。
    /// 用返回值表达取消，既不产生 first-chance 异常，也不污染日志。
    /// </summary>
    public async Task<long?> WaitAsync(string key, TimeSpan timeout, CancellationToken ct)
    {
        TaskCompletionSource<long> waiter;
        lock (_lock)
        {
            waiter = BucketFor(key).Waiter;
        }

        // 取消时让延时任务直接转为 Canceled，Task.WhenAny 正常返回，不从这里抛异常
        var delay = Task.Delay(timeout, ct);
        var done = await Task.WhenAny(waiter.Task, delay).ConfigureAwait(false);

        if (done == waiter.Task)
            return await waiter.Task.ConfigureAwait(false);

        if (ct.IsCancellationRequested) return null;

        // 超时后重新读一次：万一刚好在「延时到点」和「加锁」之间有人保存过
        lock (_lock) return _base + BucketFor(key).Pulses;
    }
}
