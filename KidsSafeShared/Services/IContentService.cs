using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 儿童内容服务：汇聚「宝宝听 / 宝宝看 / 亲子阅」三类内容。
/// 以随包离线内容为基础，开启在线后从家长自建的 KidsSafeServer 拉取
/// 经人工审核的补充目录；任何网络异常都自动回退到离线内容，绝不抛错卡死。
/// </summary>
public interface IContentService
{
    /// <summary>是否启用了在线内容（需开启开关且配置了服务器地址）。</summary>
    bool OnlineEnabled { get; }

    /// <summary>拉取「宝宝听」分组（离线儿歌 + 可选在线补充）。</summary>
    Task<List<KidsAudioGroup>> GetAudioGroupsAsync();

    /// <summary>拉取「宝宝看」分组（离线视听故事 + 可选在线补充）。</summary>
    Task<List<ContentGroup>> GetVideoGroupsAsync();

    /// <summary>拉取「亲子阅」分组（离线安全图文 + 可选在线补充）。</summary>
    Task<List<ContentGroup>> GetArticleGroupsAsync();
}
