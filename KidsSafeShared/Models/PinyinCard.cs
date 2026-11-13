namespace KidsSafeDesktop.Models;

/// <summary>
/// 一张「拼音识字」卡片：一个汉字（或词语）+ 它的拼音 + 配图（emoji 或真实抠图 PNG）。
/// 干扰项在运行时从其它卡片的拼音中随机抽取，保证多样且不重复。
/// Image 优先：有图片时显示图片，否则回退到 Emoji。
/// </summary>
public class PinyinCard
{
    public string Emoji { get; init; } = string.Empty;
    public string Word { get; init; } = string.Empty;
    /// <summary>拼音（带声调，如 "māo"）。</summary>
    public string Pinyin { get; init; } = string.Empty;
    /// <summary>图片资源文件名（位于 Resources/Images，例如 "pw_sun.png"）。为空则用 Emoji。</summary>
    public string Image { get; init; } = string.Empty;
    public bool HasImage => !string.IsNullOrEmpty(Image);
}
