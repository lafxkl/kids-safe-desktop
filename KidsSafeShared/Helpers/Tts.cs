using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Maui.Media;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 统一的 TTS 语音播报封装。
/// 设计要点：
/// 1) 优先使用指定语言包（"en" 英文 / "zh" 中文）；未安装对应语言包或查询失败时，自动回退到系统默认语音，最大化出声概率；
/// 2) 获取语言列表（GetLocalesAsync）的异常被单独捕获，绝不阻断真正的朗读调用；
/// 3) 朗读异常不再被静默吞掉，而是记录到 LastError 并输出调试日志，便于排查“设备缺少语音包”等问题；
/// 4) 提供 ProbeAsync() 在启动时探测可用性，供 UI 提示用户。
/// </summary>
public static class Tts
{
    private static readonly ConcurrentDictionary<string, Locale?> _localeCache = new();
    private static string? _lastError;

    /// <summary>最近一次朗读失败的原因（诊断用）。</summary>
    public static string? LastError => _lastError;

    /// <summary>清空最近一次错误，用于「试听」前重置诊断状态。</summary>
    public static void ClearError() => _lastError = null;

    public static async Task SpeakAsync(string text, string? localePrefix = null)
        => await SpeakAsync(text, localePrefix, CancellationToken.None);

    /// <summary>
    /// 支持取消的朗读。故事朗读需要「停止」立刻生效，所以必须把 token 一路传进去；
    /// 取消后本轮朗读结束，调用方的循环自然退出。
    /// </summary>
    /// <param name="pitch">
    /// 音高，1.0 为原声。故事朗读用它做「声音风格」——低一点沉，高一点亮。
    /// 设备/平台忽略该值时不会报错，只是听不出差别，所以调用方应保证界面文案不夸大其词。
    /// </param>
    public static async Task SpeakAsync(string text, string? localePrefix, CancellationToken ct, float pitch = 1f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (ct.IsCancellationRequested) return;

        try
        {
            Locale? loc = null;
            if (!string.IsNullOrWhiteSpace(localePrefix))
                loc = await ResolveLocaleAsync(localePrefix);

            var hasPitch = Math.Abs(pitch - 1f) > 0.001f;
            SpeechOptions? opts = loc is not null || hasPitch
                ? new SpeechOptions
                {
                    Locale = loc,
                    Pitch = hasPitch ? Math.Clamp(pitch, 0.5f, 1.6f) : null
                }
                : null;

            await TextToSpeech.SpeakAsync(text, opts, ct);
        }
        catch (OperationCanceledException)
        {
            // 用户点了停止，属于正常流程
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            Debug.WriteLine($"[TTS] 朗读失败：{ex}");
#if DEBUG
            Console.WriteLine($"[TTS] SpeakAsync failed: {ex}");
#endif
            if (ct.IsCancellationRequested) return;

            // 指定语言朗读失败时，再尝试系统默认语音：去掉 locale，但保留音高
            try
            {
                var hasPitch2 = Math.Abs(pitch - 1f) > 0.001f;
                var fallback = hasPitch2
                    ? new SpeechOptions { Pitch = Math.Clamp(pitch, 0.5f, 1.6f) }
                    : null;
                await TextToSpeech.SpeakAsync(text, fallback, ct);
            }
            catch (Exception ex2)
            {
                _lastError = ex2.Message;
                Debug.WriteLine($"[TTS] 默认语音也失败：{ex2}");
            }
        }
    }

    private static async Task<Locale?> ResolveLocaleAsync(string prefix)
    {
        if (_localeCache.TryGetValue(prefix, out var cached)) return cached;

        Locale? found = null;
        try
        {
            var locales = await TextToSpeech.GetLocalesAsync();
            found = locales.FirstOrDefault(l => l.Language != null && l.Language.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                 ?? locales.FirstOrDefault(l => l.Language != null && l.Language.Contains(prefix, System.StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] 获取语言列表失败，将改用系统默认语音：{ex.Message}");
        }

        _localeCache[prefix] = found;
        return found;
    }

    /// <summary>探测 TTS 是否可用，返回 (是否可用, 诊断详情)。</summary>
    public static async Task<(bool Available, string Detail)> ProbeAsync()
    {
        try
        {
            var locales = await TextToSpeech.GetLocalesAsync();
            var count = locales?.Count() ?? 0;
            if (count == 0)
                return (false, "设备未安装任何语音包（TTS voices），朗读功能无法出声。");

            var hasZh = locales!.Any(l => l.Language != null && l.Language.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase));
            var hasEn = locales.Any(l => l.Language != null && l.Language.StartsWith("en", System.StringComparison.OrdinalIgnoreCase));
            var tip = hasZh ? "" : "（未检测到中文语音包，中文朗读可能失败，建议安装）";
            return (true, $"可用语音包 {count} 个（含中文：{hasZh}，含英文：{hasEn}）{tip}");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return (false, "TTS 引擎初始化失败：" + ex.Message);
        }
    }
}
