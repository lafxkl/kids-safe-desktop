using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 特需（视障 / 听障）训练页共用的无障碍反馈工具。
/// 设计原则：
/// 1) 视障页「听为主、看为辅」——每个状态变化都伴随语音播报，触控目标 ≥ 96dp；
/// 2) 听障页「看为主、颤为辅」——所有反馈用高对比色块 + 屏幕闪烁 + 振动，绝不依赖声音；
/// 3) 任何平台能力缺失（如桌面无马达、无 TTS 包）都必须静默降级，不能让训练页崩溃。
/// </summary>
public static class A11y
{
    /// <summary>轻点反馈（用于「已选中」）。</summary>
    public static void Tap() => Vibrate(HapticFeedbackType.Click);

    /// <summary>答对反馈：一次长振动，听障儿童可凭手感确认。</summary>
    public static void Success() => Vibrate(HapticFeedbackType.LongPress);

    /// <summary>答错反馈：两次短振动，与答对明确区分。</summary>
    public static void Fail()
    {
        Vibrate(HapticFeedbackType.Click);
        _ = Task.Run(async () =>
        {
            await Task.Delay(130);
            Vibrate(HapticFeedbackType.Click);
        });
    }

    private static void Vibrate(HapticFeedbackType type)
    {
        try { HapticFeedback.Default.Perform(type); }
        catch { /* 桌面 / 无马达设备静默降级 */ }
    }

    /// <summary>语音播报（视障训练的主要输出通道）。</summary>
    public static Task SayAsync(string text) => Tts.SpeakAsync(text, "zh");

    /// <summary>
    /// 让元素短暂「闪」成指定颜色再复原。听障训练里替代声音做提示 / 正误反馈。
    /// 复原时用最初的背景色，因此调用方无需自己记录原色。
    /// </summary>
    public static async Task FlashAsync(VisualElement el, Color color, int holdMs = 200)
    {
        var origin = el.BackgroundColor;
        try
        {
            el.BackgroundColor = color;
            await Task.Delay(holdMs);
        }
        catch { /* 元素已销毁 */ }
        finally
        {
            try { el.BackgroundColor = origin; } catch { }
        }
    }

    /// <summary>把十六进制色串转 Color，非法时给灰色兜底（数据表里手写的颜色可能打错）。</summary>
    public static Color ColorOf(string hex)
    {
        try { return Color.FromArgb(hex); }
        catch { return Color.FromArgb("#7E57C2"); }
    }
}
