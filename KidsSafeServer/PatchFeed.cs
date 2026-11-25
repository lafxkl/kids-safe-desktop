using System.Text.Json;

namespace KidsSafeServer;

/// <summary>
/// 增量补丁的供货单。
///
/// 服务端**不持有私钥、不在线签名** —— 它只把发版机上签好的
/// <c>manifest.json</c> + <c>manifest.sig</c> 原样转出去。
/// 这样即使服务器被入侵，攻击者也发不出能通过客户端验签的补丁，
/// 最多让客户端「这次没更新成」。这是整个自更新链路里最重要的一个分工。
///
/// 目录约定（由 Scripts/build-patch.ps1 生成）：
/// <code>
///   wwwroot/patches/windows/current/manifest.json
///   wwwroot/patches/windows/current/manifest.sig
///   wwwroot/patches/windows/current/files/KidsSafeShared.dll
/// </code>
/// </summary>
public static class PatchFeed
{
    /// <summary>
    /// 读取某个平台的当前补丁。
    /// 返回 null 表示「没有补丁」；返回对象时字段直接给客户端用。
    /// </summary>
    public static object? Read(string webRoot, string platform, string? fromVersion)
    {
        var dir = Path.Combine(webRoot, "patches", platform, "current");
        var manifestPath = Path.Combine(dir, "manifest.json");
        var signaturePath = Path.Combine(dir, "manifest.sig");
        if (!File.Exists(manifestPath) || !File.Exists(signaturePath))
        {
            return null;
        }

        var manifestBytes = File.ReadAllBytes(manifestPath);

        // 客户端当前版本不在 base 里 → 这份增量用不上，直接说清楚，
        // 让它走整包更新，而不是下到一半再因为不适用而放弃。
        if (!string.IsNullOrWhiteSpace(fromVersion))
        {
            string[]? bases = null;
            try
            {
                using var doc = JsonDocument.Parse(manifestBytes);
                if (doc.RootElement.TryGetProperty("base", out var node) &&
                    node.ValueKind == JsonValueKind.Array)
                {
                    bases = node.EnumerateArray()
                                .Select(item => item.GetString())
                                .Where(item => !string.IsNullOrWhiteSpace(item))
                                .Select(item => item!)
                                .ToArray();
                }
            }
            catch (JsonException)
            {
                // 清单坏了就当没有补丁：宁可让客户端整包，也别发半份东西出去
                return null;
            }

            if (bases is { Length: > 0 } &&
                !bases.Any(b => string.Equals(b, fromVersion, StringComparison.OrdinalIgnoreCase)))
            {
                return new
                {
                    ok = true,
                    available = false,
                    reason = $"服务端没有「从 {fromVersion} 升到最新版」的增量补丁，请整包更新"
                };
            }
        }

        return new
        {
            ok = true,
            available = true,
            // base64 包一层：客户端要拿到的字节必须与签名时逐字节一致，
            // 走 JSON 字符串会被转义/换行破坏，这是自更新最常见的坑。
            manifest = Convert.ToBase64String(manifestBytes),
            signature = Convert.ToBase64String(File.ReadAllBytes(signaturePath)),
            baseUrl = $"/patches/{platform}/current/files/"
        };
    }

    /// <summary>当前补丁的版本号（写进 /api/health，方便运维不登服务器就知道发到哪一版了）。</summary>
    public static string? VersionOf(string webRoot, string platform)
    {
        try
        {
            var path = Path.Combine(webRoot, "patches", platform, "current", "manifest.json");
            if (!File.Exists(path))
            {
                return null;
            }
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            return doc.RootElement.TryGetProperty("version", out var version)
                ? version.GetString()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
