using System.Security.Cryptography;

namespace KidsSafeServer.Auth;

/// <summary>
/// 口令哈希：PBKDF2-HMAC-SHA256，10 万次迭代，16 字节随机盐，32 字节输出。
///
/// 为什么不用明文/MD5/SHA1：这个服务虽然是「家里电脑上跑」的形态，但一旦家长把端口
/// 映射到公网（很多人会为了在外面改设置而这么做），弱哈希等于把口令直接交给扫描器。
/// PBKDF2 在 .NET 基础库里就有（<see cref="Rfc2898DeriveBytes"/>），零依赖，够用。
///
/// 盐与哈希分开存，便于以后换算法时按 <see cref="Scheme"/> 前缀识别。
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>当前使用的算法标识，写进哈希串前缀，将来升级可平滑迁移。</summary>
    public const string Scheme = "pbkdf2-sha256";

    /// <summary>生成 <c>(hash, salt)</c>，两者都是 Base64 文本。</summary>
    public static (string Hash, string Salt) Create(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// 校验口令。用 <see cref="CryptographicOperations.FixedTimeEquals"/> 做定长比较，
    /// 避免按字节提前返回被计时攻击探测出「前几位对了」。
    /// </summary>
    public static bool Verify(string password, string? hash, string? salt)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt)) return false;
        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var expected = Convert.FromBase64String(hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, saltBytes, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
