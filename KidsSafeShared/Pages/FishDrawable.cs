using KidsSafeDesktop.Models;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>绘制大鱼吃小鱼的整个水域与所有鱼。</summary>
public class FishDrawable : IDrawable
{
    public FishGameViewModel? Vm { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 水域背景
        canvas.FillColor = Color.FromArgb("#B3E5FC");
        canvas.FillRectangle(dirtyRect);

        // 几个气泡装饰
        canvas.FillColor = Color.FromArgb("#81D4FA");
        canvas.FillCircle(dirtyRect.Width * 0.2f, dirtyRect.Height * 0.25f, 6);
        canvas.FillCircle(dirtyRect.Width * 0.75f, dirtyRect.Height * 0.4f, 9);
        canvas.FillCircle(dirtyRect.Width * 0.5f, dirtyRect.Height * 0.7f, 5);

        if (Vm is null) return;
        foreach (var f in Vm.Others)
            DrawFish(canvas, f, false);
        DrawFish(canvas, Vm.Player, true);
    }

    private static void DrawFish(ICanvas canvas, Fish f, bool isPlayer)
    {
        canvas.SaveState();
        canvas.Translate(f.X, f.Y);
        canvas.Rotate((float)(f.Angle * 180 / Math.PI));

        var w = f.R * 2f;
        var h = f.R * 1.3f;

        // 尾巴（在身体后方 -x 一侧）
        canvas.FillColor = f.Color;
        var tailW = f.R * 0.9f;
        var tail = new PathF();
        tail.MoveTo(-f.R, -h * 0.35f);
        tail.LineTo(-f.R, h * 0.35f);
        tail.LineTo(-f.R - tailW, 0f);
        tail.Close();
        canvas.FillPath(tail);

        // 身体
        canvas.FillColor = f.Color;
        canvas.FillEllipse(-f.R, -h / 2, w, h);

        // 玩家鱼加白色描边以便辨认
        if (isPlayer)
        {
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 3;
            canvas.DrawEllipse(-f.R, -h / 2, w, h);
        }

        // 眼睛
        canvas.FillColor = Colors.White;
        canvas.FillCircle(f.R * 0.45f, -h * 0.18f, f.R * 0.22f);
        canvas.FillColor = Colors.Black;
        canvas.FillCircle(f.R * 0.52f, -h * 0.18f, f.R * 0.11f);

        canvas.RestoreState();
    }
}
