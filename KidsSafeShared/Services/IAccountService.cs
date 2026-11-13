using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 账号服务：管理儿童本地账号与运行模式。
/// 设计原则：本地优先、离线可用。首次运行自动创建离线账号；
/// 家长可在「账号」页连接家长端服务器，切换为在线模式（云端同步）。
/// </summary>
public interface IAccountService
{
    /// <summary>当前账号（在线时 ServerUrl 非空）。</summary>
    UserAccount Current { get; }

    /// <summary>是否处于离线模式（未连接家长服务器）。</summary>
    bool IsOffline { get; }

    /// <summary>账号或模式变化时触发（用于刷新 UI 徽章）。</summary>
    event EventHandler? Changed;

    /// <summary>确保存在一个本地账号（首次运行创建离线账号），并刷新 Current。</summary>
    Task EnsureAccountAsync();

    /// <summary>
    /// 连接家长端服务器并登录。成功后将模式切换为在线并立即拉取一次配置。
    /// 失败抛出带原因的异常。
    /// <paramref name="username"/> 为空时按「家长密码 / 6 位配对码」登录（只读同步）；
    /// 填了账号则按账号密码登录（可写配置）。
    /// </summary>
    Task ConnectServerAsync(string url, string username, string secret);

    /// <summary>断开与家长端服务器的连接，切回离线模式。</summary>
    Task DisconnectAsync();

    /// <summary>更新昵称与头像（同时写回 ChildSettings 并持久化）。</summary>
    Task UpdateProfileAsync(string name, string avatar);
}
