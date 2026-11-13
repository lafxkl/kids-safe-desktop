using System.Net.Http.Json;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 家长端服务器同步实现（共享，全平台通用）。
/// 通过 HTTP 与 KidsSafeServer 通信；断网或未配置时静默失败，不影响本地使用。
/// </summary>
public class ServerSyncService : IServerSyncService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ISettingsService _settings;

    public string Status { get; private set; } = string.Empty;

    /// <summary>登录时使用的账号名（配对码/老密码登录时为空）。</summary>
    public string? DeviceAccount { get; private set; }

    /// <summary>当前令牌是不是「儿童设备」令牌 —— 这种令牌只能拉配置，不能改。</summary>
    public bool IsDeviceToken { get; private set; }

    public bool IsConfigured
    {
        get
        {
            var c = _settings.Current;
            return c.ServerEnabled && !string.IsNullOrWhiteSpace(c.ServerUrl);
        }
    }

    public ServerSyncService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<bool> LoginAsync(string? username, string secret)
    {
        try
        {
            var baseUrl = Normalize(_settings.Current.ServerUrl);
            if (baseUrl is null) { Status = "请先填写服务器地址"; return false; }
            if (string.IsNullOrWhiteSpace(secret)) { Status = "请填写密码或配对码"; return false; }

            // 填了账号 → 账号密码登录（可写）；只填密码 → 老密码或 6 位配对码（只读）
            object body = string.IsNullOrWhiteSpace(username)
                ? new { pin = secret.Trim() }
                : new { username = username!.Trim(), password = secret };

            var resp = await _http.PostAsJsonAsync($"{baseUrl}/api/auth/login", body);
            if (!resp.IsSuccessStatusCode)
            {
                // 服务端会说明原因（密码错 / 配对码过期 / 尝试过多），尽量原样展示
                var err = await SafeErrorAsync(resp);
                Status = string.IsNullOrWhiteSpace(err) ? "密码错误" : err!;
                return false;
            }
            var d = await resp.Content.ReadFromJsonAsync<LoginResp>();
            if (d is null || !d.ok || string.IsNullOrEmpty(d.token))
            {
                Status = "登录失败";
                return false;
            }
            _settings.Current.ServerToken = d.token;
            await _settings.SaveAsync();

            DeviceAccount = d.user?.username;
            IsDeviceToken = d.user?.device == true;
            Status = IsDeviceToken ? "已配对 ✓（只读同步）" : "已登录 ✓";
            return true;
        }
        catch (Exception ex)
        {
            Status = "连接失败：" + ex.Message;
            return false;
        }
    }

    /// <summary>服务端 401 的 JSON 里带 error 字段，读出来给界面用；失败就返回 null。</summary>
    private static async Task<string?> SafeErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<LoginResp>();
            return body?.error;
        }
        catch { return null; }
    }

    public async Task<bool> PullAsync()
    {
        if (!IsConfigured) { Status = "未启用服务器"; return false; }
        var token = _settings.Current.ServerToken;
        if (string.IsNullOrEmpty(token)) { Status = "请先登录"; return false; }

        try
        {
            var baseUrl = Normalize(_settings.Current.ServerUrl)!;
            var url = $"{baseUrl}/api/settings?token={Uri.EscapeDataString(token)}";
            using var resp = await _http.GetAsync(url);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Status = "令牌失效，请重新登录";
                return false;
            }
            if (!resp.IsSuccessStatusCode) { Status = "拉取失败（" + (int)resp.StatusCode + "）"; return false; }

            var p = await resp.Content.ReadFromJsonAsync<RemoteProfile>();
            if (p is null) { Status = "数据为空"; return false; }

            Apply(p);
            Status = "已同步 ✓ " + DateTime.Now.ToString("HH:mm");
            return true;
        }
        catch (Exception ex)
        {
            Status = "同步失败：" + ex.Message;
            return false;
        }
    }

    private void Apply(RemoteProfile p)
    {
        var c = _settings.Current;
        c.DailyLimitMinutes = p.DailyLimitMinutes;
        c.GamesEnabled = p.GamesEnabled;
        c.EnabledGames = p.EnabledGames ?? new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Nickname)) c.Nickname = p.Nickname;
        if (!string.IsNullOrWhiteSpace(p.WeatherCity)) c.WeatherCity = p.WeatherCity;
        if (!string.IsNullOrWhiteSpace(p.XimalayaDefaultCategory)) c.XimalayaDefaultCategory = p.XimalayaDefaultCategory;
        if (!string.IsNullOrWhiteSpace(p.ThemeId)) c.ThemeId = p.ThemeId;

        // 在线内容由家长在服务器后台统一控制：关掉后儿童端只用离线内置内容
        c.OnlineContentEnabled = p.OnlineContentEnabled;

        c.AllowedApps = p.AllowedApps
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => new AppItem
            {
                Name = a.Name,
                PackageName = a.PackageName,
                LaunchTarget = a.LaunchTarget,
                Emoji = a.Emoji ?? "⭐",
                AccentColor = a.AccentColor ?? "#4C8BF5"
            })
            .ToList();

        _ = _settings.SaveAsync();
        ThemeService.Instance.SetTheme(c.ThemeId);
    }

    /// <summary>规范化服务器地址：补全协议、去掉尾部斜杠。</summary>
    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw!.Trim();
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "http://" + s;
        s = s.TrimEnd('/');
        return s;
    }

    private class LoginResp
    {
        public bool ok { get; set; }
        public string? token { get; set; }
        public string? error { get; set; }
        public LoginUser? user { get; set; }
    }

    private class LoginUser
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? username { get; set; }
        public bool device { get; set; }
    }
}
