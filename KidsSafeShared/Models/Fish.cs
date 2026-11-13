using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>大鱼吃小鱼里的一条鱼（玩家鱼或 AI 鱼）。坐标为画布像素坐标。</summary>
public class Fish
{
    public float X { get; set; }
    public float Y { get; set; }
    public float R { get; set; }
    public float Vx { get; set; }   // 每秒像素（x 方向）
    public float Vy { get; set; }   // 每秒像素（y 方向）
    public Color Color { get; set; } = Colors.Orange;
    public bool IsPlayer { get; set; }
    public double Angle { get; set; } // 朝向（弧度），用于绘制
}
