#if ANDROID
using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.Speech;
using Microsoft.Maui.ApplicationModel;

namespace KidsSafeDesktop.Droid;

/// <summary>
/// 调系统自带的语音识别（不是自己录音）。
///
/// 为什么用 Intent 而不是 SpeechRecognizer：
/// 1) 系统识别器是独立进程/独立界面，麦克风权限由它自己持有，我们不用往清单里加录音权限——
///    对一个儿童桌面来说，「不需要录音权限」本身就是一条可验证的隐私承诺；
/// 2) 识别中随时可以取消，取消即返回 null，不会留下任何音频；
/// 3) 中文识别质量跟着系统的语音服务走，比自建模型稳。
///
/// 代价：机型差异大，个别定制系统没有识别器，所以 <see cref="IsSupported"/> 要显式判断，
/// 界面上不支持时就不显示麦克风按钮，而不是点了没反应。
/// </summary>
public static class VoiceInput
{
    /// <summary>请求码，MainActivity 用它区分是谁的结果。</summary>
    public const int RequestCode = 4711;

    private static TaskCompletionSource<string?>? _pending;

    public static bool IsSupported
    {
        get
        {
            try
            {
                return SpeechRecognizer.IsRecognitionAvailable(Platform.AppContext);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VoiceInput] 检测识别器失败：{ex.Message}");
                return false;
            }
        }
    }

    /// <summary>弹出系统识别界面，返回识别到的第一句；取消或失败返回 null。</summary>
    public static Task<string?> ListenAsync(string prompt)
    {
        // 已经在识别中：复用同一个 Task，避免连点两次弹出两个界面
        if (_pending is not null) return _pending.Task;

        var activity = Platform.CurrentActivity;
        if (activity is null || !IsSupported) return Task.FromResult<string?>(null);

        var tcs = new TaskCompletionSource<string?>();
        _pending = tcs;

        try
        {
            // 识别界面是另一个界面，本应用会进入 OnStop；而本应用开着「防退出」，
            // 会在 OnStop 后把自己拉回前台，正好把识别界面盖掉。
            // 所以先临时放行（回到前台时 OnResume 会自动解除），两者才不打架。
            KidsSafeDesktop.Platforms.Android.AndroidHooks.SuppressAutoReturnFor?.Invoke(TimeSpan.FromSeconds(60));

            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, "zh-CN");
            intent.PutExtra(RecognizerIntent.ExtraPrompt, prompt);
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 3);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
            activity.StartActivityForResult(intent, RequestCode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoiceInput] 启动识别失败：{ex}");
            _pending = null;
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }

    /// <summary>由 MainActivity.OnActivityResult 转发；重复调用无副作用。</summary>
    public static void HandleResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode) return;

        var tcs = _pending;
        if (tcs is null) return;
        _pending = null;

        string? text = null;
        if (resultCode == Result.Ok && data is not null)
        {
            var list = data.GetStringArrayListExtra(RecognizerIntent.ExtraResults);
            if (list is { Count: > 0 }) text = list[0];
        }

        tcs.TrySetResult(string.IsNullOrWhiteSpace(text) ? null : text!.Trim());
    }

    /// <summary>Activity 被系统回收前兜底，避免页面永远等一个不会来的结果。</summary>
    public static void Cancel()
    {
        var tcs = _pending;
        _pending = null;
        tcs?.TrySetResult(null);
    }
}
#endif
