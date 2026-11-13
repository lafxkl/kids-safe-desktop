using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>一个上升的气球（坐标为画布像素坐标，y 向下为正）。</summary>
public class Balloon
{
    public float X { get; set; }
    public float Y { get; set; }
    public float R { get; set; }

    /// <summary>每秒像素速度（负值表示上升）。</summary>
    public float Vy { get; set; }

    public Color Color { get; set; } = Colors.Red;

    /// <summary>被打中后短暂显示爆炸星星。</summary>
    public bool Popped { get; set; }

    public float PopTimer { get; set; }
}
