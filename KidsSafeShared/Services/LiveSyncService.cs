using System.Net;
using System.Net.Http.Json;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 基于长轮询的「实时下发」。
///
/// 工作方式：
///   循环请求 GET {ServerUrl}/api/watch?token=xxx&since=版本号
///   · 服务器没有变化 → 挂起最多 25 秒后返回 changed=false，客户端立刻再发起（这就是长轮询）；
///   · 家长刚保存过配置 → 立刻返回 changed=true，客户端调用 PullAsync() 拉取并应用；
///   · 断网/服务器没开 → 指数退避重连（2s→4s→…→30s），不打扰用户。
///
/// 一次请求占用一个连接即可，不需要 WebSocket，也不需要厂商推送 SDK。
/// </summary>
public class LiveSyncService : ILiveSyncService
{
    // 超时必须大于服务端挂起的 25 秒，否则会在正常情况下误判超时
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(40) };

    private readonly ISettingsService _settings;
    private readonly IServerSyncService _sync;

    private CancellationTokenSource? _cts;
    private long _version;
    private string _status = "未启动";
    private int _updateCount;

    public LiveSyncService(ISettingsService settings, IServerSyncService sync)
    {
        _settings = settings;
        _sync = sync;
    }

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public string Status => _status;

    public event EventHandler<string>? ConfigUpdated;

    public void Start()
    {
        if (!IsConfigured(out var reason))
        {
            _status = reason;
            return;
        }
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => LoopAsync(ct));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        _cts = null;
        _status = "已停止";
    }

    private bool IsConfigured(out string reason)
    {
        reason = "未配置服务器";
        var c = _settings.Current;
        if (!c.ServerEnabled || string.IsNullOrWhiteSpace(c.ServerUrl)) return false;
        if (string.IsNullOrWhiteSpace(c.ServerToken)) { reason = "未登录家长服务器"; return false; }
        return true;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(2);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!IsConfigured(out var reason))
                {
                    _status = reason;
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                    continue;
                }

                var baseUrl = Normalize(_settings.Current.ServerUrl)!;
                var token = _settings.Current.ServerToken!;
                var url = $"{baseUrl}/api/watch?token={Uri.EscapeDataString(token)}&since={_version}";

                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _status = "令牌失效，请在家长面板重新登录";
                    await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                    throw new Exception("HTTP " + (int)resp.StatusCode);

                // 服务端在「请求已被取消」时用 204 收尾（见 /api/watch），没有 JSON 可读；
                // 当作「无变化、立刻重连」，不要走异常路径显示成「重连中…」
                if (resp.StatusCode == HttpStatusCode.NoContent) continue;

                var d = await resp.Content.ReadFromJsonAsync<WatchResp>(ct).ConfigureAwait(false);
                if (d is null) throw new Exception("响应为空");

                backoff = TimeSpan.FromSeconds(2);

                if (d.changed && d.version != _version)
                {
                    _version = d.version;
                    _updateCount++;
                    var ok = await _sync.PullAsync().ConfigureAwait(false);
                    _status = ok
                        ? $"已连接 · 已更新 {_updateCount} 次（{DateTime.Now:HH:mm:ss}）"
                        : "已连接 · 拉取失败：" + _sync.Status;

                    if (ok) ConfigUpdated?.Invoke(this, "家长端的配置已更新并生效");
                }
                else
                {
                    _status = _updateCount == 0 ? "已连接" : $"已连接 · 已更新 {_updateCount} 次";
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _status = "重连中…（" + Short(ex.Message) + "）";
                // 长轮询失败会自动退避重连，但对家长来说就是「改了配置孩子那边没反应」。
                // 状态栏只显示最后一次错误，历史趋势要靠日志。
                KidsSafeDesktop.Helpers.AppLog.W("实时下发", "长轮询失败，退避 " + backoff.TotalSeconds.ToString("0") + "s 后重试：" + ex.Message);
                try { await Task.Delay(backoff, ct).ConfigureAwait(false); }
                catch { break; }
                backoff = TimeSpan.FromSeconds(Math.Min(30, backoff.TotalSeconds * 2));
            }
        }
    }

    private static string Short(string s) => s.Length <= 40 ? s : s[..40] + "…";

    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw!.Trim();
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "http://" + s;
        return s.TrimEnd('/');
    }

    private class WatchResp
    {
        public bool ok { get; set; }
        public long version { get; set; }
        public bool changed { get; set; }
    }
}
