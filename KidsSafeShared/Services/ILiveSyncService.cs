namespace KidsSafeDesktop.Services;

/// <summary>
/// 家长端「实时下发」通道：长轮询服务器，家长一改配置，孩子设备秒级收到并拉取。
///
/// 为什么不用厂商推送（荣耀/华为/小米…）：
///  厂商推送通道普遍要求企业开发者资质，个人开发者拿不到；而且每接一家就要多一套 SDK。
///  这个 App 本身就是常驻前台的桌面，用一个长轮询请求换来「配置秒级生效」已经足够，
///  且完全不依赖任何第三方服务。
///
/// 约定：所有失败都静默降级（只改状态文字），绝不影响桌面使用。
/// </summary>
public interface ILiveSyncService
{
    /// <summary>循环是否在运行。</summary>
    bool IsRunning { get; }

    /// <summary>通道状态文字（用于家长面板/诊断页显示）。</summary>
    string Status { get; }

    /// <summary>收到远端配置变更（参数为一句可直接展示的说明）。在后台线程触发，UI 侧请自行切主线程。</summary>
    event EventHandler<string>? ConfigUpdated;

    /// <summary>启动/重启循环。未配置服务器或未登录时只更新状态，不抛异常。</summary>
    void Start();

    /// <summary>停止循环。</summary>
    void Stop();
}
