using KidsSafeDesktop.Models;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>绘制打气球的天空背景与所有气球。</summary>
public class BalloonDrawable : IDrawable
{
    public BalloonGameViewModel? Vm { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        grad.GradientStops.Add(new GradientStop(Color.FromArgb("#B3E5FC"), 0));
        grad.GradientStops.Add(new GradientStop(Color.FromArgb("#E1F5FE"), 1));
        canvas.SetFillPaint(grad, dirtyRect);
        canvas.FillRectangle(dirtyRect);

        if (Vm is null) return;
        foreach (var b in Vm.Balloons)
            DrawBalloon(canvas, b);
    }

    private static void DrawBalloon(ICanvas canvas, Balloon b)
    {
        if (b.Popped)
        {
            canvas.FillColor = Color.FromArgb("#FFD54F");
            for (var a = 0; a < 8; a++)
            {
                var ang = a * MathF.PI / 4f;
                canvas.FillCircle(b.X + MathF.Cos(ang) * b.R * 1.2f, b.Y - MathF.Sin(ang) * b.R * 1.2f, b.R * 0.18f);
            }
            return;
        }

        // 线
        canvas.StrokeColor = Color.FromArgb("#90A4AE");
        canvas.StrokeSize = 2;
        canvas.DrawLine(b.X, b.Y + b.R * 0.9f, b.X, b.Y + b.R * 1.8f);

        // 球体
        canvas.FillColor = b.Color;
        canvas.FillEllipse(b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2);
        // 高光
        canvas.FillColor = Colors.White.WithAlpha(0.45f);
        canvas.FillEllipse(b.X - b.R * 0.5f, b.Y - b.R * 0.6f, b.R * 0.5f, b.R * 0.7f);
        // 结
        canvas.FillColor = b.Color;
        var knot = new PathF();
        knot.MoveTo(b.X - b.R * 0.16f, b.Y + b.R * 0.85f);
        knot.LineTo(b.X + b.R * 0.16f, b.Y + b.R * 0.85f);
        knot.LineTo(b.X, b.Y + b.R * 1.1f);
        knot.Close();
        canvas.FillPath(knot);
    }
}
