namespace KidsSafeDesktop.Models;

/// <summary>
/// 朗读的「声音风格」。
///
/// 说明一件容易被误解的事：一台手机的中文语音包通常只有一个，所以这里不是真的换发音人，
/// 而是调「音高（Pitch）」+「段间停顿倍率（GapScale）」得到的听感差异。
/// 音高在中低区间变化 + 停顿拉长，听起来就像换了个大人在讲；
/// 停压缩短则显得轻快。界面上会把这层如实写出来，不假装有八个配音演员。
/// </summary>
public sealed record VoiceStyle(
    string Key,
    string Name,
    string Emoji,
    float Pitch,
    float GapScale,
    string Desc);

/// <summary>四种声音风格 + 本机持久化。</summary>
public static class VoiceStyles
{
    public const string PrefKey = "story_voice_style";

    /// <summary>音高的安全区间。超出 0.5–1.6 会明显失真（像快进的卡通音），所以预设都在区间内。</summary>
    public const float MinPitch = 0.5f;
    public const float MaxPitch = 1.6f;

    public static readonly VoiceStyle Gentle =
        new("gentle", "温柔姐姐", "🌸", 1.12f, 1.25f, "声音轻一点，句子之间多留点空白");

    public static readonly VoiceStyle Grandpa =
        new("grandpa", "慈祥爷爷", "🧓", 0.84f, 1.45f, "低沉缓慢，像坐在床边慢慢讲");

    public static readonly VoiceStyle Buddy =
        new("buddy", "活泼小伙伴", "🐣", 1.32f, 0.82f, "清脆利落，故事走得更快");

    public static readonly VoiceStyle Plain =
        new("plain", "标准朗读", "📖", 1.00f, 1.00f, "系统原本的声音，不快不慢");

    public static readonly VoiceStyle[] All = { Gentle, Grandpa, Buddy, Plain };

    /// <summary>默认用温柔姐姐——睡前场景里，最不容易把孩子越听越精神。</summary>
    public static VoiceStyle Default => Gentle;

    public static VoiceStyle ByKey(string? key)
        => All.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>本机当前选择；存储值损坏时静默回退默认，不给孩子看异常。</summary>
    public static VoiceStyle Current
    {
        get
        {
            try { return ByKey(Preferences.Get(PrefKey, Default.Key)); }
            catch { return Default; }
        }
    }

    public static void Set(VoiceStyle style)
    {
        try { Preferences.Set(PrefKey, style.Key); }
        catch { /* 存储不可用时仅本次生效 */ }
    }
}
