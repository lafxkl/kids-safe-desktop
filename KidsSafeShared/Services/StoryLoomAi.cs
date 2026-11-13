using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 可选的「大模型故事引擎」：走 OpenAI 兼容的 chat/completions 接口。
///
/// 为什么默认关闭、还要家长 PIN：
/// 1. 打开它意味着把孩子说的话发到第三方服务器，这与本项目「儿童数据不出本机」的基线冲突，
///    必须由家长明确知情后再打开；
/// 2. 没有配置时随时回退到内置模板，孩子端不会出现「转圈等失败」的体验；
/// 3. 失败一律降级，不弹技术错误给孩子看。
///
/// 兼容对象：华为云 MaaS、DeepSeek、以及任何 OpenAI 兼容网关。
/// </summary>
public static class StoryLoomAi
{
    private const string KeyEnabled = "loom_ai_enabled";
    private const string KeyBase = "loom_ai_base";
    private const string KeyModel = "loom_ai_model";
    private const string KeyApiKey = "loom_ai_key";      // 优先 SecureStorage
    private const string KeyApiKeyFallback = "loom_ai_key_plain";

    public const string DefaultBase = "https://api.modelarts-maas.com/v2";
    public const string DefaultModel = "deepseek-v4.1-flash";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(95) };

    public static bool Enabled => Preferences.Get(KeyEnabled, false);
    public static string BaseUrl => Preferences.Get(KeyBase, DefaultBase);
    public static string Model => Preferences.Get(KeyModel, DefaultModel);

    private static string? _cachedKey;

    /// <summary>取 API Key。第一次从 SecureStorage 读，读不到再看明文兜底。</summary>
    public static async Task<string> GetKeyAsync()
    {
        if (_cachedKey is not null) return _cachedKey;
        try
        {
            var k = await SecureStorage.GetAsync(KeyApiKey);
            if (!string.IsNullOrWhiteSpace(k)) { _cachedKey = k; return k; }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoomAI] SecureStorage 读取失败，改用兜底：{ex.Message}");
        }
        _cachedKey = Preferences.Get(KeyApiKeyFallback, "");
        return _cachedKey;
    }

    public static async Task SaveAsync(bool enabled, string baseUrl, string model, string key)
    {
        Preferences.Set(KeyEnabled, enabled);
        Preferences.Set(KeyBase, string.IsNullOrWhiteSpace(baseUrl) ? DefaultBase : baseUrl.Trim());
        Preferences.Set(KeyModel, string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim());

        _cachedKey = key?.Trim() ?? "";
        try
        {
            if (string.IsNullOrEmpty(_cachedKey)) SecureStorage.Remove(KeyApiKey);
            else await SecureStorage.SetAsync(KeyApiKey, _cachedKey);
            Preferences.Remove(KeyApiKeyFallback);
        }
        catch (Exception ex)
        {
            // Android 某些机型没有可用的 keystore，退化为明文（仍在应用私有目录）
            System.Diagnostics.Debug.WriteLine($"[LoomAI] SecureStorage 写入失败，改用兜底：{ex.Message}");
            try { Preferences.Set(KeyApiKeyFallback, _cachedKey); } catch { /* 忽略 */ }
        }
    }

    public static async Task<bool> ReadyAsync()
        => Enabled
           && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")
           && !string.IsNullOrWhiteSpace(Model)
           && !string.IsNullOrWhiteSpace(await GetKeyAsync());

    /// <summary>
    /// 生成故事。失败返回 Error，由调用方回退模板——绝不把异常抛给 UI 线程。
    /// </summary>
    public static async Task<(LoomStory? Story, string? Error)> GenerateAsync(
        LoomIdea idea, string? wish, CancellationToken ct = default)
    {
        if (!await ReadyAsync()) return (null, "未启用或未配置");

        var key = await GetKeyAsync();
        var messages = StoryLoomEngine.BuildAiMessages(idea, wish);

        string? lastError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var content = await ChatAsync(messages, key, temperature: 0.65, maxTokens: 1800, ct);
                var story = StoryLoomEngine.ParseAiJson(content);
                story.KeywordCount = idea.Requested.Count;
                return (story, null);
            }
            catch (OperationCanceledException)
            {
                return (null, "已取消");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                // 让模型看到自己写错在哪，再试一次
                messages.Add(("assistant", "(上一次输出格式不符合要求)"));
                messages.Add(("user", $"请修正输出格式：{ex.Message}。保持孩子原来的心愿和同一条因果线，重新输出完整 JSON。"));
            }
        }

        return (null, lastError ?? "生成失败");
    }

    /// <summary>连接测试：只让模型回两个字，验证地址 / Key / 模型名是否都对。</summary>
    public static async Task<(bool Ok, string Message)> TestAsync(CancellationToken ct = default)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http"))
            return (false, "API 地址要写成 http(s):// 开头");
        if (string.IsNullOrWhiteSpace(Model))
            return (false, "请填写模型名称");

        var key = await GetKeyAsync();
        if (string.IsNullOrWhiteSpace(key))
            return (false, "请填写 API Key");

        try
        {
            var text = await ChatAsync([("user", "只回复两个字：你好")], key, 0, 16, ct);
            var shortText = text.Trim().Replace("\n", " ");
            if (shortText.Length > 24) shortText = shortText[..24];
            return (true, $"连接成功 ✅ 模型回复：{shortText}");
        }
        catch (Exception ex)
        {
            return (false, "连接失败 ❌ " + ex.Message);
        }
    }

    // ── HTTP ────────────────────────────────────────────────

    /// <summary>把用户填的 Base URL 归一化成 chat/completions 的完整地址。</summary>
    public static string ChatEndpoint(string baseUrl)
    {
        var b = (baseUrl ?? "").Trim().TrimEnd('/');
        if (b.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return b;
        if (System.Text.RegularExpressions.Regex.IsMatch(b, @"/v\d+$")) return b + "/chat/completions";
        return b + "/v1/chat/completions";
    }

    private static async Task<string> ChatAsync(
        List<(string Role, string Content)> messages, string key,
        double temperature, int maxTokens, CancellationToken ct)
    {
        var payload = new
        {
            model = Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            temperature,
            max_tokens = maxTokens
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint(BaseUrl));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            var brief = body.Length > 200 ? body[..200] : body;
            throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {brief}");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("模型返回了空内容");
        return content;
    }
}
