using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>一条涂鸦笔画：一串点 + 颜色 + 宽度。</summary>
public class DoodleStroke
{
    public List<PointF> Points { get; } = new();
    public Color Color { get; set; } = Colors.Black;
    public float Width { get; set; } = 8;
}

/// <summary>在 GraphicsView 上绘制所有笔画。</summary>
public class DoodleDrawable : IDrawable
{
    public List<DoodleStroke> Strokes { get; } = new();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 白纸背景
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(dirtyRect);

        foreach (var s in Strokes)
        {
            if (s.Points.Count == 0) continue;
            canvas.StrokeColor = s.Color;
            canvas.StrokeSize = s.Width;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            if (s.Points.Count == 1)
            {
                // 单点：画一个圆点
                var p = s.Points[0];
                canvas.FillColor = s.Color;
                canvas.FillCircle(p.X, p.Y, s.Width / 2f);
                continue;
            }

            var prev = s.Points[0];
            for (var i = 1; i < s.Points.Count; i++)
            {
                var cur = s.Points[i];
                canvas.DrawLine(prev.X, prev.Y, cur.X, cur.Y);
                prev = cur;
            }
        }
    }
}
