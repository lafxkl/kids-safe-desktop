using System.Security.Cryptography;

namespace KidsSafeDesktop.Updates;

/// <summary>
/// 清单与补丁文件的信任判断。这一层是整个自更新的安全边界，规则只有三条：
///
///   1. 清单必须带合法签名（私钥在发版机上，客户端只有公钥）；
///   2. 清单里的路径必须落在安装目录内、扩展名在白名单里（不许塞 exe / 脚本）；
///   3. 版本只能往上走（不许降级、不许重放旧清单）。
///
/// 任何一条不过，整份补丁作废 —— 不做「部分文件先装上」这种事。
/// </summary>
public static class UpdateVerifier
{
    /// <summary>
    /// 更新签名公钥。对应私钥在发版机 Keys/update-signing-private.pem（不入仓库、不入安装包）。
    /// 换密钥＝换这一段＝必须整包发一次，之后旧私钥签的所有补丁自动失效。
    /// </summary>
    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAwKIm1J1fDtTESqhF1uS8
SnyjdRK1ItfLBxgqadk2gcCj/yZsTcBthBEjBhfTp8+aGYDS2Nvm5C9RGSYJszkH
EMVWNz4BufprtH4XBRPnFlBeoSqn9yZJZ5dxuiR82fAmU1ecpfJRvdkGbD6JToiJ
N0bGNMYfSL6O+GYNUowMhu/wuIKohv9ZOJJU5dR/8LDvhcR4qJphMyAbzbnzEMyq
PHRToPmk/QuKPZlMQ4v4vvsu3bcH5L3FWbAef480o0b7AWVExGS3FHEGsLtTRGnj
s65iUZNDSDybhYgTkjFJy1m1jS+Y4xwiT8vfDnHbEhgdRGfMYHLeJYLUu7xpj9ue
O0rMh54ez/U9pLGN2O0dNigwObISKfkB/Q2ZZXKCmd+ijrvb1rsqSsG1PDsxv/oI
vU0dju8+gEyFGSpriKMrbx+9+A6sOS0bc5xTixmtAOhzvT8e5cJXIhynI6mJ8YFX
fVjYP98i0i63fHKy5ypyPX/kCzVTZYvYIiTc6YxY2B/MQBto0DCmPJv8V+SQMI2x
dn+NTKd2IkkPcG/lxiAJ2tLmQa3BrIvY7xWnS57wlqpd/SKD2g6HUG1wHxrJNCDl
hRA+eRuxl8WbrtCymrSyNgEFHUSPnp1Hx2fEtFiQfDzu/WKAbwTVosUZ4KSGX4Ox
Sf4cGRNKOEVcH3nEGCr6HQMCAwEAAQ==
-----END PUBLIC KEY-----
""";

    /// <summary>允许出现在补丁里的扩展名。</summary>
    /// <remarks>
    /// 刻意不含 .exe / .bat / .cmd / .ps1 / .msi / .dll 之外的二进制：
    /// 自更新通道一旦能替换可执行体，就等于给了一条绕过家长密码的路。
    /// 需要换宿主？那本来就得整包。
    /// </remarks>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll",                     // 托管程序集：本方案的主角
        ".json", ".dat", ".bin",    // 内容/配置
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg",  // 图片
        ".ttf", ".otf",             // 字体
        ".wav", ".mp3", ".ogg", ".m4a",                     // 音频
        ".html", ".css", ".js",     // 网页小游戏
        ".txt", ".csv"              // 纯文本素材
    };

    /// <summary>整份补丁的体积上限，防止一份恶意清单把磁盘填满。</summary>
    private const long MaxTotalBytes = 512L * 1024 * 1024;

    /// <summary>用内置公钥验证清单签名（RSA-4096 / PSS / SHA-256）。</summary>
    public static bool Verify(byte[] manifestBytes, byte[] signature)
    {
        if (manifestBytes.Length == 0 || signature.Length == 0)
        {
            return false;
        }
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            return rsa.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256,
                                  RSASignaturePadding.Pss);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// 字段校验。返回 null 表示通过，否则返回一句能直接显示给家长的中文原因。
    /// </summary>
    /// <param name="manifest">已验签的清单</param>
    /// <param name="platform">本机平台：windows / android</param>
    /// <param name="hostVersion">本机版本，如 "1.20"</param>
    /// <param name="hostCode">本机构建号</param>
    public static string? Validate(UpdateManifest manifest, string platform,
                                    string hostVersion, int hostCode, DateTimeOffset now)
    {
        if (manifest.Schema != 1)
        {
            return "补丁格式版本不兼容";
        }
        if (!string.Equals(manifest.Platform, platform, StringComparison.OrdinalIgnoreCase))
        {
            return $"这个补丁是给 {manifest.Platform} 用的，不适用于本机";
        }
        if (!Version.TryParse(manifest.Version, out _) || !Version.TryParse(hostVersion, out _))
        {
            return "版本号格式不对";
        }
        if (manifest.VersionCode <= hostCode)
        {
            return "补丁版本不比当前版本新（已拒绝降级）";
        }
        if (CompareVersion(hostVersion, manifest.MinHost) < 0)
        {
            return $"这个补丁要求宿主 ≥ {manifest.MinHost}，当前是 {hostVersion}，请整包更新";
        }
        // base 为空表示「任何版本都能用」——只有纯内容补丁（图片/文案）可以这么发。
        if (manifest.Base.Length > 0 &&
            !manifest.Base.Any(v => string.Equals(v, hostVersion, StringComparison.OrdinalIgnoreCase)))
        {
            return $"这个补丁只支持从 {string.Join(" / ", manifest.Base)} 升级，当前是 {hostVersion}";
        }
        if (manifest.ExpiresAt != default && manifest.ExpiresAt <= now)
        {
            return "补丁清单已过期";
        }
        if (manifest.Files.Count == 0 && manifest.Removed.Count == 0)
        {
            return "补丁是空的";
        }

        long total = 0;
        foreach (var file in manifest.Files)
        {
            if (!IsSafeRelativePath(file.Path))
            {
                return $"补丁里有不允许的文件路径：{file.Path}";
            }
            if (file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
            {
                return $"补丁里 {file.Path} 的哈希格式不对";
            }
            if (file.Size <= 0)
            {
                return $"补丁里 {file.Path} 的大小不对";
            }
            total += file.Size;
        }
        if (total > MaxTotalBytes)
        {
            return "补丁体积异常（超过 512 MB）";
        }

        foreach (var removed in manifest.Removed)
        {
            if (!IsSafeRelativePath(removed))
            {
                return $"补丁里有不允许的删除路径：{removed}";
            }
        }

        return null;
    }

    /// <summary>
    /// 路径白名单：必须是相对路径、不含 ..、盘符、根斜杠，扩展名在允许集合里。
    /// </summary>
    public static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\0'))
        {
            return false;
        }
        // 绝对路径 / 盘符 / UNC：都不允许出现在清单里
        if (relativePath[0] == '/' || relativePath[0] == '\\')
        {
            return false;
        }
        if (relativePath.Length >= 2 && relativePath[1] == ':')
        {
            return false;
        }

        var parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p == ".." || p == "."))
        {
            return false;
        }

        return AllowedExtensions.Contains(Path.GetExtension(relativePath));
    }

    /// <summary>
    /// 把清单里的相对路径解析成 root 下的绝对路径；越界或不合规返回 null。
    /// 这是防路径穿越的最后一道，别绕过它直接用 Path.Combine。
    /// </summary>
    public static string? ResolveUnder(string root, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            return null;
        }
        try
        {
            var rootFull = Path.GetFullPath(root)
                               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(rootFull, relativePath.Replace('\\', '/')));
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return full.StartsWith(rootFull + Path.DirectorySeparatorChar, comparison) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException
                                      or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>文件 SHA-256，小写十六进制。</summary>
    public static string Sha256OfFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>比较 "1.9" 与 "1.10"：小于 0 表示 a 更旧。</summary>
    public static int CompareVersion(string a, string b)
    {
        if (!Version.TryParse(Normalize(a), out var va) || !Version.TryParse(Normalize(b), out var vb))
        {
            return string.CompareOrdinal(a, b);
        }
        return va.CompareTo(vb);
    }

    /// <summary>
    /// "1.21" 这种两段式在 Version 里是 1.21（Build=-1），和 "1.21.0" 比会被当成更小。
    /// 补齐成四段再比，避免 "1.21" &lt; "1.21.0" 这种反直觉结果。
    /// </summary>
    private static string Normalize(string version)
    {
        var parts = version.Split('.');
        return parts.Length switch
        {
            1 => version + ".0.0.0",
            2 => version + ".0.0",
            3 => version + ".0",
            _ => version
        };
    }
}
