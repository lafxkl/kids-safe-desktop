using Microsoft.Maui.Devices;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 棋盘类小游戏的格子尺寸计算。
///
/// 为什么需要它：以前每个游戏都写死格子像素值（连连看 62、迷宫 34），
/// 换了屏幕宽度就被裁掉右边或者顶到屏幕边缘。这里统一按「当前可用宽高」反算，
/// 并给一个上下限，保证小屏不出界、大屏（平板/PC）不被拉伸成巨型方块。
/// </summary>
public static class BoardMetrics
{
    /// <summary>
    /// 反算格子边长。
    /// </summary>
    /// <param name="availWidth">可用宽度（已扣除页面内边距）。</param>
    /// <param name="availHeight">可用高度（已扣除标题、按钮等固定占用）。</param>
    /// <param name="cols">横向格子数。</param>
    /// <param name="rows">纵向格子数。</param>
    /// <param name="minCell">最小边长，低于它触摸目标太小。</param>
    /// <param name="maxCell">最大边长，避免平板上出现巨型方块。</param>
    public static double Fit(double availWidth, double availHeight, int cols, int rows,
                             double minCell, double maxCell)
    {
        if (cols <= 0 || rows <= 0) return minCell;

        var byWidth = availWidth / cols;
        var byHeight = availHeight / rows;
        var cell = Math.Floor(Math.Min(byWidth, byHeight));

        if (double.IsNaN(cell) || double.IsInfinity(cell) || cell <= 0) cell = minCell;
        return Math.Clamp(cell, minCell, maxCell);
    }

    /// <summary>
    /// 首帧还没完成布局时的估算值：用屏幕物理尺寸换算成 dp。
    /// 布局完成后 <c>SizeChanged</c> 会用真实值重算，所以这里只要量级对就行。
    /// </summary>
    public static (double width, double height) EstimateByScreen(double widthPadding = 24, double heightChrome = 210)
    {
        try
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var density = info.Density <= 0 ? 1 : info.Density;
            return (info.Width / density - widthPadding, info.Height / density - heightChrome);
        }
        catch
        {
            return (360, 460);   // 兜底：按最常见的 360dp 小屏估算
        }
    }
}
