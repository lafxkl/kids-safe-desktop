using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 语音输入的跨平台门面。
///
/// 默认关闭，且必须家长过 PIN 才能打开：
/// 语音识别在 Android 上会把孩子说的话交给系统语音服务，虽然不进我们的服务器，
/// 但「麦克风」这件事本身要让家长知情并同意。默认关 + PIN 开关是这个应用一贯的做法
/// （大模型生成也是同一条规矩）。
/// </summary>
public static class VoiceInput
{
    public const string PrefKey = "voice_input_on";

    public static bool Enabled
    {
        get
        {
            try { return Preferences.Get(PrefKey, false); }
            catch { return false; }
        }
        set
        {
            try { Preferences.Set(PrefKey, value); }
            catch { /* 存储不可用时仅本次会话生效 */ }
        }
    }

#if ANDROID
    public static bool IsSupported => Droid.VoiceInput.IsSupported;

    public static Task<string?> ListenAsync(string prompt = "说出你想听的故事")
        => Droid.VoiceInput.ListenAsync(prompt);
#else
    /// <summary>Windows 上没有等价的系统识别入口，明确返回不支持，由界面隐藏入口。</summary>
    public static bool IsSupported => false;

    public static Task<string?> ListenAsync(string prompt = "")
        => Task.FromResult<string?>(null);
#endif
}
