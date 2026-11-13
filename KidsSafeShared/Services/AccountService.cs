using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 账号服务实现（共享，全平台通用）。
/// 本地账号信息持久化在 SecureStorage；服务器连接状态复用 ChildSettings 的 Server* 字段，
/// 因此「在线模式」的判定与服务端登录令牌保持一致。
/// </summary>
public class AccountService : IAccountService
{
    private const string AccountKey = "kids_account_v1";
    private static readonly string[] Avatars =
    {
        "🐯", "🐰", "🐻", "🐼", "🦁", "🐸", "🐥", "🐨", "🦊", "🐶", "🐱", "🐵"
    };

    private readonly ISettingsService _settings;
    private readonly IServerSyncService _sync;

    public UserAccount Current { get; private set; } = new();
    public bool IsOffline => Current.Mode == AccountMode.Offline;
    public event EventHandler? Changed;

    public AccountService(ISettingsService settings, IServerSyncService sync)
    {
        _settings = settings;
        _sync = sync;
    }

    public async Task EnsureAccountAsync()
    {
        var c = _settings.Current;

        // 首次运行：创建本地离线账号
        if (string.IsNullOrEmpty(c.AccountId))
        {
            c.AccountId = Guid.NewGuid().ToString("N")[..8];
            if (string.IsNullOrWhiteSpace(c.AvatarEmoji))
                c.AvatarEmoji = Avatars[Math.Abs(StableHash(c.Nickname)) % Avatars.Length];
            await _settings.SaveAsync();
        }

        Hydrate();
    }

    public async Task ConnectServerAsync(string url, string username, string secret)
    {
        var c = _settings.Current;
        c.ServerUrl = url.Trim();
        c.ServerEnabled = true;
        await _settings.SaveAsync();

        var ok = await _sync.LoginAsync(username, secret);
        if (!ok)
        {
            // 登录失败回滚启用状态，保持离线
            c.ServerEnabled = false;
            await _settings.SaveAsync();
            Hydrate();
            throw new InvalidOperationException(_sync.Status);
        }

        // 登录成功后立即拉取一次配置
        await _sync.PullAsync();
        Hydrate();
    }

    public async Task DisconnectAsync()
    {
        var c = _settings.Current;
        c.ServerEnabled = false;
        c.ServerToken = string.Empty;
        await _settings.SaveAsync();
        Hydrate();
    }

    public async Task UpdateProfileAsync(string name, string avatar)
    {
        var c = _settings.Current;
        c.Nickname = string.IsNullOrWhiteSpace(name) ? "小朋友" : name.Trim();
        if (c.Nickname.Length > 8) c.Nickname = c.Nickname[..8];
        c.AvatarEmoji = string.IsNullOrWhiteSpace(avatar) ? "🐯" : avatar;
        await _settings.SaveAsync();
        Hydrate();
    }

    private void Hydrate()
    {
        var c = _settings.Current;
        var online = c.ServerEnabled
                     && !string.IsNullOrWhiteSpace(c.ServerUrl)
                     && !string.IsNullOrWhiteSpace(c.ServerToken);
        Current = new UserAccount
        {
            Id = c.AccountId,
            Name = string.IsNullOrWhiteSpace(c.Nickname) ? "小朋友" : c.Nickname,
            AvatarEmoji = string.IsNullOrWhiteSpace(c.AvatarEmoji) ? "🐯" : c.AvatarEmoji,
            Mode = online ? AccountMode.Online : AccountMode.Offline,
            ServerUrl = c.ServerUrl
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static int StableHash(string s)
    {
        int h = 0;
        foreach (var ch in s)
            h = (h * 31 + ch) & 0x7fffffff;
        return h;
    }
}
