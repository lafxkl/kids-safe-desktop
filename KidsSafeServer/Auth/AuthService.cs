using System.Security.Cryptography;

namespace KidsSafeServer.Auth;

/// <summary>账号体系的可配置项，来自 appsettings 的 <c>Auth</c> 节。</summary>
public sealed class AuthOptions
{
    /// <summary>老版本的单一家长密码（默认 1234）。仅作兜底，注册账号后建议关掉。</summary>
    public string AdminPin { get; set; } = "1234";

    /// <summary>是否允许继续注册新账号。首个账号永远允许注册。</summary>
    public bool AllowRegister { get; set; } = true;

    /// <summary>
    /// 是否允许 PIN 登录。null（默认）= 智能判定：一个账号都没有时允许（方便老用户升级），
    /// 注册过账号之后自动关闭，只剩账号+密码和配对码两条路。
    /// </summary>
    public bool? AllowPinLogin { get; set; }
}

/// <summary>一次请求解析出来的身份。</summary>
public sealed record AuthSession(
    string AccountId,
    string Name,
    string Role,
    bool IsDevice,
    string? DeviceId = null,
    string? Username = null)
{
    /// <summary>只有家长能改配置；设备令牌只读。</summary>
    public bool CanWrite => !IsDevice;
}

/// <summary>登录/注册结果。成功时 <see cref="Session"/> 与 <see cref="Token"/> 都有值。</summary>
public sealed class AuthResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? Token { get; init; }
    public AuthSession? Session { get; init; }

    public static AuthResult Fail(string error) => new() { Ok = false, Error = error };
    public static AuthResult Success(string token, AuthSession session) =>
        new() { Ok = true, Token = token, Session = session };
}

/// <summary>
/// 登录失败的简易限流：同一个来源 IP 连续失败 5 次，锁 60 秒。
/// 只放在内存里 —— 重启归零可以接受，目的是挡住脚本狂撞密码，不是做审计。
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxFails = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromSeconds(60);

    private readonly Dictionary<string, (int Fails, DateTimeOffset Until)> _map = new();
    private readonly object _lock = new();

    public TimeSpan? LockedFor(string key)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var v) && v.Until > DateTimeOffset.UtcNow)
                return v.Until - DateTimeOffset.UtcNow;
            return null;
        }
    }

    public void Fail(string key)
    {
        lock (_lock)
        {
            var fails = _map.TryGetValue(key, out var v) ? v.Fails + 1 : 1;
            _map[key] = fails >= MaxFails
                ? (0, DateTimeOffset.UtcNow + LockFor)
                : (fails, DateTimeOffset.MinValue);
        }
    }

    public void Reset(string key)
    {
        lock (_lock) _map.Remove(key);
    }
}

/// <summary>
/// 账号体系的门面：注册、登录、配对、令牌解析全在这里，Program.cs 只做路由。
///
/// 三条登录路径，覆盖三种真实场景：
///  1. 账号 + 密码  → 家长在网页/家长端用（可写配置）；
///  2. 6 位配对码   → 儿童设备用（只读配置，家长不必把密码告诉孩子）；
///  3. 老 PIN (1234) → 兼容老版本客户端与服务端初次上手（注册账号后自动关闭）。
/// </summary>
public sealed class AuthService
{
    private const string LegacyToken = "kids-admin-token";
    private const string LegacyAccountId = "default";

    private static readonly TimeSpan ParentTokenLife = TimeSpan.FromDays(30);
    private static readonly TimeSpan DeviceTokenLife = TimeSpan.FromDays(365);
    private static readonly TimeSpan PairCodeLife = TimeSpan.FromMinutes(10);

    private readonly AccountStore _store;
    private readonly TokenService _tokens;
    private readonly ProfileStore _profiles;
    private readonly AuthOptions _options;

    public AuthService(AccountStore store, TokenService tokens, ProfileStore profiles, AuthOptions options)
    {
        _store = store;
        _tokens = tokens;
        _profiles = profiles;
        _options = options;
    }

    public AccountStore Store => _store;

    /// <summary>PIN 登录是否可用：显式配置优先，否则「还没注册过账号」时才允许。</summary>
    public bool PinLoginEnabled => _options.AllowPinLogin ?? _store.Count == 0;

    public bool RegistrationOpen => _options.AllowRegister || _store.Count == 0;

    /// <summary>配对码有效期（分钟），登录页展示用。</summary>
    public int PairCodeMinutes => (int)PairCodeLife.TotalMinutes;

    // ── 注册 ─────────────────────────────────────────────────────────────

    public AuthResult Register(string? username, string? password, string? displayName)
    {
        if (!RegistrationOpen) return AuthResult.Fail("服务端已关闭注册");

        var error = AccountRules.ValidateUsername(username) ?? AccountRules.ValidatePassword(password);
        if (error is not null) return AuthResult.Fail(error);

        var key = username!.Trim().ToLowerInvariant();
        if (_store.FindByUsername(key) is not null) return AuthResult.Fail("该账号已被使用");

        var (hash, salt) = PasswordHasher.Create(password!);
        var first = _store.Count == 0;

        var account = new Account
        {
            Id = _store.NewId(),
            Username = key,
            DisplayName = AccountRules.CleanDisplayName(displayName, key),
            PasswordHash = hash,
            Salt = salt,
            Role = first ? "owner" : "parent",
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        _store.Add(account);

        // 第一个账号接管老配置：profile.json 复制过来，老设备的固定令牌也指向它，
        // 于是升级服务端这件事对已经装好的儿童端完全透明。
        if (first)
        {
            _profiles.AdoptLegacy(account.Id);
            _store.BootstrapAccountId = account.Id;
        }

        return IssueParent(account);
    }

    // ── 登录 ─────────────────────────────────────────────────────────────

    public AuthResult LoginByPassword(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return AuthResult.Fail("请填写账号和密码");

        var account = _store.FindByUsername(username);
        if (account is null) return AuthResult.Fail("账号或密码错误");

        if (!PasswordHasher.Verify(password, account.PasswordHash, account.Salt))
            return AuthResult.Fail("账号或密码错误");

        Touch(account);
        return IssueParent(account);
    }

    /// <summary>
    /// 兼容老客户端的 <c>{ pin }</c> 登录：先按配对码解释（6 位数字），
    /// 再按老家长密码解释。这样儿童端一行都不用改就能用新体系。
    /// </summary>
    public AuthResult LoginByPin(string? pin, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(pin)) return AuthResult.Fail("请输入密码或配对码");
        var code = pin.Trim();

        var byCode = PairDevice(code, deviceName);
        if (byCode.Ok) return byCode;

        // 6 位纯数字按配对码理解：直接回它的错误，别让家长看到「请用账号密码」这种答非所问的提示
        if (code.Length == 6 && code.All(char.IsDigit)) return byCode;

        if (!PinLoginEnabled) return AuthResult.Fail("该服务器已启用账号登录，请使用账号密码或设备配对码");

        if (code != _options.AdminPin) return AuthResult.Fail("密码错误");

        // 老密码登录：落到 bootstrap 账号（若有），否则落到虚拟的 default 账号
        var bootstrap = _store.BootstrapAccountId;
        var account = bootstrap is null ? null : _store.Find(bootstrap);
        if (bootstrap is not null && account is not null) return IssueParent(account);

        var session = new AuthSession(LegacyAccountId, "家长", "owner", false);
        var token = _tokens.Issue(new TokenClaims
        {
            Sub = LegacyAccountId,
            Name = "家长",
            Role = "owner",
            Ver = 0,
            IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ParentTokenLife).ToUnixTimeSeconds()
        });
        return AuthResult.Success(token, session);
    }

    /// <summary>用配对码把一台儿童设备绑定到账号。设备令牌只能读配置。</summary>
    public AuthResult PairDevice(string? code, string? deviceName)
    {
        var c = (code ?? "").Trim();
        if (c.Length == 0) return AuthResult.Fail("请输入配对码");

        var account = _store.FindByPairCode(c);
        if (account is null) return AuthResult.Fail("配对码无效或已过期，请在家长后台重新生成");

        var name = string.IsNullOrWhiteSpace(deviceName) ? "儿童设备" : deviceName!.Trim();
        if (name.Length > 20) name = name[..20];

        // 同名设备复用记录，避免同一台平板反复登录长出一串重复条目
        var device = account.Devices.FirstOrDefault(d => d.Name == name)
                     ?? new DeviceEntry { Id = "d_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(), Name = name };
        if (!account.Devices.Contains(device)) account.Devices.Add(device);
        device.LastSeenAt = DateTimeOffset.UtcNow;

        // 配对码一次性使用：用完立刻作废，防止被旁人瞄到后重复使用
        account.PairCode = null;
        account.PairCodeExpiresAt = null;
        _store.Update(account);

        var session = new AuthSession(account.Id, name, "device", true, device.Id, account.Username);
        var token = _tokens.Issue(new TokenClaims
        {
            Sub = account.Id,
            Name = name,
            Role = "device",
            Ver = 0,
            Dev = device.Id,
            IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.Add(DeviceTokenLife).ToUnixTimeSeconds()
        });
        return AuthResult.Success(token, session);
    }

    // ── 令牌解析 ─────────────────────────────────────────────────────────

    public AuthSession? Resolve(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        // 老的固定令牌：只用于升级过渡，映射到 bootstrap 账号
        if (token == LegacyToken)
        {
            var bootstrap = _store.BootstrapAccountId;
            if (bootstrap is null) return new AuthSession(LegacyAccountId, "家长", "owner", false);
            var owned = _store.Find(bootstrap);
            return owned is null ? null : new AuthSession(owned.Id, owned.DisplayName, owned.Role, false, null, owned.Username);
        }

        var claims = _tokens.Verify(token);
        if (claims is null) return null;

        if (claims.Role == "device")
        {
            var host = _store.Find(claims.Sub);
            if (host is null) return null;
            var device = host.Devices.FirstOrDefault(d => d.Id == claims.Dev);
            if (device is null) return null;   // 后台移除过设备 → 令牌立即作废
            device.LastSeenAt = DateTimeOffset.UtcNow;
            return new AuthSession(host.Id, device.Name, "device", true, device.Id, host.Username);
        }

        var account = _store.Find(claims.Sub);
        if (account is null)
        {
            // default 虚拟账号（未注册时的老密码登录）
            return claims.Sub == LegacyAccountId && claims.Ver == 0
                ? new AuthSession(LegacyAccountId, claims.Name, "owner", false)
                : null;
        }

        // 改过密码：此前的令牌全部失效
        if (claims.Ver != account.PasswordVersion) return null;

        return new AuthSession(account.Id, account.DisplayName, account.Role, false, null, account.Username);
    }

    // ── 账号维护 ─────────────────────────────────────────────────────────

    public string? ChangePassword(string accountId, string? oldPassword, string? newPassword)
    {
        var account = _store.Find(accountId);
        if (account is null) return "账号不存在";
        if (!PasswordHasher.Verify(oldPassword ?? "", account.PasswordHash, account.Salt))
            return "原密码不正确";

        var error = AccountRules.ValidatePassword(newPassword);
        if (error is not null) return error;

        var (hash, salt) = PasswordHasher.Create(newPassword!);
        account.PasswordHash = hash;
        account.Salt = salt;
        account.PasswordVersion++;
        _store.Update(account);
        return null;
    }

    public string? RenameAccount(string accountId, string? displayName)
    {
        var account = _store.Find(accountId);
        if (account is null) return "账号不存在";
        account.DisplayName = AccountRules.CleanDisplayName(displayName, account.Username);
        _store.Update(account);
        return null;
    }

    /// <summary>生成（或刷新）6 位配对码，10 分钟内有效，用一次即废。</summary>
    public (string? Code, DateTimeOffset? ExpiresAt, string? Error) NewPairCode(string accountId)
    {
        var account = _store.Find(accountId);
        if (account is null) return (null, null, "该账号不支持配对码（请使用正式注册的家长账号）");

        var code = _store.NewPairCode();
        var exp = DateTimeOffset.UtcNow.Add(PairCodeLife);
        account.PairCode = code;
        account.PairCodeExpiresAt = exp;
        _store.Update(account);
        return (code, exp, null);
    }

    public bool RemoveDevice(string accountId, string deviceId)
    {
        var account = _store.Find(accountId);
        if (account is null) return false;
        var removed = account.Devices.RemoveAll(d => d.Id == deviceId) > 0;
        if (removed) _store.Update(account);
        return removed;
    }

    // ── 内部 ─────────────────────────────────────────────────────────────

    private AuthResult IssueParent(Account account)
    {
        var session = new AuthSession(account.Id, account.DisplayName, account.Role, false, null, account.Username);
        var token = _tokens.Issue(new TokenClaims
        {
            Sub = account.Id,
            Name = account.DisplayName,
            Role = account.Role,
            Ver = account.PasswordVersion,
            IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ParentTokenLife).ToUnixTimeSeconds()
        });
        return AuthResult.Success(token, session);
    }

    private void Touch(Account account)
    {
        account.LastLoginAt = DateTimeOffset.UtcNow;
        _store.Update(account);
    }
}
