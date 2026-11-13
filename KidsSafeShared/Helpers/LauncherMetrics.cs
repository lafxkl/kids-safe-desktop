namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 桌面式图标（磁贴）的统一尺寸算法。
///
/// 为什么要有这个类：首页 HomePage 与「小游戏乐园 / 学习天地 / 英语学习」等子页面
/// 原本各自算尺寸（首页固定 72dp、子页面按宽度算），PC 窗口一变宽就出现
/// 「首页图标小、子页面图标大」的不一致。现在所有页面都用同一份算法：
/// 同一个可用宽度 → 同一个列数 → 同一个磁贴尺寸，永远一致。
///
/// 约定：传入的 width 必须是「网格控件的可用宽度」= 控件宽度 − 左右 Padding，
/// 所以各页面的横向 Padding 也要保持一致（统一 18），否则会差半个像素级。
/// </summary>
public static class LauncherMetrics
{
    /// <summary>单个磁贴左右外边距之和（对应模板里的 Margin="6,8"）。</summary>
    public const double ItemMarginH = 12;

    /// <summary>磁贴最小/最大尺寸（dp），避免手机上过小、桌面超宽屏上过大。</summary>
    public const double MinTile = 56;
    public const double MaxTile = 132;

    /// <summary>按可用宽度决定列数：手机 4 列，折叠屏/平板 5~7 列，桌面 8 列。</summary>
    public static int SpanFor(double contentWidth) => contentWidth switch
    {
        < 420 => 4,
        < 700 => 5,
        < 980 => 6,
        < 1360 => 7,
        _ => 8
    };

    /// <summary>
    /// 由可用宽度算出磁贴边长：让「每行正好 span 个」，
    /// 多减 1 是取整余量，防止浮点误差把一行挤成 span+1 个导致换行错乱。
    /// </summary>
    public static double TileFor(double contentWidth)
    {
        if (contentWidth <= 0) return MinTile;
        int span = SpanFor(contentWidth);
        double tile = Math.Floor(contentWidth / span) - ItemMarginH - 1;
        return Math.Clamp(tile, MinTile, MaxTile);
    }
}
