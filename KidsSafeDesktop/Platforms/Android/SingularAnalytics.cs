#if ANDROID && SINGULAR
// SINGULAR 宏只在 -p:AppChannel=global 时定义（见 csproj）。
// 国内版连 AAR 都不引用，这个文件整体不参与编译——不是「带了但不调用」。
using System.Globalization;
using System.Reflection;
using Android.Content;
using Android.Telephony;
using Com.Singular.Sdk;
using KidsSafeDesktop.Helpers;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// 海外版归因统计（Singular Kids SDK）。
///
/// 为什么用「儿童版」而不是标准版：
///   App Store 审核指南 1.3 与 Google Play 家庭政策都禁止在儿童应用里采集
///   广告标识符（IDFA / GAID）。Kids SDK 在实现上就不读这两个 ID，
///   代价是丢掉「按设备 ID 的 SAN 归因」，只剩 Install Referrer（安卓）与 SKAN（iOS）。
///
/// 为什么国内不加载：
///   1. 国内商店（华为/小米/OPPO/vivo/应用宝）没有 Google Play，Install Referrer 拿不到数据，
///      接进来只有「把设备信息发到境外服务器」的成本，没有收益；
///   2. 《个人信息保护法》第 38 条：向境外提供个人信息需要单独同意 + 告知境外接收方，
///      不做这一整套就走不通；
///   3. 本应用的定位就是「数据不出本机」，能少一个出口就少一个。
///   所以：地区判定为中国大陆时，SDK 连 init 都不会调用（不是 init 后再关）。
///
/// 判定顺序（全部不需要任何权限，也不需要设备标识）：
///   构建渠道（cn/global）→ 系统地区 → 移动网络国家码 → 时区 → 语言。
///   任何一项指向中国大陆就按国内处理（宁可少采集，不可多采集）。
/// </summary>
internal static class SingularAnalytics
{
    /// <summary>家长面板里的开关。默认开启，但只对「海外地区 + 已配置密钥」的组合生效。</summary>
    private const string KeyEnabled = "overseas_analytics_enabled";

    private const string Category = "统计";

    private static bool _tried;       // TryStart 只跑一次（启动页与家长面板都可能触发）
    private static bool _initialized; // SDK 曾经 init 成功（决定开关重新打开时是 init 还是 resume）
    private static bool _active;      // 当前是否真的在上报

    /// <summary>SDK 当前是否在采集。所有上报前都要看这个。</summary>
    public static bool IsActive => _active;

    /// <summary>本机是否按「海外」处理。家长面板据此决定要不要显示这个开关。</summary>
    public static bool IsOverseas => RegionGate.IsOutsideMainlandChina;

    /// <summary>家长是否允许（默认允许）。</summary>
    public static bool IsAllowedByParent
    {
        get
        {
            try { return Preferences.Get(KeyEnabled, true); }
            catch { return true; }
        }
    }

    /// <summary>
    /// 启动页调用。条件不满足就安静跳过，任何异常都不向外抛
    /// （统计挂了不能影响孩子进桌面）。
    /// </summary>
    public static void TryStart()
    {
        if (_tried) return;
        _tried = true;

        try
        {
            // 密钥来自构建期注入（Singular.props / -p: 参数）。没配就是「功能未启用」。
            var (key, secret) = ReadCredentials();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            {
                AppLog.I(Category, "未配置 Singular 密钥，海外版统计不启用");
                return;
            }

            if (!RegionGate.IsOutsideMainlandChina)
            {
                AppLog.I(Category, $"按中国大陆处理（{RegionGate.Reason}），不加载统计 SDK");
                return;
            }

            if (!IsAllowedByParent)
            {
                AppLog.I(Category, "家长已关闭海外版统计，跳过");
                return;
            }

            var config = new SingularConfig(key, secret);
            config.WithSessionTimeoutInSec(60);   // 与 Singular 文档默认值一致，避免会话被切成碎片

#if DEBUG
            // 只在调试包打开日志：Release 一直打日志既费电，也会把会话细节写进 logcat。
            config.WithLoggingEnabled();
#endif

            _initialized = Singular.Init(global::Android.App.Application.Context, config);
            _active = _initialized;

            AppLog.I(Category, _initialized
                ? "Singular Kids SDK 已初始化（海外版）"
                : "Singular 初始化返回 false，已跳过");
        }
        catch (Exception ex)
        {
            AppLog.W(Category, "初始化失败（不影响使用）", ex);
        }
    }

    /// <summary>家长在面板里开/关。关闭会立刻向 SDK 发停止跟踪指令，不只是本地打标记。</summary>
    public static void SetAllowedByParent(bool enabled)
    {
        try { Preferences.Set(KeyEnabled, enabled); }
        catch (Exception ex) { AppLog.W(Category, "写入统计开关失败", ex); }

        try
        {
            if (!enabled)
            {
                if (_initialized) Singular.StopAllTracking();
                _active = false;
                AppLog.I(Category, "家长关闭海外版统计，已停止跟踪");
                return;
            }

            if (_initialized)
            {
                Singular.ResumeAllTracking();
                _active = true;
                AppLog.I(Category, "家长重新开启海外版统计");
            }
            else
            {
                _tried = false;      // 之前因为开关关着没初始化，这里补一次
                TryStart();
            }
        }
        catch (Exception ex)
        {
            AppLog.W(Category, "切换统计开关失败", ex);
        }
    }

    /// <summary>上报一个事件（不带参数）。未激活时是空操作。</summary>
    public static void Track(string name)
    {
        if (!_active || string.IsNullOrWhiteSpace(name)) return;

        try { Singular.Event(name); }
        catch (Exception ex) { AppLog.W(Category, $"上报事件 {name} 失败", ex); }
    }

    /// <summary>上报一个带属性的事件。属性会原样出现在 Singular 后台的事件明细里。</summary>
    public static void Track(string name, IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            Track(name);
            return;
        }

        if (!_active || string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var json = new Org.Json.JSONObject();
            foreach (var pair in properties)
            {
                json.Put(pair.Key, pair.Value);
            }

            Singular.EventJSON(name, json);
        }
        catch (Exception ex)
        {
            AppLog.W(Category, $"上报事件 {name} 失败", ex);
        }
    }

    /// <summary>从程序集元数据里取构建期注入的 Key / Secret。</summary>
    private static (string Key, string Secret) ReadCredentials()
        => (RegionGate.ReadMetadata("SingularApiKey"), RegionGate.ReadMetadata("SingularSecret"));
}

/// <summary>
/// 「这台设备算国内还是海外」的判定。结果在进程内只算一次。
///
/// 不读 IMEI / OAID / Android ID / 位置 —— 判断地区不需要这些，
/// 用系统地区、网络国家码、时区和语言就够了，而这些在同意隐私政策前也不会碰。
/// </summary>
internal static class RegionGate
{
    private static readonly Lazy<(bool Outside, string Reason)> Cached =
        new(Evaluate, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>true = 按海外处理（可以使用境外统计）；false = 按中国大陆处理。</summary>
    public static bool IsOutsideMainlandChina => Cached.Value.Outside;

    /// <summary>判定依据，写进日志方便家长反馈时定位。</summary>
    public static string Reason => Cached.Value.Reason;

    private static (bool Outside, string Reason) Evaluate()
    {
        // ① 构建渠道优先：打「国内包」时可以强制关掉，打「海外包」时可以强制打开。
        var channel = ReadMetadata("SingularChannel");
        if (string.Equals(channel, "cn", StringComparison.OrdinalIgnoreCase))
            return (false, "构建渠道 cn");
        if (string.Equals(channel, "global", StringComparison.OrdinalIgnoreCase))
            return (true, "构建渠道 global");

        // ② 系统地区设置（用户在系统里选的国家/地区）
        try
        {
            var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            if (string.Equals(region, "CN", StringComparison.OrdinalIgnoreCase))
                return (false, "系统地区 CN");
        }
        catch
        {
            // 个别 ROM 上 RegionInfo 会抛异常，跳过这一项
        }

        // ③ 移动网络国家码：getNetworkCountryIso 不需要任何权限（SIM 国家码才需要，所以没读它）
        try
        {
            var context = global::Android.App.Application.Context;
            if (context.GetSystemService(Context.TelephonyService) is TelephonyManager telephony)
            {
                var iso = telephony.NetworkCountryIso;
                if (!string.IsNullOrEmpty(iso))
                {
                    return iso.Equals("cn", StringComparison.OrdinalIgnoreCase)
                        ? (false, "移动网络国家码 cn")
                        : (true, $"移动网络国家码 {iso}");
                }
            }
        }
        catch
        {
            // 无电话服务 / 厂商限制时拿不到，继续看后面的信号
        }

        // ④ 时区：中国大陆全境同一时区，且没有别的国家用它
        try
        {
            var tz = TimeZoneInfo.Local.Id;
            if (tz.Contains("Shanghai", StringComparison.OrdinalIgnoreCase)
                || tz.Contains("Chongqing", StringComparison.OrdinalIgnoreCase)
                || tz.Contains("Harbin", StringComparison.OrdinalIgnoreCase)
                || tz.Contains("Urumqi", StringComparison.OrdinalIgnoreCase)
                || tz.Contains("Beijing", StringComparison.OrdinalIgnoreCase))
                return (false, $"时区 {tz}");
        }
        catch
        {
            // 忽略
        }

        // ⑤ 语言：简体中文一律按国内处理。海外华语用户会被少统计一次，
        //    但方向是「宁可少采集」，符合本应用的立场。
        try
        {
            var culture = CultureInfo.CurrentCulture.Name;
            if (culture.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)
                || string.Equals(culture, "zh-CN", StringComparison.OrdinalIgnoreCase))
                return (false, $"语言 {culture}");
        }
        catch
        {
            // 忽略
        }

        return (true, "未命中任何中国大陆特征");
    }

    /// <summary>读构建期写进程序集的自定义元数据（见 csproj 里的 AssemblyAttribute）。</summary>
    internal static string ReadMetadata(string key)
    {
        try
        {
            foreach (var attribute in typeof(RegionGate).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(attribute.Key, key, StringComparison.Ordinal))
                {
                    return attribute.Value ?? string.Empty;
                }
            }
        }
        catch
        {
            // 读不到按未配置处理
        }

        return string.Empty;
    }
}
#endif
