using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KidsSafeServer;

/// <summary>MXNZP 故事大全接入配置（仅服务端持有，绝不下发到儿童客户端）。</summary>
public class MxnzpOptions
{
    /// <summary>APP_ID / USER_ID。</summary>
    public string AppId { get; set; } = string.Empty;
    /// <summary>APP_SECRET。</summary>
    public string AppSecret { get; set; } = string.Empty;
    /// <summary>是否已完成配置。未配置时故事接口直接返回不可用，不向第三方发请求。</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
}

/// <summary>mxnzp 原始返回结构（仅反序列化用，内部字段命名与官方一致）。</summary>
public sealed class MxnzpType
{
    public string name { get; set; } = string.Empty;
    public int type_id { get; set; }
}

public sealed class MxnzpSummary
{
    public long storyId { get; set; }
    public string title { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public int length { get; set; }
    public string readTime { get; set; } = string.Empty;
    public string shortDesc { get; set; } = string.Empty;
}

public sealed class MxnzpDetailData
{
    public long storyId { get; set; }
    public string title { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public int length { get; set; }
    public string readTime { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

/// <summary>
/// 故事大全（mxnzp.com）服务端代理。
/// <list type="bullet">
///   <item>密钥仅在服务端保存，客户端永远不直接接触第三方，也不持 secret。</item>
///   <item>内置内存缓存（默认 60 分钟），避免重复请求、规避第三方 QPS 限制。</item>
///   <item>串行 + 最小间隔限流，适配「普通会员 QPS=1」的配额，避免触发频率限制。</item>
/// </list>
/// </summary>
public sealed class MxnzpStoryService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://www.mxnzp.com"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly MxnzpOptions _opt;
    private readonly ILogger<MxnzpStoryService> _log;

    // QPS=1 限流：串行 + 最小间隔
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastCallUtc = DateTime.MinValue;
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(1100);

    // 简单内存缓存
    private readonly Dictionary<string, (object Value, DateTime ExpiryUtc)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(60);
    private readonly object _lock = new();

    public MxnzpStoryService(IOptions<MxnzpOptions> opt, ILogger<MxnzpStoryService> log)
    {
        _opt = opt.Value;
        _log = log;
    }

    private async Task<T?> GetAsync<T>(string relative, string cacheKey, CancellationToken ct = default) where T : class
    {
        // 1) 命中缓存直接返回
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var e) && e.ExpiryUtc > DateTime.UtcNow)
                return (T)e.Value;
        }

        if (!_opt.Enabled)
            return null;

        // 2) 串行 + 间隔限流，避免触发第三方 QPS 频率限制
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var wait = MinGap - (DateTime.UtcNow - _lastCallUtc);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct).ConfigureAwait(false);
            _lastCallUtc = DateTime.UtcNow;

            var sep = relative.Contains('?') ? '&' : '?';
            var url = $"/api/story/{relative}{sep}app_id={Uri.EscapeDataString(_opt.AppId)}&app_secret={Uri.EscapeDataString(_opt.AppSecret)}";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("mxnzp 上游返回 HTTP {Status}", resp.StatusCode);
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;
            if (!root.TryGetProperty("code", out var codeEl) || codeEl.GetInt32() != 1)
            {
                var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : "(无错误信息)";
                _log.LogWarning("mxnzp 业务错误：{Msg}", msg);
                return null;
            }

            if (!root.TryGetProperty("data", out var dataEl))
                return null;

            var value = JsonSerializer.Deserialize<T>(dataEl.GetRawText(), JsonOpts);
            if (value is not null)
            {
                lock (_lock)
                {
                    _cache[cacheKey] = (value, DateTime.UtcNow + _ttl);
                }
            }

            return value;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "mxnzp 上游调用异常");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<List<MxnzpType>?> GetTypesAsync(CancellationToken ct = default)
        => GetAsync<List<MxnzpType>>("types", "mxnzp:types", ct);

    public Task<List<MxnzpSummary>?> GetListAsync(int typeId, int page, CancellationToken ct = default)
        => GetAsync<List<MxnzpSummary>>($"list?type_id={typeId}&page={page}", $"mxnzp:list:{typeId}:{page}", ct);

    public Task<MxnzpDetailData?> GetDetailAsync(long storyId, CancellationToken ct = default)
        => GetAsync<MxnzpDetailData>($"details?story_id={storyId}", $"mxnzp:detail:{storyId}", ct);

    public Task<List<MxnzpSummary>?> SearchAsync(string keyword, int page, CancellationToken ct = default)
        => GetAsync<List<MxnzpSummary>>($"search?keyword={Uri.EscapeDataString(keyword)}&page={page}", $"mxnzp:search:{keyword}:{page}", ct);
}
