namespace KidsSafeDesktop.Services;

/// <summary>
/// 家长端服务器同步：登录获取令牌，并从服务器拉取托管配置合并到本地设置。
/// </summary>
public interface IServerSyncService
{
    /// <summary>最近一次操作的状态描述（用于 UI 显示）。</summary>
    string Status { get; }

    /// <summary>是否已配置服务器地址并启用。</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// 登录服务器。两种凭证二选一：
    /// <list type="bullet">
    ///   <item><paramref name="username"/> 为空时，<paramref name="secret"/> 当作家长密码或 6 位配对码；</item>
    ///   <item>填了账号则按「账号 + 密码」登录，可写配置（配对码登录是只读的）。</item>
    /// </list>
    /// </summary>
    Task<bool> LoginAsync(string? username, string secret);

    /// <summary>从服务器拉取配置并合并到本地设置。返回是否成功。</summary>
    Task<bool> PullAsync();
}
