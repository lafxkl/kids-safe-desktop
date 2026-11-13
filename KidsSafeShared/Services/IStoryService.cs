using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 故事大全（mxnzp）客户端服务。
/// 所有请求都经由家长自建的 KidsSafeServer 代理（密钥只在服务端），不在客户端持有任何第三方密钥。
/// 仅当「在线内容」开启且配置了服务器地址时才联网；任何失败都返回空/空列表，绝不抛错卡死儿童界面。
/// </summary>
public interface IStoryService
{
    /// <summary>是否启用了在线故事（需开启在线内容开关且配置了服务器地址）。</summary>
    bool OnlineEnabled { get; }

    /// <summary>拉取故事分类（睡前故事/格林童话/…）。</summary>
    Task<List<StoryCategory>> GetCategoriesAsync();

    /// <summary>按分类拉取故事列表（第 page 页，从 1 开始）。</summary>
    Task<List<StorySummary>> GetListAsync(int typeId, int page);

    /// <summary>按关键词搜索故事（第 page 页，从 1 开始）。</summary>
    Task<List<StorySummary>> SearchAsync(string keyword, int page);

    /// <summary>拉取单篇故事正文。</summary>
    Task<StoryDetail?> GetDetailAsync(long storyId);
}
