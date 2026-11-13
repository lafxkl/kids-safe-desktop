using System.Net.Http.Json;
using System.Text.Json;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// <see cref="ITextbookService"/> 的默认实现。
///
/// 三处缓存，都为「校园网/家里网速一般」这个现实场景服务：
/// ① 目录缓存 10 分钟，反复进出书架不重复请求；
/// ② 目录另存一份 json 到本地，下次断网也能看到书单（页图另算）；
/// ③ 页图按「书号/page-0001.jpg」落盘，翻回过看过的页不必再联网。
/// </summary>
public sealed class TextbookService : ITextbookService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly TimeSpan ShelfTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISettingsService _settings;
    private readonly object _gate = new();
    private readonly HashSet<string> _inflight = new();

    private TextbookShelf? _shelf;
    private DateTime _shelfAt = DateTime.MinValue;

    public TextbookService(ISettingsService settings) => _settings = settings;

    public bool OnlineEnabled
    {
        get
        {
            var c = _settings.Current;
            return c.OnlineContentEnabled && !string.IsNullOrWhiteSpace(c.ServerUrl);
        }
    }

    // ── 目录 ───────────────────────────────────────────────────
    public async Task<TextbookShelf?> GetShelfAsync(bool force = false)
    {
        lock (_gate)
        {
            if (!force && _shelf is not null && DateTime.UtcNow - _shelfAt < ShelfTtl)
                return _shelf;
        }

        var baseUrl = BaseUrl();
        if (baseUrl is not null && OnlineEnabled)
        {
            try
            {
                var shelf = await Http.GetFromJsonAsync<TextbookShelf>($"{baseUrl}/api/books", JsonOpts);
                if (shelf is { Editions.Count: > 0 })
                {
                    lock (_gate)
                    {
                        _shelf = shelf;
                        _shelfAt = DateTime.UtcNow;
                    }
                    await SaveShelfCopyAsync(shelf);
                    return shelf;
                }
            }
            catch
            {
                // 联网失败不回传错误，走下面的本地副本
            }
        }

        // 回落：上一次成功拉取的目录
        var cached = await LoadShelfCopyAsync();
        if (cached is not null)
        {
            lock (_gate)
            {
                _shelf = cached;
                _shelfAt = DateTime.UtcNow - ShelfTtl + TimeSpan.FromMinutes(1); // 一分钟后再试联网
            }
        }
        return cached ?? _shelf;
    }

    private static string ShelfCopyPath => Path.Combine(FileSystem.AppDataDirectory, "textbooks", "shelf.json");

    private static async Task SaveShelfCopyAsync(TextbookShelf shelf)
    {
        try
        {
            var dir = Path.GetDirectoryName(ShelfCopyPath)!;
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(ShelfCopyPath, JsonSerializer.Serialize(shelf, JsonOpts));
        }
        catch
        {
            // 目录缓存写不进去不影响阅读
        }
    }

    private static async Task<TextbookShelf?> LoadShelfCopyAsync()
    {
        try
        {
            if (!File.Exists(ShelfCopyPath)) return null;
            var text = await File.ReadAllTextAsync(ShelfCopyPath);
            return JsonSerializer.Deserialize<TextbookShelf>(text, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    // ── 图片地址 ───────────────────────────────────────────────
    public string? CoverUrl(Textbook book)
    {
        var baseUrl = BaseUrl();
        if (baseUrl is null || string.IsNullOrEmpty(book.Cover)) return null;
        return baseUrl + book.Cover;
    }

    private static string PagePath(string id, int page) =>
        Path.Combine(FileSystem.AppDataDirectory, "textbooks", id, $"page-{page:0000}.jpg");

    // ── 页图 ───────────────────────────────────────────────────
    public async Task<string?> GetPageAsync(Textbook book, int page)
    {
        var path = PagePath(book.Id, page);
        if (File.Exists(path) && new FileInfo(path).Length > 1024) return path;

        var baseUrl = BaseUrl();
        if (baseUrl is null || !OnlineEnabled) return null;

        var url = $"{baseUrl}/books/pages/{book.Id}/page-{page:0000}.jpg";

        // 同一页被快速连点（或预热与翻页撞在一起）时只下载一次
        var key = $"{book.Id}:{page}";
        lock (_gate)
        {
            if (!_inflight.Add(key)) return null;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            if (bytes.Length < 1024) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
            return path;
        }
        catch
        {
            return null;
        }
        finally
        {
            lock (_gate) _inflight.Remove(key);
        }
    }

    public void Prefetch(Textbook book, int page)
    {
        if (!OnlineEnabled) return;
        foreach (var p in new[] { page + 1, page - 1 })
        {
            if (p < 1 || p > book.Pages) continue;
            var path = PagePath(book.Id, p);
            if (File.Exists(path)) continue;
            _ = Task.Run(async () =>
            {
                try { await GetPageAsync(book, p); } catch { /* 预热失败无所谓 */ }
            });
        }
    }

    public (int Books, int Pages, long Bytes) CacheStats()
    {
        try
        {
            var root = Path.Combine(FileSystem.AppDataDirectory, "textbooks");
            if (!Directory.Exists(root)) return (0, 0, 0);

            var books = 0;
            var pages = 0;
            long bytes = 0;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var files = Directory.GetFiles(dir, "page-*.jpg");
                if (files.Length == 0) continue;
                books++;
                pages += files.Length;
                foreach (var f in files)
                {
                    try { bytes += new FileInfo(f).Length; } catch { }
                }
            }
            return (books, pages, bytes);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    // ── 工具 ───────────────────────────────────────────────────
    private string? BaseUrl()
    {
        var raw = _settings.Current.ServerUrl;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            s = "http://" + s;
        return s.TrimEnd('/');
    }
}
