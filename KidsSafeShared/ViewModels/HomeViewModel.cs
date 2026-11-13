using System.Collections.ObjectModel;
using System.Timers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 儿童主页：显示允许的磁贴、时钟与剩余时间。
/// </summary>
public class HomeViewModel : BaseViewModel
{
    private readonly System.Timers.Timer _clockTimer = new(1000);
    private string _clockText = string.Empty;
    private string _remainingText = string.Empty;
    private string _greeting = "你好呀！";
    private bool _gamesVisible = true;
    private bool _onlineContentVisible;
    private string _nickname = "小朋友";
    private string _avatarEmoji = "🐯";

    private string _weatherEmoji = "🌡️";
    private string _weatherText = string.Empty;
    private bool _weatherVisible;
    private string _weatherCity = string.Empty;

    private string _accountBadge = "🟢 离线模式";
    private string _accountDetail = "数据保存在本机";
    private bool _isOffline = true;

    /// <summary>账号模式徽章文案（离线/在线）。</summary>
    public string AccountBadge
    {
        get => _accountBadge;
        set => SetProperty(ref _accountBadge, value);
    }

    /// <summary>账号模式补充说明（如服务器地址）。</summary>
    public string AccountDetail
    {
        get => _accountDetail;
        set => SetProperty(ref _accountDetail, value);
    }

    /// <summary>是否处于离线模式。</summary>
    public bool IsOffline
    {
        get => _isOffline;
        set => SetProperty(ref _isOffline, value);
    }

    private static readonly string[] Avatars =
    {
        "🐯", "🐰", "🐻", "🐼", "🦁", "🐸", "🐥", "🐨", "🦊", "🐶", "🐱", "🐵"
    };

    public ObservableCollection<AppItem> Apps { get; } = new();

    /// <summary>小朋友昵称（点首页头像可修改）。</summary>
    public string Nickname
    {
        get => _nickname;
        set => SetProperty(ref _nickname, value);
    }

    /// <summary>根据昵称稳定生成的专属头像 emoji。</summary>
    public string AvatarEmoji
    {
        get => _avatarEmoji;
        set => SetProperty(ref _avatarEmoji, value);
    }

    public string ClockText
    {
        get => _clockText;
        set => SetProperty(ref _clockText, value);
    }

    public string RemainingText
    {
        get => _remainingText;
        set => SetProperty(ref _remainingText, value);
    }

    public string Greeting
    {
        get => _greeting;
        set => SetProperty(ref _greeting, value);
    }

    /// <summary>家长是否允许显示小游戏入口。</summary>
    public bool GamesVisible
    {
        get => _gamesVisible;
        set => SetProperty(ref _gamesVisible, value);
    }

    /// <summary>
    /// 家长是否允许「在线内容」（家长中心 → 在线内容）。
    /// 关闭时首页隐藏听一听 / 星夜故事屋 / 电子课本三个入口：
    /// 它们的内容主体来自家长服务器，离线只剩零星内置内容，留着反而让孩子点进去以为坏了。
    /// </summary>
    public bool OnlineContentVisible
    {
        get => _onlineContentVisible;
        set => SetProperty(ref _onlineContentVisible, value);
    }

    private bool _eyeCareVisible;

    /// <summary>护眼模式当前是否生效（生效时在顶部显示提示徽章）。</summary>
    public bool EyeCareVisible
    {
        get => _eyeCareVisible;
        set => SetProperty(ref _eyeCareVisible, value);
    }

    /// <summary>按当前设置刷新护眼状态显示。</summary>
    public void RefreshEyeCare()
    {
        EyeCareVisible = Helpers.EyeCareRules.IsEffectivelyOn(
            ServiceProvider.Get<ISettingsService>().Current);
    }

    /// <summary>天气 emoji 图标（如 ☀️）。</summary>
    public string WeatherEmoji
    {
        get => _weatherEmoji;
        set => SetProperty(ref _weatherEmoji, value);
    }

    /// <summary>天气文字，例如「26° 北京 · 晴」。</summary>
    public string WeatherText
    {
        get => _weatherText;
        set => SetProperty(ref _weatherText, value);
    }

    /// <summary>是否已成功获取到天气（控制 UI 显隐）。</summary>
    public bool WeatherVisible
    {
        get => _weatherVisible;
        set => SetProperty(ref _weatherVisible, value);
    }

    /// <summary>天气定位到的城市名。</summary>
    public string WeatherCity
    {
        get => _weatherCity;
        set => SetProperty(ref _weatherCity, value);
    }

    private bool _hasWeatherAlerts;
    private ObservableCollection<WeatherHour> _weatherHours = new();
    private ObservableCollection<WeatherAlert> _weatherAlerts = new();

    /// <summary>逐小时预报。</summary>
    public ObservableCollection<WeatherHour> WeatherHours
    {
        get => _weatherHours;
        set => SetProperty(ref _weatherHours, value);
    }

    /// <summary>自动推导的预警/提醒。</summary>
    public ObservableCollection<WeatherAlert> WeatherAlerts
    {
        get => _weatherAlerts;
        set => SetProperty(ref _weatherAlerts, value);
    }

    /// <summary>是否有预警需要展示。</summary>
    public bool HasWeatherAlerts
    {
        get => _hasWeatherAlerts;
        set => SetProperty(ref _hasWeatherAlerts, value);
    }

    /// <summary>请求启动某应用（由页面处理平台启动）</summary>
    public event EventHandler<AppItem>? AppLaunchRequested;

    /// <summary>请求打开家长入口（由页面处理导航）</summary>
    public event EventHandler? ParentRequested;

    /// <summary>请求打开账号页（由页面处理导航）</summary>
    public event EventHandler? AccountRequested;

    public HomeViewModel()
    {
        ServiceProvider.Get<IAccountService>().Changed += (_, _) => LoadAccount();
    }

    public void Start()
    {
        LoadProfile();
        LoadAccount();
        _clockTimer.Elapsed += OnClockTick;
        _clockTimer.Start();
        UpdateClock();
        UpdateRemaining();
    }

    /// <summary>从账号服务刷新离线/在线状态显示。</summary>
    public void LoadAccount()
    {
        var acc = ServiceProvider.Get<IAccountService>().Current;
        IsOffline = acc.Mode == AccountMode.Offline;
        if (IsOffline)
        {
            AccountBadge = "🟢 离线模式";
            AccountDetail = "数据保存在本机";
        }
        else
        {
            AccountBadge = "☁️ 已联网";
            AccountDetail = "家长服务器 · " + (string.IsNullOrWhiteSpace(acc.ServerUrl) ? "已连接" : acc.ServerUrl);
        }
    }

    /// <summary>从设置载入昵称与专属头像。</summary>
    public void LoadProfile()
    {
        var name = ServiceProvider.Get<ISettingsService>().Current.Nickname;
        _nickname = string.IsNullOrWhiteSpace(name) ? "小朋友" : name.Trim();
        AvatarEmoji = Avatars[StableHash(_nickname) % Avatars.Length];
        OnPropertyChanged(nameof(Nickname));
        OnPropertyChanged(nameof(AvatarEmoji));
    }

    /// <summary>保存新的昵称（最长 8 字，空则回退到「小朋友」）。</summary>
    public async Task SetNicknameAsync(string? raw)
    {
        var name = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = "小朋友";
        if (name.Length > 8)
            name = name[..8];

        ServiceProvider.Get<ISettingsService>().Current.Nickname = name;
        await ServiceProvider.Get<ISettingsService>().SaveAsync();

        Nickname = name;
        AvatarEmoji = Avatars[StableHash(name) % Avatars.Length];
        UpdateClock();
    }

    private static int StableHash(string s)
    {
        int h = 0;
        foreach (var c in s)
            h = (h * 31 + c) & 0x7fffffff;
        return h;
    }

    public void Stop() => _clockTimer.Stop();

    public async Task RefreshAppsAsync()
    {
        Apps.Clear();
        var settings = ServiceProvider.Get<ISettingsService>();
        var catalog = ServiceProvider.Get<IAppCatalogService>();
        foreach (var a in settings.Current.AllowedApps)
        {
            // 重新从系统抓取真实图标（仅 Android 有效；其他平台回落到 emoji）
            if (!string.IsNullOrEmpty(a.PackageName))
            {
                try { a.Icon = await catalog.GetAppIconAsync(a.PackageName); }
                catch { /* 保留 emoji 兜底 */ }
            }
            Apps.Add(a);
        }
        GamesVisible = settings.Current.GamesEnabled;
        OnlineContentVisible = settings.Current.OnlineContentEnabled;
    }

    public void UpdateRemaining()
    {
        var st = ServiceProvider.Get<IScreenTimeService>();
        RemainingText = st.IsLockedOut
            ? "今天的时间用完啦"
            : "剩余 " + Format(ServiceProvider.Get<IScreenTimeService>().Remaining);
    }

    private void OnClockTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        UpdateClock();
        UpdateRemaining();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText = now.ToString("HH:mm");
        var h = now.Hour;
        var timeHi = h switch
        {
            < 6 => "夜深啦",
            < 12 => "早上好",
            < 14 => "中午好",
            < 18 => "下午好",
            _ => "晚上好"
        };
        Greeting = Nickname == "小朋友"
            ? $"{timeHi}呀！"
            : $"{timeHi}，{Nickname}！";
    }

    /// <summary>
    /// 异步加载天气（自动定位 + 逐小时预报 + 预警推导）。失败时静默降级（不显示天气卡片）。
    /// </summary>
    public async Task LoadWeatherAsync()
    {
        try
        {
            var city = ServiceProvider.Get<ISettingsService>().Current.WeatherCity;
            var info = await ServiceProvider.Get<IWeatherService>().GetForecastAsync(city);
            if (info is null)
            {
                WeatherVisible = false;
                return;
            }
            WeatherEmoji = info.Emoji;
            WeatherCity = info.City;
            WeatherText = $"{info.TempC:0}° {info.City} · {info.Description}";
            WeatherVisible = true;

            WeatherHours = new ObservableCollection<WeatherHour>(info.Hours);
            WeatherAlerts = new ObservableCollection<WeatherAlert>(info.Alerts);
            HasWeatherAlerts = info.Alerts.Count > 0;
        }
        catch
        {
            WeatherVisible = false;
        }
    }

    public void RequestLaunch(AppItem app) => AppLaunchRequested?.Invoke(this, app);

    public void RequestParent() => ParentRequested?.Invoke(this, EventArgs.Empty);

    public void RequestAccount() => AccountRequested?.Invoke(this, EventArgs.Empty);

    private static string Format(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours} 小时 {t.Minutes} 分";
        if (t.TotalMinutes >= 1)
            return $"{t.Minutes} 分 {t.Seconds} 秒";
        return $"{t.Seconds} 秒";
    }
}
