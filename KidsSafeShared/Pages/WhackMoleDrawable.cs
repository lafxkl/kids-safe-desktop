using KidsSafeDesktop.Models;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>绘制打地鼠的草地、地洞与地鼠。</summary>
public class WhackMoleDrawable : IDrawable
{
    public WhackMoleGameViewModel? Vm { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 草地背景
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        grad.GradientStops.Add(new GradientStop(Colors.LightGreen, 0));
        grad.GradientStops.Add(new GradientStop(Color.FromArgb("#A5D6A7"), 1));
        canvas.SetFillPaint(grad, dirtyRect);
        canvas.FillRectangle(dirtyRect);

        if (Vm is null) return;

        foreach (var m in Vm.Moles)
            DrawHoleAndMole(canvas, m);
    }

    private static void DrawHoleAndMole(ICanvas canvas, Mole m)
    {
        // 地洞（土堆）
        canvas.FillColor = Color.FromArgb("#6D4C41");
        canvas.FillEllipse(m.X - m.R * 1.5f, m.Y - m.R * 0.55f, m.R * 3.0f, m.R * 1.1f);
        canvas.FillColor = Color.FromArgb("#3E2723");
        canvas.FillEllipse(m.X - m.R * 1.15f, m.Y - m.R * 0.32f, m.R * 2.3f, m.R * 0.7f);

        if (m.State == 0) return;

        var cx = m.X;
        var cy = m.Y - m.R * 0.55f;

        // 身体
        canvas.FillColor = Color.FromArgb("#8D6E63");
        canvas.FillCircle(cx, cy, m.R * 0.95f);
        // 肚皮
        canvas.FillColor = Color.FromArgb("#D7CCC8");
        canvas.FillEllipse(cx - m.R * 0.5f, cy - m.R * 0.1f, m.R, m.R * 0.9f);
        // 眼睛
        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx - m.R * 0.35f, cy - m.R * 0.25f, m.R * 0.24f);
        canvas.FillCircle(cx + m.R * 0.35f, cy - m.R * 0.25f, m.R * 0.24f);
        canvas.FillColor = Colors.Black;
        canvas.FillCircle(cx - m.R * 0.35f, cy - m.R * 0.25f, m.R * 0.11f);
        canvas.FillCircle(cx + m.R * 0.35f, cy - m.R * 0.25f, m.R * 0.11f);
        // 鼻子
        canvas.FillColor = Color.FromArgb("#F06292");
        canvas.FillCircle(cx, cy + m.R * 0.12f, m.R * 0.15f);

        if (m.State == 2)
        {
            // 被打中：金色星星爆开
            canvas.FillColor = Color.FromArgb("#FFD54F");
            for (var a = 0; a < 8; a++)
            {
                var ang = a * MathF.PI / 4f;
                canvas.FillCircle(cx + MathF.Cos(ang) * m.R * 1.25f, cy - MathF.Sin(ang) * m.R * 1.25f, m.R * 0.17f);
            }
        }
    }
}
