using System.Net.Http.Json;
using System.Text.Json;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// <see cref="IContentService"/> 的默认实现。
/// 离线内容永远可用；在线内容仅在 <c>ChildSettings.OnlineContentEnabled</c> 开启且配置了
/// 服务器地址时尝试拉取，失败则静默回退离线。
/// </summary>
public class ContentService : IContentService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly ISettingsService _settings;

    public ContentService(ISettingsService settings)
    {
        _settings = settings;
    }

    public bool OnlineEnabled
    {
        get
        {
            var c = _settings.Current;
            return c.OnlineContentEnabled && !string.IsNullOrWhiteSpace(c.ServerUrl);
        }
    }

    public async Task<List<KidsAudioGroup>> GetAudioGroupsAsync()
    {
        var groups = new List<KidsAudioGroup>(KidsAudioData.All);

        var online = await FetchAsync("/api/content/audio");
        if (online is not null)
        {
            foreach (var g in online)
            {
                if (groups.Any(x => x.Key == g.Key)) continue;
                groups.Add(ToAudioGroup(g));
            }
        }

        return groups;
    }

    public async Task<List<ContentGroup>> GetVideoGroupsAsync()
    {
        var groups = KidsContentSeeds.VideoGroups();

        var online = await FetchAsync("/api/content/video");
        if (online is not null)
        {
            foreach (var g in online)
            {
                if (groups.Any(x => x.Key == g.Key)) continue;
                groups.Add(ToContentGroup(g, MediaKind.Video));
            }
        }

        return groups;
    }

    public async Task<List<ContentGroup>> GetArticleGroupsAsync()
    {
        var groups = KidsContentSeeds.ArticleGroups();

        var online = await FetchAsync("/api/content/articles");
        if (online is not null)
        {
            foreach (var g in online)
            {
                if (groups.Any(x => x.Key == g.Key)) continue;
                groups.Add(ToContentGroup(g, MediaKind.Article));
            }
        }

        return groups;
    }

    // ── 从家长服务器拉取受控内容目录 ──────────────────────────────
    private async Task<List<ContentGroupDto>?> FetchAsync(string path)
    {
        if (!OnlineEnabled) return null;
        try
        {
            var baseUrl = Normalize(_settings.Current.ServerUrl);
            if (baseUrl is null) return null;
            using var resp = await _http.GetAsync($"{baseUrl}{path}");
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<ContentGroupDto>>();
        }
        catch
        {
            // 联网失败不应影响儿童正常使用，静默回退离线
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

    // ── DTO → 领域模型 ───────────────────────────────────────────
    private static KidsAudioGroup ToAudioGroup(ContentGroupDto g)
    {
        var group = new KidsAudioGroup
        {
            Key = g.Key,
            Name = g.Name,
            Emoji = g.Emoji,
            Color = g.Color,
            Desc = g.Desc
        };
        foreach (var it in g.Items)
        {
            var kind = it.AudioKind switch
            {
                "Ambient" => KidsAudioKind.Ambient,
                "Speech" => KidsAudioKind.Speech,
                _ => KidsAudioKind.Music
            };
            group.Items.Add(new KidsAudioItem
            {
                Id = it.Id,
                GroupKey = g.Key,
                Title = it.Title,
                Emoji = it.Emoji,
                Desc = it.Desc,
                Color = g.Color,
                Kind = kind,
                FileName = it.FileName,
                AudioUrl = it.AudioUrl,
                Lines = it.Lines ?? [],
                Locale = it.Locale,
                Loop = it.Loop,
                Hint = it.Hint
            });
        }
        return group;
    }

    private static ContentGroup ToContentGroup(ContentGroupDto g, MediaKind kind)
    {
        var group = new ContentGroup
        {
            Key = g.Key,
            Name = g.Name,
            Emoji = g.Emoji,
            Color = g.Color,
            Desc = g.Desc,
            Kind = kind
        };
        foreach (var it in g.Items)
        {
            group.Items.Add(new ContentItem
            {
                Id = it.Id,
                GroupKey = g.Key,
                Title = it.Title,
                Emoji = it.Emoji,
                Desc = it.Desc,
                Color = g.Color,
                Hint = it.Hint,
                Kind = kind,
                Source = ContentItemSource.Online,
                VideoUrl = it.VideoUrl,
                VideoFileName = it.VideoFileName,
                NarrationLines = it.NarrationLines ?? [],
                Body = it.Body ?? [],
                Tags = it.Tags ?? []
            });
        }
        return group;
    }

    // ── 与服务端对齐的内容 DTO（仅用于反序列化）──────────────────
    private class ContentItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Emoji { get; set; } = "🎵";
        public string Desc { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF8A65";
        public string Hint { get; set; } = string.Empty;
        public string Kind { get; set; } = "Audio";
        public string Source { get; set; } = "online";
        public string? AudioKind { get; set; }
        public string? FileName { get; set; }
        public string? AudioUrl { get; set; }
        public string[]? Lines { get; set; }
        public string Locale { get; set; } = "zh";
        public bool Loop { get; set; }
        public string? VideoUrl { get; set; }
        public string? VideoFileName { get; set; }
        public string[]? NarrationLines { get; set; }
        public string[]? Body { get; set; }
        public string[]? Tags { get; set; }
    }

    private class ContentGroupDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Emoji { get; set; } = "🎵";
        public string Color { get; set; } = "#FF8A65";
        public string Desc { get; set; } = string.Empty;
        public string Kind { get; set; } = "Audio";
        public List<ContentItemDto> Items { get; set; } = new();
    }
}
