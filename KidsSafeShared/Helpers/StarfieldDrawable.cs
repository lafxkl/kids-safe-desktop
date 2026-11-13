using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「心灵感应魔法」的背景星空。
/// 用固定随机种子生成，保证每次进入星点位置一致——否则来回切页面会像在下雪，
/// 反而抢掉卡片的注意力。只画一次，不做动画。
/// </summary>
public sealed class StarfieldDrawable : IDrawable
{
    private static readonly (float X, float Y, float R)[] Stars = BuildStars();

    private static (float, float, float)[] BuildStars()
    {
        var rnd = new Random(20261105);
        var stars = new (float, float, float)[80];
        for (var i = 0; i < stars.Length; i++)
        {
            stars[i] = (
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble(),
                0.7f + (float)rnd.NextDouble() * 1.7f);
        }
        return stars;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;

        foreach (var (x, y, r) in Stars)
        {
            canvas.FillColor = Color.FromArgb("#FFFFFF").WithAlpha(0.10f + r * 0.16f);
            canvas.FillCircle(x * dirtyRect.Width, y * dirtyRect.Height, r);
        }

        // 右上角一团柔光，给「水晶球在发光」一点空间感
        canvas.FillColor = Color.FromArgb("#7C5CFF").WithAlpha(0.10f);
        canvas.FillCircle(dirtyRect.Width * 0.82f, dirtyRect.Height * 0.12f, dirtyRect.Width * 0.42f);
    }
}
