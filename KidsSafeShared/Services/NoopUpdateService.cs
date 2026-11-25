using System.ComponentModel;
using KidsSafeDesktop.Updates;

namespace KidsSafeDesktop.Services;

/// <summary>
/// iOS 的增量更新占位实现：**永远没有更新、也永远不下载**。
///
/// 不是「懒得做」，而是 App Store 审核指南 2.5.2 明确禁止应用在运行期下载并执行
/// 会改变应用功能的代码。Android 那套「下 dll → 下次启动预加载」的做法搬到 iOS
/// 就是拒审项，所以 iOS 的更新只有一条路：发新版本到 App Store。
///
/// 界面不需要改：家长中心里的「检查更新」在 iOS 上会走到真实界面的空状态
/// （State 恒为 UpToDate），点下去也不会联网。
/// </summary>
public sealed class NoopUpdateService : IUpdateService
{
    /// <summary>状态永远不变，属性变更通知不会触发；显式空实现避免 CS0067 告警。</summary>
    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

    public UpdateState State => UpdateState.UpToDate;

    public string Status => "App Store 版本由系统自动更新";

    public string? AvailableVersion => null;

    public string? Notes => null;

    public double Progress => 0;

    public string? LastResult => null;

    public bool HasPendingPatch => false;

    public Task<bool> CheckAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<bool> DownloadAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<bool> ApplyAsync(bool restart) => Task.FromResult(false);

    public void Initialize()
    {
        // 没有任何落盘计划要恢复
    }

    public void Cancel()
    {
        // 没有可取消的下载
    }
}
