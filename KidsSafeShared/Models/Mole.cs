using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Models;

/// <summary>打地鼠里的一只地鼠（或地鼠洞）。坐标为画布像素坐标。</summary>
public class Mole
{
    public float X { get; set; }
    public float Y { get; set; }
    public float R { get; set; }

    /// <summary>0=藏起来，1=冒头，2=被打中（短暂显示星星）。</summary>
    public int State { get; set; }

    /// <summary>当前状态剩余时间（秒）。</summary>
    public float Timer { get; set; }
}
