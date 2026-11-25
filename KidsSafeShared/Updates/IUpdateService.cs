using System.ComponentModel;

namespace KidsSafeDesktop.Updates;

/// <summary>增量更新的状态。界面只该看这个枚举，不要去猜字符串。</summary>
public enum UpdateState
{
    /// <summary>还没检查过（或服务端没配）。</summary>
    Idle,
    /// <summary>正在向服务端要清单。</summary>
    Checking,
    /// <summary>已是最新。</summary>
    UpToDate,
    /// <summary>有新版本，还没下载。</summary>
    Available,
    /// <summary>正在下载补丁文件。</summary>
    Downloading,
    /// <summary>已下载并校验通过，等重启落盘。</summary>
    Ready,
    /// <summary>正在交给落盘器 / 正在重启。</summary>
    Applying,
    /// <summary>这一次没成功（下次会自动重试，不影响使用）。</summary>
    Failed
}

/// <summary>
/// 增量更新。Windows 与 Android 共用同一份清单、同一套校验，
/// 只有「怎么落盘」这一步分叉：
///   · Windows：交给 KidsSafeUpdater.exe，在进程退出后替换 dll 再重启；
///   · Android：写到 files/patches，下次冷启动由 PatchBootstrapper 预加载覆盖。
/// </summary>
public interface IUpdateService : INotifyPropertyChanged
{
    UpdateState State { get; }

    /// <summary>给家长看的一句话状态。</summary>
    string Status { get; }

    /// <summary>可用的新版本号；没有则为 null。</summary>
    string? AvailableVersion { get; }

    /// <summary>这一版改了什么。</summary>
    string? Notes { get; }

    /// <summary>下载进度 0..1。</summary>
    double Progress { get; }

    /// <summary>上次落盘的结果（成功 / 已回滚 / 原因），启动时读一次。</summary>
    string? LastResult { get; }

    /// <summary>是否已有下载好、等着生效的补丁。</summary>
    bool HasPendingPatch { get; }

    /// <summary>检查更新。返回 true 表示有新版本可用。</summary>
    Task<bool> CheckAsync(CancellationToken ct = default);

    /// <summary>下载差异文件并校验。返回 true 表示已就绪。</summary>
    Task<bool> DownloadAsync(CancellationToken ct = default);

    /// <summary>
    /// 让补丁生效。
    /// <paramref name="restart"/> 为 true 时，Windows 会立即重启应用（家长点「马上更新」）；
    /// 为 false 时只交给落盘器排队，等下次退出再装。
    /// </summary>
    Task<bool> ApplyAsync(bool restart);

    /// <summary>启动时调用：读上次落盘结果、发现未装完的补丁、清理过期目录。</summary>
    void Initialize();

    void Cancel();
}
