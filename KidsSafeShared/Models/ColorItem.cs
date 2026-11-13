namespace KidsSafeDesktop.Models;

/// <summary>一个颜色的名称与十六进制值，用于「找颜色」游戏。</summary>
public class ColorItem
{
    public string Name { get; init; } = string.Empty;
    public string Hex { get; init; } = "#000000";
}
