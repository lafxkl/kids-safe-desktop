using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 六维语言潜能雷达图。
///
/// 用 GraphicsView 手绘而不是引第三方图表库：这里只需要一个正六边形 + 一条折线，
/// 自己画没有依赖、也方便按深色底调色（图表库默认配色在夜间页面上会发灰）。
///
/// 口径上刻意做成「倾向」而非「成绩」：
/// 网格不标 0-10 的数字，只画 4 圈；数值变化用页面上的 ▲+1 表达，
/// 避免家长把它当成考试分数去比较。
/// </summary>
public sealed class LoomRadarDrawable : IDrawable
{
    /// <summary>六个维度的当前值（0-10）。</summary>
    public IReadOnlyDictionary<string, int> Values { get; set; } = new Dictionary<string, int>();

    public void Draw(ICanvas canvas, RectF rect)
    {
        var dims = StoryLoomCatalog.Dimensions;
        var n = dims.Length;
        if (n == 0) return;

        var cx = rect.Center.X;
        var cy = rect.Center.Y;
        var radius = Math.Min(rect.Width, rect.Height) * 0.30f;

        // 背景网格：4 圈
        canvas.StrokeColor = Color.FromArgb("#2A3568");
        canvas.StrokeSize = 1;
        for (var ring = 1; ring <= 4; ring++)
            canvas.DrawPath(RingPath(cx, cy, radius * ring / 4f, n));

        // 6 条辐条
        canvas.StrokeColor = Color.FromArgb("#232C58");
        for (var i = 0; i < n; i++)
        {
            var a = AngleOf(i, n);
            canvas.DrawLine(cx, cy, cx + radius * MathF.Cos(a), cy + radius * MathF.Sin(a));
        }

        // 数值折线
        var path = new PathF();
        for (var i = 0; i < n; i++)
        {
            var v = Math.Clamp(Values.TryGetValue(dims[i].Key, out var raw) ? raw : 5, 0, 10) / 10f;
            var a = AngleOf(i, n);
            var x = cx + radius * v * MathF.Cos(a);
            var y = cy + radius * v * MathF.Sin(a);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close();

        canvas.FillColor = Color.FromArgb("#33FF6B9D");
        canvas.FillPath(path);
        canvas.StrokeColor = Color.FromArgb("#FF6B9D");
        canvas.StrokeSize = 2;
        canvas.DrawPath(path);

        // 顶点圆点 + 维度名
        canvas.FontSize = 11;
        for (var i = 0; i < n; i++)
        {
            var a = AngleOf(i, n);
            var px = cx + radius * MathF.Cos(a);
            var py = cy + radius * MathF.Sin(a);

            canvas.FillColor = Color.FromArgb(dims[i].Accent);
            canvas.FillCircle(px, py, 4);

            var lx = cx + (radius + 26) * MathF.Cos(a);
            var ly = cy + (radius + 26) * MathF.Sin(a);
            canvas.FontColor = Color.FromArgb("#C9D2F5");
            canvas.DrawString(dims[i].Name, lx - 40, ly - 10, 80, 20,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static float AngleOf(int i, int n) => -MathF.PI / 2 + i * 2 * MathF.PI / n;

    private static PathF RingPath(float cx, float cy, float r, int n)
    {
        var path = new PathF();
        for (var i = 0; i < n; i++)
        {
            var a = AngleOf(i, n);
            var x = cx + r * MathF.Cos(a);
            var y = cy + r * MathF.Sin(a);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }
}
