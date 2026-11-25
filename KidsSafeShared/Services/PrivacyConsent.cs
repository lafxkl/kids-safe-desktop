using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 隐私政策的同意状态。
///
/// 合规要点（工信部 164 号文 / 各安卓商店审核规则）：
/// 1. 首次启动必须先展示隐私政策并取得「主动点击」的同意，不得默认勾选；
/// 2. 同意之前不得初始化任何第三方 SDK、不得发起网络请求、不得读取设备标识；
/// 3. 不同意时可以退出应用，且不得因拒绝而降低已有功能；
/// 4. 隐私政策条款发生重大变更时，需要重新征得同意 —— 因此这里记的是「版本号」，
///    只比对 true/false 是不够的。
/// </summary>
public static class PrivacyConsent
{
    /// <summary>当前隐私政策版本。内容有实质修改时递增，用户会被要求重新同意。</summary>
    public const string CurrentVersion = "1.1";

    /// <summary>隐私政策生效日期（展示用）。</summary>
    public const string EffectiveDate = "2026-11-04";

    private const string KeyAgreed = "privacy_agreed";
    private const string KeyVersion = "privacy_agreed_version";
    private const string KeyTime = "privacy_agreed_at";

    /// <summary>是否已就「当前版本」的隐私政策取得同意。</summary>
    public static bool HasAgreed
    {
        get
        {
            try
            {
                return Preferences.Get(KeyAgreed, false)
                       && Preferences.Get(KeyVersion, string.Empty) == CurrentVersion;
            }
            catch
            {
                return false;   // 读不到一律按「未同意」处理，宁可多问一次
            }
        }
    }

    /// <summary>同意时间（未同意时为 null，用于家长面板展示与留痕）。</summary>
    public static DateTime? AgreedAt
    {
        get
        {
            var raw = Preferences.Get(KeyTime, string.Empty);
            return DateTime.TryParse(raw, out var t) ? t : null;
        }
    }

    /// <summary>记录同意。只写本地，不联网。</summary>
    public static void Agree()
    {
        Preferences.Set(KeyAgreed, true);
        Preferences.Set(KeyVersion, CurrentVersion);
        Preferences.Set(KeyTime, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    /// <summary>撤回同意（家长面板里提供，撤回后下次启动会重新弹出同意页）。</summary>
    public static void Revoke()
    {
        Preferences.Remove(KeyAgreed);
        Preferences.Remove(KeyVersion);
        Preferences.Remove(KeyTime);
    }
}
