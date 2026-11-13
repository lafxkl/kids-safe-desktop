using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 电子课本：目录 + 页图。
///
/// 课本原件（PDF）留在服务端，客户端只按需取「一页一张 jpg」并缓存到本地，
/// 所以第一次翻某页要等网络，之后同一页立刻出来，断网也能接着看已翻过的页。
/// </summary>
public interface ITextbookService
{
    /// <summary>家长是否开启了在线内容并填了服务器地址。</summary>
    bool OnlineEnabled { get; }

    /// <summary>拉取目录；失败时回落到上一次成功拉取的本地副本（可能为 null）。</summary>
    Task<TextbookShelf?> GetShelfAsync(bool force = false);

    /// <summary>封面绝对地址，直接给 Image 用。</summary>
    string? CoverUrl(Textbook book);

    /// <summary>
    /// 取第 <paramref name="page"/> 页（从 1 开始）。返回本地文件路径；
    /// 下载失败返回 null，由页面提示"再试一次"。
    /// </summary>
    Task<string?> GetPageAsync(Textbook book, int page);

    /// <summary>预热相邻页，翻页时不至于每次都在转圈。</summary>
    void Prefetch(Textbook book, int page);

    /// <summary>已缓存的册数 / 页数（帮助页与诊断用）。</summary>
    (int Books, int Pages, long Bytes) CacheStats();
}
