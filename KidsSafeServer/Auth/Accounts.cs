using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KidsSafeServer.Auth;

/// <summary>一台已配对的儿童设备（用配对码登录过，只能读配置，不能改）。</summary>
public sealed class DeviceEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset PairedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
}

/// <summary>
/// 一个家长账号。口令只存 PBKDF2 哈希；<see cref="PasswordVersion"/> 每次改密码 +1，
/// 用来让改密码之前签发的令牌立刻失效。
/// </summary>
public sealed class Account
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";

    /// <summary>显示名（家庭名，如「小明家」），网页右上角显示。</summary>
    public string DisplayName { get; set; } = "";

    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public int PasswordVersion { get; set; } = 1;

    /// <summary>owner = 第一个注册的账号（可管理其他账号）；parent = 普通家长。</summary>
    public string Role { get; set; } = "parent";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>儿童设备配对码（6 位数字），给「不想在平板上输家长密码」的场景用。</summary>
    public string? PairCode { get; set; }
    public DateTimeOffset? PairCodeExpiresAt { get; set; }

    public List<DeviceEntry> Devices { get; set; } = new();

    /// <summary>对外暴露的账号信息（绝不包含哈希/盐）。</summary>
    public object ToPublic() => new
    {
        id = Id,
        username = Username,
        displayName = DisplayName,
        role = Role,
        createdAt = CreatedAt,
        lastLoginAt = LastLoginAt,
        devices = Devices.Select(d => new { id = d.Id, name = d.Name, pairedAt = d.PairedAt, lastSeenAt = d.LastSeenAt })
    };
}

/// <summary>accounts.json 的落盘结构。</summary>
public sealed class AccountDatabase
{
    /// <summary>
    /// 老客户端手里那个固定令牌 "kids-admin-token" 映射到哪个账号。
    /// 第一个账号注册时自动迁到这里，于是升级服务端后老设备不用重新配对。
    /// </summary>
    public string? BootstrapAccountId { get; set; }

    public List<Account> Accounts { get; set; } = new();
}

/// <summary>账号存储：单文件 JSON（data/accounts.json），读写全加锁，够家用规模。</summary>
public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly object _lock = new();
    private AccountDatabase _db;

    public AccountStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
        _db = LoadFromDisk();
    }

    private AccountDatabase LoadFromDisk()
    {
        if (!File.Exists(_path)) return new AccountDatabase();
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AccountDatabase>(json, JsonOptions) ?? new AccountDatabase();
        }
        catch
        {
            // 文件损坏时保留一份现场，别让家长改过的账号凭空消失
            try { File.Copy(_path, _path + ".broken", overwrite: true); } catch { }
            return new AccountDatabase();
        }
    }

    private void FlushLocked()
    {
        var json = JsonSerializer.Serialize(_db, JsonOptions);
        File.WriteAllText(_path, json);
    }

    public int Count
    {
        get { lock (_lock) return _db.Accounts.Count; }
    }

    public string? BootstrapAccountId
    {
        get { lock (_lock) return _db.BootstrapAccountId; }
        set { lock (_lock) { _db.BootstrapAccountId = value; FlushLocked(); } }
    }

    public List<Account> All()
    {
        lock (_lock) return _db.Accounts.ToList();
    }

    public Account? Find(string id)
    {
        lock (_lock) return _db.Accounts.FirstOrDefault(a => a.Id == id);
    }

    public Account? FindByUsername(string username)
    {
        var key = username.Trim().ToLowerInvariant();
        lock (_lock) return _db.Accounts.FirstOrDefault(a => a.Username == key);
    }

    public Account? FindByPairCode(string code)
    {
        lock (_lock)
            return _db.Accounts.FirstOrDefault(a =>
                a.PairCode == code && a.PairCodeExpiresAt is { } exp && exp > DateTimeOffset.UtcNow);
    }

    public void Add(Account account)
    {
        lock (_lock) { _db.Accounts.Add(account); FlushLocked(); }
    }

    /// <summary>
    /// 更新账号。传进来的如果不是同一个对象实例，按 Id 覆盖数组里的那一份。
    /// 统一走这个方法，避免各处直接改内存对象导致没落盘。
    /// </summary>
    public void Update(Account account)
    {
        lock (_lock)
        {
            var idx = _db.Accounts.FindIndex(a => a.Id == account.Id);
            if (idx < 0) _db.Accounts.Add(account);
            else _db.Accounts[idx] = account;
            FlushLocked();
        }
    }

    /// <summary>生成一个不与现存账号冲突的账号 Id（u_ + 8 位十六进制）。</summary>
    public string NewId()
    {
        lock (_lock)
        {
            while (true)
            {
                var id = "u_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
                if (_db.Accounts.All(a => a.Id != id)) return id;
            }
        }
    }

    /// <summary>生成 6 位纯数字配对码，避开已占用/未过期的。</summary>
    public string NewPairCode()
    {
        lock (_lock)
        {
            while (true)
            {
                var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                var taken = _db.Accounts.Any(a => a.PairCode == code && a.PairCodeExpiresAt > DateTimeOffset.UtcNow);
                if (!taken) return code;
            }
        }
    }
}

/// <summary>账号规则：用户名格式、口令长度，集中在这里，注册与改密码共用。</summary>
public static class AccountRules
{
    private static readonly Regex UsernamePattern = new("^[a-zA-Z0-9_-]{3,32}$", RegexOptions.Compiled);

    public static string? ValidateUsername(string? username) =>
        string.IsNullOrWhiteSpace(username) ? "请填写账号"
        : !UsernamePattern.IsMatch(username.Trim()) ? "账号需为 3–32 位字母、数字、下划线或短横线"
        : null;

    public static string? ValidatePassword(string? password) =>
        string.IsNullOrWhiteSpace(password) ? "请填写密码"
        : password.Length < 6 ? "密码至少 6 位"
        : password.Length > 64 ? "密码不能超过 64 位"
        : null;

    /// <summary>显示名兜底：空则用账号名。</summary>
    public static string CleanDisplayName(string? name, string username)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) return username;
        return n.Length > 20 ? n[..20] : n;
    }
}
