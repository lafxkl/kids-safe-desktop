using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 反馈上报实现（共享，Windows / Android 通用）。
///
/// 三个原则：
/// 1) **默认打官网**：数据要「同步到官网」，就不能跟着家长自建的服务器走——
///    那台服务器开发者看不到。地址固定为 https://kids.siguangblog.cc，
///    需要指向别处（自建/联调）时用偏好项 feedback.base_url 覆盖。
/// 2) **断网不丢**：提交失败先落到本机队列（AppDataDirectory/feedback_queue.json），
///    下次打开家长面板或下次提交时自动补传，用户不用重填一遍。
/// 3) **不猜身份**：只带平台和版本号，不带设备号、不带昵称、不带账号。
/// </summary>
public sealed class FeedbackService : IFeedbackService
{
    /// <summary>官网地址（与 nginx 反代的 /api/ 同域，避免跨域与证书问题）。</summary>
    public const string OfficialBase = "https://kids.siguangblog.cc";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<FeedbackDraft> _queue = new();
    private bool _loaded;

    public int PendingCount { get { lock (_queue) return _queue.Count; } }

    public string Status { get; private set; } = string.Empty;

    private static string QueuePath => Path.Combine(FileSystem.AppDataDirectory, "feedback_queue.json");

    private static string BaseUrl
    {
        get
        {
            var custom = Preferences.Get("feedback.base_url", string.Empty);
            var url = string.IsNullOrWhiteSpace(custom) ? OfficialBase : custom.Trim();
            return url.TrimEnd('/');
        }
    }

    private static string PlatformTag()
    {
#if ANDROID
        return "android";
#elif WINDOWS
        return "windows";
#elif IOS
        return "ios";
#elif MACCATALYST
        return "mac";
#else
        return DeviceInfo.Platform.ToString().ToLowerInvariant();
#endif
    }

    public async Task<int> FlushAsync()
    {
        await LoadAsync();
        if (PendingCount == 0) return 0;

        // 和 SubmitAsync 共用一把异步锁：否则「进页面补传」和「正在提交」会同时发同一条，
        // 服务端虽然幂等不会重复计数，但本机队列会被两边各删一次，容易乱。
        await _gate.WaitAsync();
        try
        {
            return await FlushCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<int> FlushCoreAsync()
    {
        List<FeedbackDraft> snapshot;
        lock (_queue) snapshot = _queue.ToList();

        var sent = 0;
        foreach (var item in snapshot)
        {
            var (ok, retry) = await PostAsync(item);
            if (ok)
            {
                lock (_queue) _queue.RemoveAll(x => x.Id == item.Id);
                sent++;
            }
            else if (!retry)
            {
                // 服务端明确拒绝（内容为空 / 字段不合法）→ 再重试一万次也不会成功，丢掉
                lock (_queue) _queue.RemoveAll(x => x.Id == item.Id);
                AppLog.W("反馈", $"队列里有一条被服务端拒绝，已丢弃：{item.Kind}");
            }
            else
            {
                break;   // 还是断网，后面的不用试了
            }
        }

        if (sent > 0)
        {
            await SaveAsync();
            Status = $"之前攒下的 {sent} 条反馈已补传 ✓";
            AppLog.I("反馈", Status);
        }
        return sent;
    }

    public async Task<FeedbackResult> SubmitAsync(FeedbackDraft draft)
    {
        await LoadAsync();

        draft.Platform ??= PlatformTag();
        draft.AppVersion ??= AppInfo.Current.VersionString;
        draft.Channel ??= "parent-dashboard";

        // 异步等锁：提交按钮在 UI 线程上，Wait() 会在另一条正在发（最长 12 秒）时把界面卡住
        await _gate.WaitAsync();
        try
        {
            var (ok, retry) = await PostAsync(draft);
            if (ok)
            {
                Status = "已提交 ✓ 谢谢你的反馈";
                AppLog.I("反馈", $"提交成功：{draft.Kind}");
                return new FeedbackResult(true, false, Status);
            }

            if (retry)
            {
                lock (_queue) _queue.Add(draft);
                await SaveAsync();
                Status = "网络不太好，已存在本机，联网后自动补传";
                AppLog.W("反馈", $"提交失败，已入队（{draft.Kind}）");
                return new FeedbackResult(true, true, Status);
            }

            Status = _lastError.Length > 0 ? _lastError : "提交失败，请稍后再试";
            return new FeedbackResult(false, false, Status);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string _lastError = string.Empty;

    /// <summary>
    /// 真正发一次。返回 (成功, 是否值得重试)。
    /// 「值得重试」= 断网 / 超时 / 5xx / 429；4xx 的其它情况说明数据本身有问题。
    /// </summary>
    private async Task<(bool ok, bool retry)> PostAsync(FeedbackDraft draft)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/feedback/submit")
            {
                Content = JsonContent.Create(draft, options: JsonOpts)
            };
            // 有个明确的 UA，排障时能在服务器日志里认出是客户端而不是浏览器
            req.Headers.UserAgent.ParseAdd($"KidsSafeDesktop/{AppInfo.Current.VersionString} ({draft.Platform})");

            using var resp = await _http.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                _lastError = string.Empty;
                return (true, false);
            }

            if (resp.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)resp.StatusCode >= 500)
            {
                _lastError = "服务器忙，稍后再试";
                return (false, true);
            }

            // 4xx：把服务端给的原因读出来，比「提交失败」有用得多
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<SubmitResp>(JsonOpts);
                _lastError = string.IsNullOrWhiteSpace(err?.error) ? "内容不符合要求" : err!.error!;
            }
            catch
            {
                _lastError = $"提交失败（HTTP {(int)resp.StatusCode}）";
            }
            return (false, false);
        }
        catch (Exception ex)
        {
            // 断网、DNS、证书、超时都走这里：全部按「可重试」处理
            _lastError = "网络不太好";
            AppLog.W("反馈", "提交异常：" + ex.Message);
            return (false, true);
        }
    }

    private async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!File.Exists(QueuePath)) return;
            var json = await File.ReadAllTextAsync(QueuePath);
            var list = JsonSerializer.Deserialize<List<FeedbackDraft>>(json, JsonOpts);
            if (list is not null)
            {
                lock (_queue)
                {
                    _queue.Clear();
                    _queue.AddRange(list);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.W("反馈", "读取本地队列失败：" + ex.Message);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            List<FeedbackDraft> snapshot;
            lock (_queue) snapshot = _queue.ToList();

            if (snapshot.Count == 0)
            {
                if (File.Exists(QueuePath)) File.Delete(QueuePath);
                return;
            }

            var json = JsonSerializer.Serialize(snapshot, JsonOpts);
            await File.WriteAllTextAsync(QueuePath, json);
        }
        catch (Exception ex)
        {
            AppLog.W("反馈", "保存本地队列失败：" + ex.Message);
        }
    }

    private sealed record SubmitResp(bool ok, bool duplicate, string? error);
}
