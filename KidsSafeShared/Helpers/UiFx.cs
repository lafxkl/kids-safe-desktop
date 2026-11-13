using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 儿童界面常用的趣味动效，让点击更有「童趣」反馈。
/// </summary>
public static class UiFx
{
    /// <summary>
    /// 点击时轻轻弹一下（缩小再弹回），用在磁贴、按钮、卡片上增加趣味反馈。
    /// 设计为可丢弃（fire-and-forget），不影响后续导航或业务。
    /// </summary>
    public static async void Pop(this View view)
    {
        await view.PopAsync();
    }

    /// <summary>
    /// 可等待版的 <see cref="Pop"/>。
    /// 导航前必须用它：页面构造（XAML 展开）会占满 UI 线程，
    /// 如果按下动画还没画完就展开新页面，动画会卡在半途，
    /// 表现为「点一下，图标先顿一下再跳页」。
    /// </summary>
    public static async Task PopAsync(this View view, int shrinkMs = 70, int restoreMs = 170)
    {
        if (view is null) return;
        try
        {
            await view.ScaleTo(0.92, (uint)shrinkMs, Easing.CubicOut);
            await view.ScaleTo(1.0, (uint)restoreMs, Easing.CubicIn);
        }
        catch
        {
            // 动画被中断（如页面已切换）时静默忽略
        }
    }
}
