using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KidsSafeServer;

/// <summary>
/// 一条用户提交（意见反馈 / 满意度调查 / 使用问卷）。
///
/// 设计取向：
/// - 只存「用户主动填的东西」，不存 IP、不存设备号、不存账号 —— 家长/孩子不需要为了说句话交出身份；
/// - Id 由客户端生成（GUID），重试补传时服务端靠它去重，不会因为断网重发多算一份；
/// - 时间一律服务端打点（UTC），客户端时钟不可信。
/// </summary>
public sealed class FeedbackItem
{
    /// <summary>客户端生成的唯一 id（幂等键）。</summary>
    public string Id { get; set; } = "";

    /// <summary>feedback / satisfaction / survey</summary>
    public string Kind { get; set; } = "";

    /// <summary>windows / android / linux / harmony / web</summary>
    public string? Platform { get; set; }

    public string? AppVersion { get; set; }

    /// <summary>从哪个入口提交的（parent-dashboard / website / settings …），用于判断渠道效果。</summary>
    public string? Channel { get; set; }

    /// <summary>
    /// 用户自愿留下的联系方式（邮箱/微信/手机）。不填就是匿名。
    /// </summary>
    public string? Contact { get; set; }

    /// <summary>
    /// 展示用昵称（选填，官网评论区用）。和 Contact 一样只在用户自己填的时候才有，
    /// 不填就在官网显示「匿名家长」——不是从设备或账号里推断出来的。
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>总体满意度 1–5。</summary>
    public int? Rating { get; set; }

    /// <summary>推荐意愿 0–10（NPS）。</summary>
    public int? Nps { get; set; }

    /// <summary>分维度评分：ease / content / stable / design / value，1–5。</summary>
    public Dictionary<string, int>? Dims { get; set; }

    /// <summary>标签：问题分类或「最喜欢的功能」。</summary>
    public List<string>? Tags { get; set; }

    /// <summary>问卷作答：题号 → 答案（选项文字或自由文本）。</summary>
    public Dictionary<string, string>? Answers { get; set; }

    /// <summary>自由文本（意见正文 / 建议）。</summary>
    public string? Content { get; set; }

    /// <summary>是否同意在官网匿名展示这段话。默认不同意 —— 不点就是不给公开。</summary>
    public bool Public { get; set; }

    /// <summary>服务端收到的时刻（UTC，ISO 8601）。</summary>
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// 反馈存储：按月一个 JSONL 文件（data/feedback/2026-11.jsonl），一行一条。
///
/// 为什么不是数据库：这一层的数据量是「每月几十到几百条」，SQLite 的运维成本
/// （备份、锁、迁移）远大于收益；JSONL 可以直接 grep、可以直接拷走、
/// 出问题用记事本就能看。真到了需要分页几万条的规模再换，接口形状不用变。
/// </summary>
public sealed class FeedbackStore
{
    public static readonly string[] Kinds = ["feedback", "satisfaction", "survey"];

    private readonly string _dir;
    private readonly object _lock = new();

    /// <summary>全部记录（启动时从磁盘读入，追加时同步维护）。</summary>
    private readonly List<FeedbackItem> _items = new();

    /// <summary>已收到的 Id，用于幂等去重。</summary>
    private readonly HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        WriteIndented = false
    };

    public FeedbackStore(string dir)
    {
        _dir = dir;
        Load();
    }

    /// <summary>磁盘上已有的条数（启动自检用）。</summary>
    public int Count { get { lock (_lock) return _items.Count; } }

    private void Load()
    {
        try
        {
            if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
            foreach (var file in Directory.EnumerateFiles(_dir, "*.jsonl").OrderBy(f => f, StringComparer.Ordinal))
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var item = JsonSerializer.Deserialize<FeedbackItem>(line, JsonOpts);
                        if (item is null || string.IsNullOrWhiteSpace(item.Id)) continue;
                        _items.Add(item);
                        _ids.Add(item.Id);
                    }
                    catch
                    {
                        // 单行坏掉不影响其它行：手工编辑过的文件、写一半断电，都可能出现半行
                    }
                }
            }
        }
        catch
        {
            // 目录不可写也不能让服务起不来：提交时会再尝试建目录并返回错误
        }
    }

    /// <summary>
    /// 写入一条。返回 false 表示字段不合格（error 里是给人看的原因）；
    /// <paramref name="duplicate"/> 为 true 表示这条之前已经收过（幂等命中，仍算成功）。
    /// </summary>
    public bool TryAdd(FeedbackItem item, out string? error, out bool duplicate)
    {
        error = null;
        duplicate = false;

        var kind = (item.Kind ?? "").Trim().ToLowerInvariant();
        if (!Kinds.Contains(kind))
        {
            error = "kind 只能是 feedback / satisfaction / survey";
            return false;
        }
        item.Kind = kind;

        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 64)
            item.Id = Guid.NewGuid().ToString("N");

        item.Content = Clean(item.Content, 2000);
        item.Contact = Clean(item.Contact, 120);
        // 昵称只用于展示，不参与任何身份判断：20 字够写「浩浩妈妈」，留太长会撑爆卡片
        item.Nickname = Clean(item.Nickname, 20);
        item.Platform = Clean(item.Platform, 24);
        item.AppVersion = Clean(item.AppVersion, 32);
        item.Channel = Clean(item.Channel, 40);
        if (item.Rating is < 1 or > 5) item.Rating = null;
        if (item.Nps is < 0 or > 10) item.Nps = null;

        if (item.Dims is not null)
        {
            var dims = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in item.Dims.Take(12))
            {
                var key = Clean(k, 24);
                if (string.IsNullOrEmpty(key)) continue;
                if (v is < 1 or > 5) continue;
                dims[key] = v;
            }
            item.Dims = dims.Count == 0 ? null : dims;
        }

        if (item.Tags is not null)
        {
            var tags = item.Tags
                .Select(t => Clean(t, 40))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(t => t!)
                .ToList();
            item.Tags = tags.Count == 0 ? null : tags;
        }

        if (item.Answers is not null)
        {
            var answers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (k, v) in item.Answers.Take(40))
            {
                var key = Clean(k, 32);
                var val = Clean(v, 500);
                if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(val)) continue;
                answers[key] = val!;
            }
            item.Answers = answers.Count == 0 ? null : answers;
        }

        // 「一条什么都没写的提交」不算数据：至少要有正文、评分或问卷作答之一
        var hasSomething = !string.IsNullOrWhiteSpace(item.Content)
                           || item.Rating is not null
                           || (item.Answers is { Count: > 0 });
        if (!hasSomething)
        {
            error = "内容为空";
            return false;
        }

        lock (_lock)
        {
            if (_ids.Contains(item.Id))
            {
                duplicate = true;
                return true;
            }

            item.CreatedAt = DateTimeOffset.UtcNow.ToString("o");

            try
            {
                Directory.CreateDirectory(_dir);
                var path = Path.Combine(_dir, DateTime.UtcNow.ToString("yyyy-MM") + ".jsonl");
                var line = JsonSerializer.Serialize(item, JsonOpts) + "\n";
                // UTF-8 无 BOM：这样 grep / tail -f 直接能看中文，不用处理开头三个字节
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                error = "服务端写入失败：" + ex.Message;
                return false;
            }

            _items.Add(item);
            _ids.Add(item.Id);
            return true;
        }
    }

    /// <summary>按时间倒序取记录（管理端与统计共用）。</summary>
    public List<FeedbackItem> Query(string? kind, int days, int limit, int offset)
    {
        var from = days <= 0 ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow.AddDays(-days);
        lock (_lock)
        {
            IEnumerable<FeedbackItem> q = _items;
            if (!string.IsNullOrWhiteSpace(kind))
                q = q.Where(i => string.Equals(i.Kind, kind, StringComparison.OrdinalIgnoreCase));
            if (from > DateTimeOffset.MinValue)
                q = q.Where(i => Parsed(i) >= from);
            return q.OrderByDescending(Parsed).Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 500)).ToList();
        }
    }

    /// <summary>符合条件的条数（管理端分页要显示「共 N 条」）。</summary>
    public int CountOf(string? kind, int days)
    {
        var from = days <= 0 ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow.AddDays(-days);
        lock (_lock)
        {
            return _items.Count(i =>
                (string.IsNullOrWhiteSpace(kind) || string.Equals(i.Kind, kind, StringComparison.OrdinalIgnoreCase)) &&
                (from == DateTimeOffset.MinValue || Parsed(i) >= from));
        }
    }

    /// <summary>官网公开的匿名留言：只有用户明确勾选「可以在官网匿名展示」的才出现。</summary>
    public List<object> PublicQuotes(int limit)
    {
        lock (_lock)
        {
            return _items
                .Where(i => i.Public && !string.IsNullOrWhiteSpace(i.Content))
                .OrderByDescending(Parsed)
                // 上限从 20 放宽到 100：官网评论区要整页展示，不是首页四条。
                // 公开留言本来就是用户勾了「可以展示」的，条数多不增加隐私风险，只是页面更长。
                .Take(Math.Clamp(limit, 1, 100))
                .Select(i => (object)new
                {
                    kind = i.Kind,
                    rating = i.Rating,
                    content = i.Content,
                    nickname = i.Nickname,
                    // 只到月：既能让读者判断时效，又不会精确到「谁在哪天说了什么」
                    month = Parsed(i).ToString("yyyy-MM"),
                    platform = i.Platform
                })
                .ToList();
        }
    }

    private static DateTimeOffset Parsed(FeedbackItem i) =>
        DateTimeOffset.TryParse(i.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t : DateTimeOffset.MinValue;

    /// <summary>官网与家长端共用的一份聚合结果（不含任何联系方式）。</summary>
    public object Stats(int days)
    {
        var from = days <= 0 ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow.AddDays(-days);
        List<FeedbackItem> list;
        lock (_lock)
        {
            list = _items.Where(i => from == DateTimeOffset.MinValue || Parsed(i) >= from).ToList();
        }

        var sat = list.Where(i => i.Kind == "satisfaction").ToList();
        var fb = list.Where(i => i.Kind == "feedback").ToList();
        var sv = list.Where(i => i.Kind == "survey").ToList();

        var rated = sat.Where(i => i.Rating is not null).Select(i => i.Rating!.Value).ToList();
        var npsList = sat.Where(i => i.Nps is not null).Select(i => i.Nps!.Value).ToList();
        var promoters = npsList.Count(v => v >= 9);
        var detractors = npsList.Count(v => v <= 6);
        var nps = npsList.Count == 0 ? (double?)null
            : Math.Round(100.0 * (promoters - detractors) / npsList.Count, 0);

        // 分维度：只统计真的打过分的那几项，缺项不算 0 分
        var dims = new Dictionary<string, object>();
        var dimNames = new[] { "ease", "content", "stable", "design", "value" };
        foreach (var name in dimNames)
        {
            var vals = sat.Where(i => i.Dims is not null && i.Dims.ContainsKey(name))
                          .Select(i => i.Dims![name]).ToList();
            if (vals.Count == 0) continue;
            dims[name] = new { count = vals.Count, avg = Math.Round(vals.Average(), 2) };
        }

        var tagCount = list.Where(i => i.Tags is not null)
                           .SelectMany(i => i.Tags!)
                           .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                           .Select(g => new { tag = g.Key, count = g.Count() })
                           .OrderByDescending(x => x.count)
                           .Take(12)
                           .ToList();

        var platformCount = list.Where(i => !string.IsNullOrWhiteSpace(i.Platform))
                                .GroupBy(i => i.Platform!, StringComparer.OrdinalIgnoreCase)
                                .Select(g => new { platform = g.Key, count = g.Count() })
                                .OrderByDescending(x => x.count)
                                .ToList();

        var today = DateTimeOffset.UtcNow.Date;
        return new
        {
            ok = true,
            days,
            generatedAt = DateTimeOffset.UtcNow.ToString("o"),
            total = list.Count,
            today = list.Count(i => Parsed(i) >= today),
            last7 = list.Count(i => Parsed(i) >= DateTimeOffset.UtcNow.AddDays(-7)),
            byKind = new { feedback = fb.Count, satisfaction = sat.Count, survey = sv.Count },
            platforms = platformCount,
            satisfaction = new
            {
                count = sat.Count,
                rated = rated.Count,
                // 一位小数就够：把「4.37」写成 4.4，比堆两位小数更像人说的话
                avgRating = rated.Count == 0 ? (double?)null : Math.Round(rated.Average(), 1),
                dist = new[] { 1, 2, 3, 4, 5 }.Select(star => rated.Count(v => v == star)).ToArray(),
                npsCount = npsList.Count,
                nps,
                promoters,
                detractors,
                dims
            },
            tags = tagCount,
            withContact = list.Count(i => !string.IsNullOrWhiteSpace(i.Contact)),
            withContent = list.Count(i => !string.IsNullOrWhiteSpace(i.Content))
        };
    }

    /// <summary>CSV 导出：带 BOM，Excel 双击打开中文不乱码。</summary>
    public string ToCsv(string? kind, int days)
    {
        var rows = Query(kind, days, 50000, 0).OrderBy(Parsed).ToList();
        var sb = new StringBuilder();
        sb.Append("时间(UTC),类型,平台,版本,渠道,评分,NPS,标签,正文,问卷作答,联系方式,同意公开\r\n");
        foreach (var i in rows)
        {
            sb.Append(Csv(i.CreatedAt)).Append(',')
              .Append(Csv(KindName(i.Kind))).Append(',')
              .Append(Csv(i.Platform)).Append(',')
              .Append(Csv(i.AppVersion)).Append(',')
              .Append(Csv(i.Channel)).Append(',')
              .Append(i.Rating?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(i.Nps?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(Csv(i.Tags is null ? "" : string.Join('|', i.Tags))).Append(',')
              .Append(Csv(i.Content)).Append(',')
              .Append(Csv(i.Answers is null ? "" : string.Join(" | ", i.Answers.Select(kv => $"{kv.Key}={kv.Value}")))).Append(',')
              .Append(Csv(i.Contact)).Append(',')
              .Append(i.Public ? "是" : "否")
              .Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 删除一条（用户要求删除自己的反馈时用）。
    ///
    /// JSONL 不能原地摘掉中间一行，只能整月文件重写：先写 .tmp 再 File.Move 覆盖，
    /// 这样即使重写过程中断电，也不会留下半个文件。
    /// </summary>
    public bool TryDelete(string id, out string? error)
    {
        error = null;
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item is null) { error = "没找到这条记录"; return false; }

            try
            {
                var month = Parsed(item).ToString("yyyy-MM");
                if (month == "0001-01") month = DateTime.UtcNow.ToString("yyyy-MM");
                var path = Path.Combine(_dir, month + ".jsonl");
                if (File.Exists(path))
                {
                    var keep = File.ReadAllLines(path)
                        .Where(line => !LineHasId(line, id))
                        .ToList();
                    var tmp = path + ".tmp";
                    File.WriteAllLines(tmp, keep, new UTF8Encoding(false));
                    File.Move(tmp, path, overwrite: true);
                }

                _items.Remove(item);
                _ids.Remove(item.Id);
                return true;
            }
            catch (Exception ex)
            {
                error = "删除失败：" + ex.Message;
                return false;
            }
        }
    }

    private static bool LineHasId(string line, string id)
    {
        try
        {
            var item = JsonSerializer.Deserialize<FeedbackItem>(line, JsonOpts);
            return item is not null && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string KindName(string kind) => kind switch
    {
        "feedback" => "意见反馈",
        "satisfaction" => "满意度",
        "survey" => "问卷",
        _ => kind
    };

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var v = s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        if (v.Contains('"') || v.Contains(',') || v.Contains(' '))
            v = "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    /// <summary>去掉控制字符、压掉超长内容。换行与制表保留（正文本来就可能是多段）。</summary>
    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var sb = new StringBuilder(Math.Min(s.Length, max));
        foreach (var ch in s)
        {
            if (sb.Length >= max) break;
            if (ch == '\n' || ch == '\r' || ch == '\t' || !char.IsControl(ch)) sb.Append(ch);
        }
        var v = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}

/// <summary>
/// 提交限流：同一 IP 10 分钟 12 条、24 小时 60 条。
///
/// 目的不是防黑客，是防「有人按住提交按钮刷一万条」把数据弄脏。
/// 亲子产品没有登录门槛，这个门槛必须留在服务端，不能只靠前端节流。
/// </summary>
public sealed class FeedbackLimiter
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _hits = new(StringComparer.Ordinal);

    public bool Allow(string ip, int per10Min = 12, int perDay = 60)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            if (!_hits.TryGetValue(ip, out var list))
            {
                list = new List<DateTimeOffset>();
                _hits[ip] = list;
            }
            list.RemoveAll(t => (now - t).TotalHours >= 24);
            if (list.Count >= perDay) return false;
            if (list.Count(t => (now - t).TotalMinutes < 10) >= per10Min) return false;
            list.Add(now);

            // 顺手清掉一整天没动静的 IP，避免字典无限长大
            if (_hits.Count > 5000)
            {
                foreach (var key in _hits.Where(kv => kv.Value.Count == 0 || (now - kv.Value[^1]).TotalHours >= 24)
                                         .Select(kv => kv.Key).Take(1000).ToList())
                    _hits.Remove(key);
            }
            return true;
        }
    }
}
