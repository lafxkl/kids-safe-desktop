using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 启动动画绘制器（GraphicsView + IDrawable 逐帧重绘）。
/// 视觉主题：糖果渐变 + 守护之盾 + 旋转光环 + 环绕星点。
/// 标语固定为「守护儿童网络安全」。
/// </summary>
public class SplashDrawable : IDrawable
{
    /// <summary>启动标语：守护儿童网络安全。</summary>
    public const string Slogan = "守护儿童网络安全";

    private const string AppName = "儿童安全桌面";
    private const string EnglishName = "Kids Safe Desktop";

    // 糖果色板（与 AppIcon 渐变一致）
    private static readonly Color CandyPink = Color.FromArgb("#FF9EC4");
    private static readonly Color CandyPurple = Color.FromArgb("#B388FF");
    private static readonly Color CandyBlue = Color.FromArgb("#82D0FF");
    private static readonly Color ShieldInk = Color.FromArgb("#7E57C2");

    /// <summary>动画已播放的秒数，由页面按帧推进。</summary>
    public double Time { get; set; }

    /// <summary>初始化加载进度 0~1，驱动底部进度条。</summary>
    public double Progress { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;
        float cx = w / 2f;
        float cy = h * 0.40f;

        float r = Math.Clamp(MathF.Min(w, h) * 0.16f, 44f, 92f);

        DrawBackground(canvas, dirtyRect, w, h);
        DrawWaves(canvas, cx, cy, r);
        DrawParticles(canvas, cx, cy, r);
        DrawOrbitRing(canvas, cx, cy, r);
        DrawShield(canvas, cx, cy, r);

        var textTop = cy + r + 52f;
        DrawAppName(canvas, w, textTop);
        DrawSlogan(canvas, w, textTop + 38f);
        DrawEnglishName(canvas, w, textTop + 84f);
        DrawProgress(canvas, w, h);
    }

    // ==================== 背景 ====================

    private void DrawBackground(ICanvas canvas, RectF rect, float w, float h)
    {
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        grad.GradientStops.Add(new GradientStop(CandyPink, 0));
        grad.GradientStops.Add(new GradientStop(CandyPurple, 0.5f));
        grad.GradientStops.Add(new GradientStop(CandyBlue, 1));
        canvas.SetFillPaint(grad, rect);
        canvas.FillRectangle(rect);

        // 缓慢漂浮的柔光泡泡，让画面不死板
        for (var i = 0; i < 5; i++)
        {
            var phase = (float)(Time * (0.16f + i * 0.05f)) + i * 1.7f;
            var bx = w * (0.18f + 0.17f * i) + MathF.Sin(phase) * 26f;
            var by = h * (0.16f + (i % 3) * 0.26f) + MathF.Cos(phase * 0.8f) * 22f;
            var br = 44f + i * 13f;
            canvas.FillColor = Colors.White.WithAlpha(0.10f);
            canvas.FillCircle(bx, by, br);
        }
    }

    // ==================== 扩散波纹 ====================

    private void DrawWaves(ICanvas canvas, float cx, float cy, float r)
    {
        const float period = 1.9f;
        for (var i = 0; i < 3; i++)
        {
            var t = (float)((Time + i * period / 3.0) % period) / period;
            var ease = 1f - MathF.Pow(1f - t, 2f);
            canvas.StrokeColor = Colors.White.WithAlpha((1f - t) * 0.34f);
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(cx, cy, r * (1.18f + ease * 1.45f));
        }
    }

    // ==================== 环绕星点 ====================

    private void DrawParticles(ICanvas canvas, float cx, float cy, float r)
    {
        for (var i = 0; i < 14; i++)
        {
            var seed = i * 47f;
            var orbit = r * (1.95f + (i % 4) * 0.42f);
            var speed = 16f + (i % 5) * 8f;
            var ang = (float)(Time * speed + seed) * MathF.PI / 180f;

            var px = cx + MathF.Cos(ang) * orbit;
            var py = cy + MathF.Sin(ang) * orbit * 0.60f;   // 椭圆轨道更有层次

            var size = 3.0f + (i % 3) * 1.7f;
            var alpha = 0.30f + 0.42f * MathF.Abs(MathF.Sin(ang));
            canvas.FillColor = Colors.White.WithAlpha(alpha);
            DrawSparkle(canvas, px, py, size);
        }
    }

    /// <summary>四角闪光小星星。</summary>
    private static void DrawSparkle(ICanvas canvas, float x, float y, float r)
    {
        var p = new PathF();
        p.MoveTo(x, y - r);
        p.LineTo(x + r * 0.30f, y - r * 0.30f);
        p.LineTo(x + r, y);
        p.LineTo(x + r * 0.30f, y + r * 0.30f);
        p.LineTo(x, y + r);
        p.LineTo(x - r * 0.30f, y + r * 0.30f);
        p.LineTo(x - r, y);
        p.LineTo(x - r * 0.30f, y - r * 0.30f);
        p.Close();
        canvas.FillPath(p);
    }

    // ==================== 旋转光环 ====================

    private void DrawOrbitRing(ICanvas canvas, float cx, float cy, float r)
    {
        canvas.SaveState();
        canvas.Translate(cx, cy);
        canvas.Rotate((float)(Time * 46 % 360));

        canvas.StrokeColor = Colors.White.WithAlpha(0.88f);
        canvas.StrokeSize = 5f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeDashPattern = new[] { 22f, 15f };

        var d = r * 1.62f;
        canvas.DrawEllipse(-d, -d, d * 2f, d * 2f);
        canvas.RestoreState();
    }

    // ==================== 守护之盾 ====================

    private void DrawShield(ICanvas canvas, float cx, float cy, float r)
    {
        var appear = Clamp01((Time - 0.10) / 0.58);
        var scale = appear <= 0 ? 0f : EaseOutBack(appear);
        if (scale <= 0) return;

        canvas.SaveState();
        canvas.Translate(cx, cy);
        canvas.Scale(scale, scale);
        canvas.Translate(-cx, -cy);

        var path = BuildShield(cx, cy, r);

        // 盾面柔和渐变
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        g.GradientStops.Add(new GradientStop(Colors.White.WithAlpha(0.98f), 0));
        g.GradientStops.Add(new GradientStop(Colors.White.WithAlpha(0.72f), 1));
        canvas.SetFillPaint(g, new RectF(cx - r, cy - r, r * 2f, r * 2f));
        canvas.FillPath(path);

        // 盾边描线
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 4f;
        canvas.DrawPath(path);

        DrawGuardianFace(canvas, cx, cy, r);
        canvas.RestoreState();
    }

    /// <summary>盾牌轮廓：上宽下尖，圆润收底。</summary>
    private static PathF BuildShield(float cx, float cy, float r)
    {
        var p = new PathF();
        p.MoveTo(cx, cy - 1.00f * r);
        p.LineTo(cx + 0.78f * r, cy - 0.62f * r);
        p.LineTo(cx + 0.78f * r, cy + 0.12f * r);
        p.QuadTo(cx + 0.78f * r, cy + 0.78f * r, cx, cy + 1.12f * r);
        p.QuadTo(cx - 0.78f * r, cy + 0.78f * r, cx - 0.78f * r, cy + 0.12f * r);
        p.LineTo(cx - 0.78f * r, cy - 0.62f * r);
        p.Close();
        return p;
    }

    /// <summary>盾内守护者笑脸：两只眼睛 + 一抹微笑。</summary>
    private static void DrawGuardianFace(ICanvas canvas, float cx, float cy, float r)
    {
        canvas.FillColor = ShieldInk;

        var eyeDx = r * 0.28f;
        var eyeY = cy - r * 0.14f;
        var eyeR = r * 0.085f;

        canvas.FillCircle(cx - eyeDx, eyeY, eyeR);
        canvas.FillCircle(cx + eyeDx, eyeY, eyeR);

        // 微笑弧线
        canvas.StrokeColor = ShieldInk;
        canvas.StrokeSize = r * 0.075f;
        canvas.StrokeLineCap = LineCap.Round;
        var mw = r * 0.34f;
        var my = cy + r * 0.16f;
        var mh = r * 0.22f;
        canvas.DrawArc(cx - mw, my, mw * 2f, mh * 2f, 20f, 140f, false, false);
    }

    // ==================== 文案 ====================

    private void DrawAppName(ICanvas canvas, float w, float y)
    {
        var t = Clamp01((Time - 0.42) / 0.5);
        if (t <= 0) return;

        canvas.FontSize = 21f;
        canvas.FontColor = Colors.White.WithAlpha(0.92f * t);
        canvas.DrawString(AppName, 0, y + (1f - t) * 12f, w, 32f,
            HorizontalAlignment.Center, VerticalAlignment.Top);
    }

    /// <summary>标语逐字揭幕（带轻微上浮与过冲）。</summary>
    private void DrawSlogan(ICanvas canvas, float w, float y)
    {
        var chars = Slogan.ToCharArray();
        var cw = 32f;
        var gap = 5f;
        var total = cw * chars.Length + gap * (chars.Length - 1);
        var startX = (w - total) / 2f;

        canvas.FontSize = 27f;
        for (var i = 0; i < chars.Length; i++)
        {
            var t = Clamp01((Time - 0.92 - i * 0.075) / 0.36);
            if (t <= 0) continue;

            var alpha = (float)Math.Min(1.0, t * 1.7);
            var rise = (1f - alpha) * 15f;
            var cx = startX + i * (cw + gap);

            canvas.FontColor = Colors.White.WithAlpha(alpha);
            canvas.DrawString(chars[i].ToString(), cx, y + rise, cw, 40f,
                HorizontalAlignment.Center, VerticalAlignment.Top);

            if (t < 1f) continue;
            canvas.FillColor = Colors.White.WithAlpha(0.85f);
            canvas.FillCircle(cx + cw / 2f, y + 46f, 2.6f);
        }
    }

    private void DrawEnglishName(ICanvas canvas, float w, float y)
    {
        var t = Clamp01((Time - 1.70) / 0.55);
        if (t <= 0) return;

        canvas.FontSize = 13f;
        canvas.FontColor = Colors.White.WithAlpha(0.72f * t);
        canvas.DrawString(EnglishName, 0, y, w, 24f,
            HorizontalAlignment.Center, VerticalAlignment.Top);
    }

    // ==================== 底部进度条 ====================

    private void DrawProgress(ICanvas canvas, float w, float h)
    {
        var trackW = MathF.Min(w * 0.34f, 230f);
        var x = (w - trackW) / 2f;
        var y = h - 92f;
        var bh = 8f;

        canvas.FillColor = Colors.White.WithAlpha(0.26f);
        canvas.FillRoundedRectangle(x, y, trackW, bh, bh / 2f);

        // 真实加载进度与扫描进度取较大者，保证视觉始终连贯
        var sweep = (float)((Time * 0.5) % 1.0);
        var fill = MathF.Max((float)Progress, sweep);
        var fw = MathF.Max(bh, trackW * fill);

        canvas.FillColor = Colors.White.WithAlpha(0.95f);
        canvas.FillRoundedRectangle(x, y, fw, bh, bh / 2f);
    }

    // ==================== 缓动 ====================

    private static float Clamp01(double t) => (float)(t < 0 ? 0 : t > 1 ? 1 : t);

    /// <summary>带回弹的出场缓动（轻微过冲）。</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
