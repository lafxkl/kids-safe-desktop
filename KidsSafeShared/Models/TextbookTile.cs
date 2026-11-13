namespace KidsSafeDesktop.Models;

/// <summary>
/// 书架上的一张卡片。把 <see cref="Textbook"/> 转成可直接绑定的显示对象：
/// 封面需要 ImageSource，旧版标记要控制角标显隐，直接在 XAML 里算不方便。
/// </summary>
public class TextbookTile
{
    public Textbook Book { get; init; } = new();

    public string Title => Book.Title;

    public string SubTitle => Book.SubTitle;

    public bool IsLegacy => Book.Legacy;

    public ImageSource? Cover { get; init; }
}
