namespace KidsSafeDesktop.Models;

/// <summary>
/// 一张「看图识字」卡片：一个图片（真实抠图 PNG）或 emoji 当作图片，对应正确的汉字。
/// 干扰项在运行时从其它卡片的词语中随机抽取，保证不重复且多样。
/// Image 优先：有图片时显示图片，否则回退到 Emoji。
/// </summary>
public class PictureWordCard
{
    public string Emoji { get; init; } = string.Empty;
    public string Word { get; init; } = string.Empty;
    /// <summary>图片资源文件名（位于 Resources/Images，例如 "pw_sun.png"）。为空则用 Emoji。</summary>
    public string Image { get; init; } = string.Empty;
    public bool HasImage => !string.IsNullOrEmpty(Image);
}
