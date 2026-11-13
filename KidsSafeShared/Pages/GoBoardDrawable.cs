using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 围棋棋盘绘制：木纹底、9×9 网格、星位、黑白棋子与最后一手标记。
///
/// 所有尺寸都由画布宽高现算（不写死像素），所以同一份代码在手机、折叠屏、平板上
/// 都能画成正方形棋盘；点击坐标的换算也用同一套 <see cref="Measure"/>，保证「点哪儿落哪儿」。
/// </summary>
public sealed class GoBoardDrawable : IDrawable
{
    public Models.GoEngine? Engine { get; set; }

    /// <summary>最近一手是否高亮（悔棋后为 false）。</summary>
    public bool ShowLastMove { get; set; } = true;

    private static readonly Color WoodLight = Color.FromArgb("#F0C98A");
    private static readonly Color WoodDark = Color.FromArgb("#D9A45B");
    private static readonly Color LineColor = Color.FromArgb("#6B4A20");
    private static readonly Color BlackStone = Color.FromArgb("#263238");
    private static readonly Color BlackStoneEdge = Color.FromArgb("#101418");
    private static readonly Color WhiteStone = Color.FromArgb("#FFFDF7");
    private static readonly Color WhiteStoneEdge = Color.FromArgb("#B9AE9A");

    /// <summary>棋盘几何：格子边长、左上角第一个交叉点的坐标。</summary>
    public static (float Cell, float OriginX, float OriginY, float Side) Measure(float width, float height)
    {
        var size = Models.GoEngine.Size;
        var side = Math.Min(width, height);
        // 两侧各留 0.9 格做边距，避免最外圈的棋子贴边
        var cell = side / (size - 1 + 1.8f);
        var boardSide = cell * (size - 1);
        var originX = (width - boardSide) / 2f;
        var originY = (height - boardSide) / 2f;
        return (cell, originX, originY, boardSide);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Engine is null) return;

        var (cell, ox, oy, boardSide) = Measure(dirtyRect.Width, dirtyRect.Height);
        if (cell <= 0) return;

        var pad = cell * 0.9f;
        var boardRect = new RectF(ox - pad, oy - pad, boardSide + pad * 2, boardSide + pad * 2);
        var radius = cell * 0.6f;

        // 棋盘木底
        canvas.FillColor = WoodDark;
        canvas.FillRoundedRectangle(boardRect, radius, radius);

        var inner = new RectF(boardRect.X + 3, boardRect.Y + 3, boardRect.Width - 6, boardRect.Height - 6);
        canvas.FillColor = WoodLight;
        canvas.FillRoundedRectangle(inner, radius * 0.9f, radius * 0.9f);

        // 网格
        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = Math.Max(1f, cell * 0.055f);
        var size = Models.GoEngine.Size;
        for (var i = 0; i < size; i++)
        {
            var x = ox + i * cell;
            var y = oy + i * cell;
            canvas.DrawLine(x, oy, x, oy + boardSide);
            canvas.DrawLine(ox, y, ox + boardSide, y);
        }

        // 星位（9 路：四角星 + 天元）
        canvas.FillColor = LineColor;
        foreach (var (r, c) in new[] { (2, 2), (2, 6), (4, 4), (6, 2), (6, 6) })
        {
            var cx = ox + c * cell;
            var cy = oy + r * cell;
            canvas.FillCircle(cx, cy, cell * 0.1f);
        }

        // 棋子
        var stoneR = cell * 0.46f;
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                var v = Engine.At(r, c);
                if (v == Models.GoEngine.Empty) continue;
                DrawStone(canvas, ox + c * cell, oy + r * cell, stoneR, v == Models.GoEngine.Black);
            }
        }

        // 最后一手：黑子上画白点，白子上画黑点
        if (ShowLastMove && Engine.LastMove is { } last)
        {
            var cx = ox + last.Col * cell;
            var cy = oy + last.Row * cell;
            var v = Engine.At(last.Row, last.Col);
            canvas.FillColor = v == Models.GoEngine.Black ? Colors.White : Colors.Black;
            canvas.FillCircle(cx, cy, cell * 0.13f);
        }
    }

    private static void DrawStone(ICanvas canvas, float cx, float cy, float radius, bool isBlack)
    {
        // 落影
        canvas.FillColor = Color.FromRgba(0, 0, 0, 45);
        canvas.FillCircle(cx + radius * 0.10f, cy + radius * 0.14f, radius);

        canvas.FillColor = isBlack ? BlackStone : WhiteStone;
        canvas.FillCircle(cx, cy, radius);

        canvas.StrokeColor = isBlack ? BlackStoneEdge : WhiteStoneEdge;
        canvas.StrokeSize = Math.Max(0.8f, radius * 0.09f);
        canvas.DrawCircle(cx, cy, radius);

        // 高光：左上角一点亮，棋子看起来是立体的而不是色块
        canvas.FillColor = isBlack ? Color.FromRgba(255, 255, 255, 55) : Color.FromRgba(255, 255, 255, 200);
        canvas.FillCircle(cx - radius * 0.30f, cy - radius * 0.32f, radius * 0.30f);
    }
}
