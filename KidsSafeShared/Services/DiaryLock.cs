using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

public enum DiaryUnlockResult
{
    /// <summary>密码正确。</summary>
    Ok,
    /// <summary>密码不对（还没到锁定期）。</summary>
    Wrong,
    /// <summary>试错次数太多，正在冷却。</summary>
    Locked,
    /// <summary>还没设置过日记密码。</summary>
    NotSet
}

/// <summary>
/// 日记密码。
///
/// 几个刻意的决定：
/// - **只存派生值**：PBKDF2-HMAC-SHA256，10 万次迭代 + 16 字节随机盐。不存明文，也不存可逆结果，
///   拿到存储文件也没法反推出孩子设的 4 位数字。虽然 4 位数字本身空间只有 1 万种，
///   但攻击成本被拉到「每猜一次要算 10 万次哈希」，配合下面的输错冷却，够用了。
/// - **4 位数字**：孩子记得住键盘位置比记住一串字符容易得多，也避免中文输入法的坑。
/// - **连续输错会冷却**（5 次 60 秒 → 8 次 180 秒 → 12 次 300 秒），且冷却时间落盘，
///   杀掉应用重开也不能绕过。任何情况下都**不删日记**——惩罚不该落在孩子写下的东西上。
/// - 没有「密保问题」这种自欺欺人的东西：答案往往是「我妈妈的名字」，比密码还好猜。
/// </summary>
public static class DiaryLock
{
    private const string Key = "diary_lock_v1";
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private sealed class State
    {
        public string Salt { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public int Fails { get; set; }
        /// <summary>UTC ticks，0 表示当前没在冷却。</summary>
        public long LockedUntilTicks { get; set; }
    }

    private static State Load()
    {
        try
        {
            var raw = Preferences.Get(Key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return new State();
            return JsonSerializer.Deserialize<State>(raw) ?? new State();
        }
        catch
        {
            return new State();
        }
    }

    private static void Save(State s)
    {
        try { Preferences.Set(Key, JsonSerializer.Serialize(s)); }
        catch { /* 存不下也不能影响开锁，宁可这次不记冷却 */ }
    }

    public static bool IsSet => Load().Hash.Length > 0;

    public static string Hint => Load().Hint;

    /// <summary>还差多少秒能再试。0 表示现在可以试。</summary>
    public static int RemainingLockSeconds
    {
        get
        {
            var s = Load();
            if (s.LockedUntilTicks <= 0) return 0;
            var left = new DateTime(s.LockedUntilTicks, DateTimeKind.Utc) - DateTime.UtcNow;
            return left.TotalSeconds > 0 ? (int)Math.Ceiling(left.TotalSeconds) : 0;
        }
    }

    /// <summary>这一轮已经输错了几次（用于把「还剩几次」讲给孩子听）。</summary>
    public static int Fails => Load().Fails;

    public static bool IsFourDigits(string? pin)
        => pin is { Length: 4 } && pin.All(c => c is >= '0' and <= '9');

    /// <summary>设置或重设密码。</summary>
    public static void SetPin(string pin, string hint = "")
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var state = new State
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(Derive(pin, salt)),
            Hint = (hint ?? string.Empty).Trim(),
            Fails = 0,
            LockedUntilTicks = 0
        };
        Save(state);
    }

    /// <summary>只换提示语，不动密码。</summary>
    public static void SetHint(string hint)
    {
        var s = Load();
        if (s.Hash.Length == 0) return;
        s.Hint = (hint ?? string.Empty).Trim();
        Save(s);
    }

    /// <summary>校验密码。remainingSeconds 在返回 Locked 时是剩余冷却秒数，其余情况为 0。</summary>
    public static DiaryUnlockResult Verify(string pin, out int remainingSeconds)
    {
        var s = Load();
        remainingSeconds = 0;

        if (s.Hash.Length == 0) return DiaryUnlockResult.NotSet;

        if (s.LockedUntilTicks > 0)
        {
            var left = new DateTime(s.LockedUntilTicks, DateTimeKind.Utc) - DateTime.UtcNow;
            if (left.TotalSeconds > 0)
            {
                remainingSeconds = (int)Math.Ceiling(left.TotalSeconds);
                return DiaryUnlockResult.Locked;
            }

            // 冷却结束，次数从这一轮重新算
            s.LockedUntilTicks = 0;
            s.Fails = 0;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(s.Salt);
            expected = Convert.FromBase64String(s.Hash);
        }
        catch
        {
            return DiaryUnlockResult.Wrong;
        }

        if (CryptographicOperations.FixedTimeEquals(Derive(pin, salt), expected))
        {
            s.Fails = 0;
            s.LockedUntilTicks = 0;
            Save(s);
            return DiaryUnlockResult.Ok;
        }

        s.Fails++;

        var cooldown = s.Fails switch
        {
            >= 12 => 300,
            >= 8 => 180,
            >= 5 => 60,
            _ => 0
        };

        if (cooldown > 0)
        {
            s.LockedUntilTicks = DateTime.UtcNow.AddSeconds(cooldown).Ticks;
            remainingSeconds = cooldown;
        }

        Save(s);
        return DiaryUnlockResult.Wrong;
    }

    /// <summary>清掉密码（家长重置时用）。不会碰日记内容。</summary>
    public static void Reset() => Save(new State());

    private static byte[] Derive(string pin, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin ?? string.Empty),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
}
