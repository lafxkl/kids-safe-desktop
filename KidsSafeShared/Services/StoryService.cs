using System.Net.Http.Json;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// <see cref="IStoryService"/> 的默认实现：通过家长服务器代理访问故事大全。
/// 复用 <see cref="IContentService.OnlineEnabled"/> 的开关与服务器地址，失败时静默回退。
/// </summary>
public class StoryService : IStoryService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly ISettingsService _settings;
    private readonly IContentService _content;

    public StoryService(ISettingsService settings, IContentService content)
    {
        _settings = settings;
        _content = content;
    }

    public bool OnlineEnabled => _content.OnlineEnabled;

    private string? BaseUrl()
    {
        if (!OnlineEnabled) return null;
        return Normalize(_settings.Current.ServerUrl);
    }

    // ── 分类 ────────────────────────────────────────────────────
    public async Task<List<StoryCategory>> GetCategoriesAsync()
    {
        var baseUrl = BaseUrl();
        if (baseUrl is null) return new();
        try
        {
            var r = await _http.GetFromJsonAsync<CategoriesResp>($"{baseUrl}/api/story/types");
            var list = r?.Categories ?? new List<StoryCategory>();
            foreach (var c in list)
                c.Emoji = CategoryEmoji(c.Name);
            return list;
        }
        catch
        {
            return new();
        }
    }

    // ── 列表 ────────────────────────────────────────────────────
    public async Task<List<StorySummary>> GetListAsync(int typeId, int page)
    {
        var baseUrl = BaseUrl();
        if (baseUrl is null) return new();
        try
        {
            var r = await _http.GetFromJsonAsync<ListResp>($"{baseUrl}/api/story/list?typeId={typeId}&page={page}");
            return r?.Items ?? new();
        }
        catch
        {
            return new();
        }
    }

    // ── 搜索 ────────────────────────────────────────────────────
    public async Task<List<StorySummary>> SearchAsync(string keyword, int page)
    {
        var baseUrl = BaseUrl();
        if (baseUrl is null) return new();
        try
        {
            var enc = Uri.EscapeDataString(keyword);
            var r = await _http.GetFromJsonAsync<ListResp>($"{baseUrl}/api/story/search?keyword={enc}&page={page}");
            return r?.Items ?? new();
        }
        catch
        {
            return new();
        }
    }

    // ── 详情 ────────────────────────────────────────────────────
    public async Task<StoryDetail?> GetDetailAsync(long storyId)
    {
        var baseUrl = BaseUrl();
        if (baseUrl is null) return null;
        try
        {
            var r = await _http.GetFromJsonAsync<StoryDetail>($"{baseUrl}/api/story/detail?storyId={storyId}");
            return r;
        }
        catch
        {
            return null;
        }
    }

    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw!.Trim();
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "http://" + s;
        return s.TrimEnd('/');
    }

    /// <summary>给分类分配一个直观的图标。</summary>
    private static string CategoryEmoji(string name) => name switch
    {
        var n when n.Contains("睡前") => "🌙",
        var n when n.Contains("格林") => "🧚",
        var n when n.Contains("民间") => "🏮",
        var n when n.Contains("益智") => "🧩",
        var n when n.Contains("历史") => "📜",
        var n when n.Contains("寓言") => "🦊",
        var n when n.Contains("经典") => "👑",
        var n when n.Contains("成语") => "📚",
        var n when n.Contains("童话") => "🧸",
        var n when n.Contains("儿童") => "🧒",
        _ => "📖"
    };

    // ── 与服务端对齐的响应 DTO（仅反序列化）────────────────────
    private class CategoriesResp
    {
        public List<StoryCategory> Categories { get; set; } = new();
    }

    private class ListResp
    {
        public List<StorySummary> Items { get; set; } = new();
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }
}
