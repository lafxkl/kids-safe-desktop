using System.Collections.ObjectModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>家长后台里「某个游戏是否开启」的开关项。</summary>
public class GameToggle : BaseViewModel
{
    public GameInfo Info { get; }
    private bool _isOn;
    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }
    public GameToggle(GameInfo info, bool isOn)
    {
        Info = info;
        _isOn = isOn;
    }
}

public class CatalogEntry : BaseViewModel
{
    public AppItem App { get; }
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }
    public CatalogEntry(AppItem app, bool selected)
    {
        App = app;
        _selected = selected;
    }
}

/// <summary>
/// 家长后台：管理密码、每日时长、允许应用与网站。
/// </summary>
public class ParentDashboardViewModel : BaseViewModel
{
    private readonly ISettingsService _settings;
    private int _dailyLimitMinutes = 60;
    private string _newPin = string.Empty;
    private string _status = string.Empty;
    private string _newSite = string.Empty;
    private bool _gamesEnabled = true;

    // —— 护眼模式 ——
    private bool _eyeCareEnabled;
    private double _eyeCareIntensity = 0.22;
    private bool _eyeCareAutoEnabled;
    private int _eyeCareAutoStartHour = 19;
    private int _eyeCareAutoEndHour = 7;

    // —— 家长端服务器同步 ——
    private string _serverUrl = string.Empty;
    private bool _serverEnabled;
    private string _serverPin = string.Empty;
    private string _serverUser = string.Empty;
    private string _serverStatus = string.Empty;
    private string _liveStatus = string.Empty;
    private ILiveSyncService? _live;

    // —— 在线内容（宝宝听/宝宝看/亲子阅/故事屋/电子课本是否连服务器取补充内容）——
    private bool _onlineContentEnabled;

    public ObservableCollection<CatalogEntry> Catalog { get; } = new();
    public ObservableCollection<string> AllowedSites { get; } = new();
    public ObservableCollection<GameToggle> Games { get; } = new();

    public bool GamesEnabled
    {
        get => _gamesEnabled;
        set
        {
            if (SetProperty(ref _gamesEnabled, value))
                _settings.Current.GamesEnabled = value;
        }
    }

    /// <summary>
    /// 锁定模式（安卓「屏幕固定」）：开启后屏蔽通知栏、Home 手势与多任务键，
    /// 是本应用唯一能阻挡系统级退出手势的官方手段。
    /// 代价：固定期间无法启动家长放行的外部应用，需要先从家长入口退出。
    /// 直接存 Preferences（不落业务配置），返回主页时由 MainActivity 生效。
    /// </summary>
    public bool KioskLockEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Get("kiosk_lock_task", false);
        set
        {
            if (Microsoft.Maui.Storage.Preferences.Get("kiosk_lock_task", false) == value) return;
            Microsoft.Maui.Storage.Preferences.Set("kiosk_lock_task", value);
            OnPropertyChanged();
            Status = value
                ? "已开启锁定模式：返回主页后生效（首次会弹系统「固定」确认框）"
                : "已关闭锁定模式：返回主页后生效";
        }
    }

    /// <summary>
    /// 防退出（自动回前台）：被底部上滑 / 多任务切走时，立刻把自己拉回最前。
    /// **默认开启**——这是普通应用（无 Root、无设备所有者权限）对抗系统级 Home 手势最有效的手段。
    /// 直接存 Preferences（Preferences 键 <c>anti_exit_guard</c>），由 MainActivity 在 OnStop 时读取。
    /// </summary>
    public bool AntiExitEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Get("anti_exit_guard", true);
        set
        {
            if (Microsoft.Maui.Storage.Preferences.Get("anti_exit_guard", true) == value) return;
            Microsoft.Maui.Storage.Preferences.Set("anti_exit_guard", value);
            OnPropertyChanged();
            Status = value
                ? "已开启防退出：被切到后台会立刻自动回到儿童桌面"
                : "已关闭防退出：可以正常切出儿童桌面";
        }
    }

    public int DailyLimitMinutes    {
        get => _dailyLimitMinutes;
        set { if (SetProperty(ref _dailyLimitMinutes, value)) _settings.Current.DailyLimitMinutes = value; }
    }

    public string NewPin
    {
        get => _newPin;
        set => SetProperty(ref _newPin, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string NewSite
    {
        get => _newSite;
        set => SetProperty(ref _newSite, value);
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set { if (SetProperty(ref _serverUrl, value)) _settings.Current.ServerUrl = value; }
    }

    public bool ServerEnabled
    {
        get => _serverEnabled;
        set { if (SetProperty(ref _serverEnabled, value)) _settings.Current.ServerEnabled = value; }
    }

    /// <summary>
    /// 在线内容总开关：开启后宝宝听 / 宝宝看 / 亲子阅 / 故事屋 / 电子课本会从家长自建服务器
    /// 拉取经人工审核的补充内容；关闭则只用离线内置内容。连服务器同步时以服务器下发值为准。
    /// </summary>
    public bool OnlineContentEnabled
    {
        get => _onlineContentEnabled;
        set
        {
            if (!SetProperty(ref _onlineContentEnabled, value)) return;
            _settings.Current.OnlineContentEnabled = value;
            Status = value
                ? "已开启在线内容：需要填好服务器地址，内容只来自这台服务器"
                : "已关闭在线内容：宝宝听/宝宝看/亲子阅/电子课本只用本机内置内容";
        }
    }

    public string ServerPin
    {
        get => _serverPin;
        set => SetProperty(ref _serverPin, value);
    }

    /// <summary>家长账号（可选）。为空时 ServerPin 里填密码或 6 位配对码。</summary>
    public string ServerUser
    {
        get => _serverUser;
        set => SetProperty(ref _serverUser, value);
    }

    public string ServerStatus
    {
        get => _serverStatus;
        set => SetProperty(ref _serverStatus, value);
    }

    /// <summary>实时下发通道的状态（长轮询）。家长改完配置后会显示「已更新 N 次」。</summary>
    public string LiveStatus
    {
        get => _liveStatus;
        set => SetProperty(ref _liveStatus, value);
    }

    /// <summary>保存成功（由页面处理导航/刷新）</summary>
    public event EventHandler? Saved;

    // ===== 护眼模式 =====

    public bool EyeCareEnabled
    {
        get => _eyeCareEnabled;
        set
        {
            if (SetProperty(ref _eyeCareEnabled, value))
            {
                _settings.Current.EyeCareEnabled = value;
                OnPropertyChanged(nameof(EyeCareStatusText));
            }
        }
    }

    public double EyeCareIntensity
    {
        get => _eyeCareIntensity;
        set
        {
            if (SetProperty(ref _eyeCareIntensity, value))
            {
                _settings.Current.EyeCareIntensity = value;
                OnPropertyChanged(nameof(EyeCareIntensityText));
            }
        }
    }

    public bool EyeCareAutoEnabled
    {
        get => _eyeCareAutoEnabled;
        set
        {
            if (SetProperty(ref _eyeCareAutoEnabled, value))
            {
                _settings.Current.EyeCareAutoEnabled = value;
                OnPropertyChanged(nameof(EyeCareStatusText));
            }
        }
    }

    public int EyeCareAutoStartHour
    {
        get => _eyeCareAutoStartHour;
        set
        {
            if (SetProperty(ref _eyeCareAutoStartHour, value))
            {
                _settings.Current.EyeCareAutoStartHour = value;
                OnPropertyChanged(nameof(EyeCareStatusText));
            }
        }
    }

    public int EyeCareAutoEndHour
    {
        get => _eyeCareAutoEndHour;
        set
        {
            if (SetProperty(ref _eyeCareAutoEndHour, value))
            {
                _settings.Current.EyeCareAutoEndHour = value;
                OnPropertyChanged(nameof(EyeCareStatusText));
            }
        }
    }

    public string EyeCareIntensityText
    {
        get
        {
            var p = (int)Math.Round(_eyeCareIntensity * 100);
            var level = _eyeCareIntensity switch
            {
                < 0.15 => "（关闭级）",
                < 0.40 => "（柔和）",
                < 0.70 => "（标准）",
                _ => "（强力）"
            };
            return $"滤镜强度 {p}% {level}";
        }
    }

    public string EyeCareStatusText
    {
        get
        {
            var s = _settings.Current;
            var on = EyeCareRules.IsEffectivelyOn(s);
            if (s.EyeCareEnabled)
                return $"当前生效中 ✅（常开，{(int)Math.Round(EyeCareRules.ToOpacity(s.EyeCareIntensity) * 100)}% 遮罩）";
            if (s.EyeCareAutoEnabled)
                return $"定时生效中 ✅（{EyeCareRules.DescribeRange(s)}）";
            return on ? "当前生效中 ✅" : "当前未生效（到设定时段会自动开启）";
        }
    }

    /// <summary>家长请求退出儿童模式</summary>
    public event EventHandler? ExitRequested;

    /// <summary>应用版本展示文本（页脚显示）。</summary>
    public string VersionText => AppVersion.Display;

    public ParentDashboardViewModel()
    {
        _settings = ServiceProvider.Get<ISettingsService>();
        _dailyLimitMinutes = _settings.Current.DailyLimitMinutes;
        _gamesEnabled = _settings.Current.GamesEnabled;
        _serverUrl = _settings.Current.ServerUrl;
        _serverEnabled = _settings.Current.ServerEnabled;
        _onlineContentEnabled = _settings.Current.OnlineContentEnabled;
        _eyeCareEnabled = _settings.Current.EyeCareEnabled;
        _eyeCareIntensity = _settings.Current.EyeCareIntensity;
        _eyeCareAutoEnabled = _settings.Current.EyeCareAutoEnabled;
        _eyeCareAutoStartHour = _settings.Current.EyeCareAutoStartHour;
        _eyeCareAutoEndHour = _settings.Current.EyeCareAutoEndHour;
        foreach (var s in _settings.Current.AllowedSites)
            AllowedSites.Add(s);
        foreach (var g in GameCatalog.All)
            Games.Add(new GameToggle(g, _settings.Current.IsGameEnabled(g.Type)));
    }

    /// <summary>用家长密码登录家长端服务器（保存令牌）。</summary>
    public async Task LoginServerAsync()
    {
        var ok = await ServiceProvider.Get<IServerSyncService>().LoginAsync(ServerUser, ServerPin);
        ServerStatus = ServiceProvider.Get<IServerSyncService>().Status;
        ServerPin = string.Empty;
        if (ok)
        {
            await _settings.SaveAsync();
            RefreshLive();
        }
    }

    /// <summary>从家长端服务器拉取配置并合并到本地。</summary>
    public async Task SyncServerAsync()
    {
        var ok = await ServiceProvider.Get<IServerSyncService>().PullAsync();
        ServerStatus = ServiceProvider.Get<IServerSyncService>().Status;
        if (ok)
        {
            await _settings.SaveAsync();
            await RebindAsync();
        }
        RefreshLive();
    }

    /// <summary>启动/重启实时通道并刷新状态文字（登录、同步后调用）。</summary>
    private void RefreshLive()
    {
        try
        {
            _live ??= ServiceProvider.Get<ILiveSyncService>();
            _live.Start();
            LiveStatus = "实时通道：" + _live.Status;
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }

    /// <summary>页面出现时订阅实时通道事件（离开时 Detach，避免单例事件持有页面）。</summary>
    public void Attach()
    {
        try
        {
            _live ??= ServiceProvider.Get<ILiveSyncService>();
            _live.ConfigUpdated -= OnLiveConfigUpdated;
            _live.ConfigUpdated += OnLiveConfigUpdated;
            LiveStatus = "实时通道：" + _live.Status;
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }

    public void Detach()
    {
        if (_live is not null) _live.ConfigUpdated -= OnLiveConfigUpdated;
    }

    private void OnLiveConfigUpdated(object? sender, string message)
    {
        // 事件来自后台线程：切主线程刷新 UI
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                LiveStatus = "实时通道：" + (_live?.Status ?? message);
                Status = "☁️ " + message;
                await RebindAsync();
            }
            catch (Exception ex)
            {
                Helpers.NavFx.Log(ex);
            }
        });
    }

    /// <summary>拉取配置后，把本地 UI 字段刷新成服务器下发的配置。</summary>
    public async Task RebindAsync()
    {
        _dailyLimitMinutes = _settings.Current.DailyLimitMinutes;
        OnPropertyChanged(nameof(DailyLimitMinutes));
        _gamesEnabled = _settings.Current.GamesEnabled;
        OnPropertyChanged(nameof(GamesEnabled));
        AllowedSites.Clear();
        foreach (var s in _settings.Current.AllowedSites) AllowedSites.Add(s);
        Games.Clear();
        foreach (var g in GameCatalog.All)
            Games.Add(new GameToggle(g, _settings.Current.IsGameEnabled(g.Type)));
        _serverUrl = _settings.Current.ServerUrl;
        OnPropertyChanged(nameof(ServerUrl));
        _serverEnabled = _settings.Current.ServerEnabled;
        OnPropertyChanged(nameof(ServerEnabled));
        _onlineContentEnabled = _settings.Current.OnlineContentEnabled;
        OnPropertyChanged(nameof(OnlineContentEnabled));

        _eyeCareEnabled = _settings.Current.EyeCareEnabled;
        OnPropertyChanged(nameof(EyeCareEnabled));
        _eyeCareIntensity = _settings.Current.EyeCareIntensity;
        OnPropertyChanged(nameof(EyeCareIntensity));
        _eyeCareAutoEnabled = _settings.Current.EyeCareAutoEnabled;
        OnPropertyChanged(nameof(EyeCareAutoEnabled));
        _eyeCareAutoStartHour = _settings.Current.EyeCareAutoStartHour;
        OnPropertyChanged(nameof(EyeCareAutoStartHour));
        _eyeCareAutoEndHour = _settings.Current.EyeCareAutoEndHour;
        OnPropertyChanged(nameof(EyeCareAutoEndHour));
        OnPropertyChanged(nameof(EyeCareStatusText));
        OnPropertyChanged(nameof(EyeCareIntensityText));

        await LoadCatalogAsync();
    }

    public async Task LoadCatalogAsync()
    {
        Catalog.Clear();
        var catalogSvc = ServiceProvider.Get<IAppCatalogService>();
        var installed = await catalogSvc.GetInstalledAppsAsync();
        var allowed = _settings.Current.AllowedApps;
        foreach (var app in installed)
        {
            // 预先抓取真实图标（安卓有效），在加入集合前赋值以便绑定生效
            if (!string.IsNullOrEmpty(app.PackageName))
            {
                try { app.Icon = await catalogSvc.GetAppIconAsync(app.PackageName); }
                catch { /* 回落 emoji */ }
            }
            var selected = allowed.Any(a => a.SameAs(app));
            Catalog.Add(new CatalogEntry(app, selected));
        }
    }

    public void ToggleApp(CatalogEntry entry)
    {
        var allowed = _settings.Current.AllowedApps;
        var existing = allowed.FirstOrDefault(a => a.SameAs(entry.App));
        if (entry.Selected && existing is null)
            allowed.Add(entry.App);
        else if (!entry.Selected && existing is not null)
            allowed.Remove(existing);
    }

    public async Task SaveAsync()
    {
        // 同步应用勾选状态（防止直接改集合未触发）
        var allowed = _settings.Current.AllowedApps;
        foreach (var e in Catalog)
        {
            var existing = allowed.FirstOrDefault(a => a.SameAs(e.App));
            if (e.Selected && existing is null) allowed.Add(e.App);
            else if (!e.Selected && existing is not null) allowed.Remove(existing);
        }

        if (!string.IsNullOrWhiteSpace(NewPin))
        {
            if (NewPin.Length < 4)
            {
                Status = "密码至少需要 4 位";
                return;
            }
            _settings.SetPin(NewPin);
            NewPin = string.Empty;
        }

        _settings.Current.AllowedSites = AllowedSites.ToList();

        // 保存小游戏开关：全部开启时存空列表表示“全部允许”
        var enabled = Games.Where(g => g.IsOn).Select(g => g.Info.Type.ToString()).ToList();
        if (enabled.Count == Games.Count)
            enabled.Clear();
        _settings.Current.EnabledGames = enabled;

        await _settings.SaveAsync();

        // 立即应用护眼设置（无需重启）
        EyeCare.Refresh();

        Status = "已保存 ✓";
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public void AddSite()
    {
        var s = NewSite?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return;
        if (!AllowedSites.Contains(s))
            AllowedSites.Add(s);
        NewSite = string.Empty;
    }

    public void RemoveSite(string site) => AllowedSites.Remove(site);

    /// <summary>清空所有游戏排行榜记录（家长操作）。</summary>
    public void ClearLeaderboard()
    {
        ServiceProvider.Get<ILeaderboardService>().Clear();
        Status = "排行榜已清空";
    }

    public void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
