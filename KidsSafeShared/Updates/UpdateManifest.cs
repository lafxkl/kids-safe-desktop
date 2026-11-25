using System.Text.Json;
using System.Text.Json.Serialization;

namespace KidsSafeDesktop.Updates;

/// <summary>
/// 一份增量更新清单。字段含义与校验规则见 Docs/增量更新方案.md 第三节。
///
/// 注意：这个类只负责「解析」，不做任何信任判断 —— 调用方必须先用
/// <see cref="UpdateVerifier.Verify"/> 验签，再用 <see cref="UpdateVerifier.Validate"/>
/// 校验字段，才允许使用里面的路径和哈希。
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>格式版本。客户端只认自己认识的那一版，不认识的宁可整包。</summary>
    [JsonPropertyName("schema")]
    public int Schema { get; set; }

    /// <summary>windows / android。装错平台的 dll 会直接崩，必须先比这个字段。</summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    /// <summary>目标版本，如 "1.21"。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>单调递增的构建号；必须大于本机，否则视为降级/重放。</summary>
    [JsonPropertyName("versionCode")]
    public int VersionCode { get; set; }

    /// <summary>宿主（壳工程 + 运行时）的最低版本。低于它说明接口不兼容，只能整包。</summary>
    [JsonPropertyName("minHost")]
    public string MinHost { get; set; } = string.Empty;

    /// <summary>这份补丁可以从哪些版本升级上来。不在列表里的版本必须整包。</summary>
    [JsonPropertyName("base")]
    public string[] Base { get; set; } = Array.Empty<string>();

    [JsonPropertyName("issuedAt")]
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>过期时间。旧清单被重放时，迟早会过期失效。</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>这一版改了什么，显示给家长看。</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("files")]
    public List<UpdateFile> Files { get; set; } = new();

    /// <summary>要从安装目录里删掉的文件（改名/合并后留下的旧 dll）。</summary>
    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = new();

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // 清单是我们自己签的，但解析时仍然别让未知字段炸掉：
        // 将来加字段（比如 delta 信息）时，老客户端要能继续读。
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static UpdateManifest? Parse(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            return JsonSerializer.Deserialize<UpdateManifest>(utf8Json, ParseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>清单里的一个文件。</summary>
public sealed class UpdateFile
{
    /// <summary>相对安装目录的路径，如 "KidsSafeShared.dll"。</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>小写十六进制 SHA-256。</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
