using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KidsSafeServer.Auth;

/// <summary>令牌里携带的最小身份信息。字段名用短名，让令牌短一点。</summary>
public sealed class TokenClaims
{
    [JsonPropertyName("sub")] public string Sub { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>owner / parent / device。</summary>
    [JsonPropertyName("role")] public string Role { get; set; } = "parent";

    /// <summary>口令版本号，改密码后 +1，旧令牌立即失效（设备令牌固定为 0，不受影响）。</summary>
    [JsonPropertyName("ver")] public int Ver { get; set; }

    /// <summary>设备令牌专属：设备 Id，用来支持「后台踢掉某台设备」。</summary>
    [JsonPropertyName("dev")] public string? Dev { get; set; }

    [JsonPropertyName("iat")] public long IssuedAt { get; set; }
    [JsonPropertyName("exp")] public long ExpiresAt { get; set; }
}

/// <summary>
/// 自签令牌：<c>v1.&lt;base64url(payload)&gt;.&lt;base64url(HMAC-SHA256)&gt;</c>。
///
/// 为什么不用 JWT 库：多加一个 NuGet 依赖，发布目录就多一堆 dll，而这个项目
/// 一直刻意保持「一个 exe 双击就能跑」。HMAC 签名 + JSON 载荷三十行就够，
/// 语义和 JWT 的 HS256 完全一致，客户端只是把令牌当不透明字符串回传。
///
/// 密钥来源：优先配置项 <c>Auth:Secret</c>；没配就生成 32 字节随机密钥写到
/// <c>data/auth.key</c>，保证重启后已登录的家长不会被强制退出。
/// </summary>
public sealed class TokenService
{
    private const string Prefix = "v1";
    private readonly byte[] _key;

    public TokenService(string? secret, string keyFilePath)
    {
        if (!string.IsNullOrWhiteSpace(secret))
        {
            _key = Encoding.UTF8.GetBytes(secret!);
            return;
        }

        var dir = Path.GetDirectoryName(keyFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);

        if (File.Exists(keyFilePath))
        {
            var text = File.ReadAllText(keyFilePath).Trim();
            if (text.Length >= 32)
            {
                _key = Convert.FromBase64String(text);
                return;
            }
        }

        _key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(keyFilePath, Convert.ToBase64String(_key));
    }

    public string Issue(TokenClaims claims)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(claims);
        var head = Base64Url(payload);
        var sig = Sign(head);
        return $"{Prefix}.{head}.{sig}";
    }

    /// <summary>校验签名与过期时间；任何一项不通过都返回 <c>null</c>，不区分原因。</summary>
    public TokenClaims? Verify(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return null;

        var expected = Sign(parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
            return null;

        try
        {
            var json = Convert.FromBase64String(Pad(parts[1]));
            var claims = JsonSerializer.Deserialize<TokenClaims>(json);
            if (claims is null || string.IsNullOrEmpty(claims.Sub)) return null;
            if (claims.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return claims;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private string Sign(string head)
    {
        using var hmac = new HMACSHA256(_key);
        return Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(head)));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>base64url 去掉了补位 <c>=</c>，还原成标准 base64 需要补回来。</summary>
    private static string Pad(string s) => (s.Length % 4) switch
    {
        2 => s + "==",
        3 => s + "=",
        _ => s
    };
}
