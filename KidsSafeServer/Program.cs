using KidsSafeServer;
using KidsSafeServer.Auth;

var builder = WebApplication.CreateBuilder(args);

// 允许跨设备访问家长后台（同局域网内用手机/电脑浏览器打开）
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// 故事大全（mxnzp）代理：密钥仅在服务端保存，客户端不直接接触第三方
builder.Services.AddOptions<MxnzpOptions>().Bind(builder.Configuration.GetSection("Mxnzp"));
builder.Services.AddSingleton<MxnzpStoryService>();

var app = builder.Build();
app.UseCors();

// 静态文件：家长后台仪表盘（wwwroot/index.html）、登录页（wwwroot/login.html）
app.UseDefaultFiles();
app.UseStaticFiles();

// ── 数据目录：配置、账号、令牌密钥全部落在 data/ 下，整个目录拷走即可迁移 ──
var dataDir = Path.Combine(app.Environment.ContentRootPath, "data");
var profiles = new ProfileStore(
    Path.Combine(dataDir, "profile.json"),        // 老的单账号配置（未注册时的 default 账号）
    Path.Combine(dataDir, "profiles"));           // 每个正式账号一份 {accountId}.json

var accounts = new AccountStore(Path.Combine(dataDir, "accounts.json"));
var tokenService = new TokenService(
    builder.Configuration["Auth:Secret"],
    Path.Combine(dataDir, "auth.key"));           // 没配密钥就自动生成并持久化，重启不掉线

var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();

// 兼容老配置文件里的顶层 AdminPin（原来是唯一的登录方式）
var legacyPin = builder.Configuration["AdminPin"];
if (!string.IsNullOrWhiteSpace(legacyPin)) authOptions.AdminPin = legacyPin!;

var auth = new AuthService(accounts, tokenService, profiles, authOptions);
var throttle = new LoginThrottle();

// 配置变更信号：家长保存配置后唤醒该账号下所有正在长轮询的儿童设备（见 /api/watch）
var signal = new WatchSignal();

// 启动时间：/api/health 用它算运行时长，便于判断「服务是不是刚重启过」
var startedAt = DateTimeOffset.UtcNow;

// 下载地址：官网 / 应用市场链接，留空表示暂未提供公网下载
var downloadUrl = builder.Configuration["Site:DownloadUrl"] ?? "";
var releasePage = builder.Configuration["Site:ReleasePage"] ?? "";

// ── 身份解析小工具：令牌可以放查询串（老客户端）、Authorization 头（curl / 新客户端） ──
static string? TokenOf(HttpContext ctx, string? query)
{
    if (!string.IsNullOrWhiteSpace(query)) return query;
    var header = ctx.Request.Headers.Authorization.ToString();
    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return header["Bearer ".Length..].Trim();
    return null;
}

AuthSession? SessionOf(HttpContext ctx, string? query) => auth.Resolve(TokenOf(ctx, query));

static object UserPayload(AuthSession s) => new
{
    id = s.AccountId,
    name = s.Name,
    role = s.Role,
    username = s.Username,
    device = s.IsDevice,
    canWrite = s.CanWrite
};

static IResult LoginOk(AuthResult r) => Results.Ok(new
{
    ok = true,
    token = r.Token,
    user = UserPayload(r.Session!)
});

static IResult LoginFail(string? error) =>
    Results.Json(new { ok = false, error }, statusCode: 401);

// ── 登录页用：服务器有没有账号、能不能注册、还支不支持老密码 ──
app.MapGet("/api/auth/status", () => Results.Ok(new
{
    ok = true,
    hasAccounts = auth.Store.Count > 0,
    accountCount = auth.Store.Count,
    registrationOpen = auth.RegistrationOpen,
    pinLoginEnabled = auth.PinLoginEnabled,
    pairCodeMinutes = auth.PairCodeMinutes,
    serverVersion = Releases.ServerVersion,
    latestAppVersion = Releases.Latest.Version
}));

// ── 注册：第一个注册的账号是 owner，并接管老配置 / 老设备令牌 ──
app.MapPost("/api/auth/register", (RegisterRequest req, HttpContext ctx) =>
{
    var result = auth.Register(req.Username, req.Password, req.DisplayName);
    if (!result.Ok)
        return Results.Json(new { ok = false, error = result.Error }, statusCode: 400);

    var first = auth.Store.Count == 1;
    return Results.Ok(new
    {
        ok = true,
        token = result.Token,
        user = UserPayload(result.Session!),
        adoptedLegacyProfile = first,      // 前端可以提示「已把原有配置迁到新账号」
        role = result.Session!.Role
    });
});

// ── 登录：三种请求体都能吃 ──
//   { "username": "...", "password": "..." }  家长账号
//   { "code": "123456", "device": "小明的平板" } 儿童设备配对
//   { "pin": "1234" }                          老客户端（配对码 / 老密码都走这里）
app.MapPost("/api/auth/login", (LoginRequest req, HttpContext ctx) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "local";
    if (throttle.LockedFor(ip) is { } wait)
        return Results.Json(
            new { ok = false, error = $"尝试次数过多，请 {Math.Ceiling(wait.TotalSeconds)} 秒后再试" },
            statusCode: 429);

    var result = !string.IsNullOrWhiteSpace(req.Code)
        ? auth.PairDevice(req.Code, req.Device)
        : !string.IsNullOrWhiteSpace(req.Username)
            ? auth.LoginByPassword(req.Username, req.Password)
            : auth.LoginByPin(req.Pin, req.Device);

    if (!result.Ok)
    {
        throttle.Fail(ip);
        return LoginFail(result.Error);
    }

    throttle.Reset(ip);
    return LoginOk(result);
});

// 当前登录者；登录页/后台刷新时校验令牌是否还有效
app.MapGet("/api/auth/me", (string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    return s is null ? Results.Unauthorized() : Results.Ok(new { ok = true, user = UserPayload(s) });
});

// 退出：令牌是自签的，服务端不留会话，客户端丢掉即可（改密码会让旧令牌自动失效）
app.MapPost("/api/auth/logout", () => Results.Ok(new { ok = true }));

app.MapPost("/api/auth/password", (PasswordRequest req, string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    if (s.IsDevice) return Results.Json(new { ok = false, error = "设备令牌不能修改密码" }, statusCode: 403);

    var error = auth.ChangePassword(s.AccountId, req.OldPassword, req.NewPassword);
    return error is null
        ? Results.Ok(new { ok = true })
        : Results.Json(new { ok = false, error }, statusCode: 400);
});

app.MapPut("/api/auth/profile", (ProfileRequest req, string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    if (s.IsDevice) return Results.Json(new { ok = false, error = "设备令牌不能修改资料" }, statusCode: 403);

    var error = auth.RenameAccount(s.AccountId, req.DisplayName);
    return error is null
        ? Results.Ok(new { ok = true, displayName = req.DisplayName })
        : Results.Json(new { ok = false, error }, statusCode: 400);
});

// ── 设备配对码：家长在后台点一下生成 6 位数字，儿童端填进去即可同步（只读） ──
app.MapGet("/api/auth/paircode", (string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    if (s.IsDevice) return Results.Json(new { ok = false, error = "设备令牌不能生成配对码" }, statusCode: 403);

    var (code, expiresAt, error) = auth.NewPairCode(s.AccountId);
    return error is not null
        ? Results.Json(new { ok = false, error }, statusCode: 400)
        : Results.Ok(new
        {
            ok = true,
            code,
            expiresAt,
            minutes = auth.PairCodeMinutes,
            tip = "在儿童端「账号 → 家长服务器密码」里填这 6 位数字，孩子只能同步配置、不能修改"
        });
});

app.MapGet("/api/auth/devices", (string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    var account = auth.Store.Find(s.AccountId);
    return Results.Ok(new { ok = true, devices = account?.Devices ?? new List<DeviceEntry>() });
});

// 移除设备：该设备的令牌立刻作废（Resolve 时找不到设备记录即拒绝）
app.MapDelete("/api/auth/devices/{id}", (string id, string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    var removed = auth.RemoveDevice(s.AccountId, id);
    return removed
        ? Results.Ok(new { ok = true })
        : Results.NotFound(new { ok = false, error = "没有这台设备" });
});

// ── 配置读写：每个账号一份，设备令牌只读 ──
app.MapGet("/api/settings", (string? token, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    return s is null ? Results.Unauthorized() : Results.Ok(profiles.Load(s.AccountId));
});

app.MapPut("/api/settings", (string? token, ManagedProfile profile, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    if (!s.CanWrite) return Results.Json(new { ok = false, error = "设备只能同步配置，修改请在家长后台" }, statusCode: 403);

    profiles.Save(profile, s.AccountId);
    signal.Pulse(s.AccountId);
    return Results.Ok(new { ok = true, updatedAt = profile.UpdatedAt });
});

// 兼容客户端用 POST 提交
app.MapPost("/api/settings", (string? token, ManagedProfile profile, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();
    if (!s.CanWrite) return Results.Json(new { ok = false, error = "设备只能同步配置，修改请在家长后台" }, statusCode: 403);

    profiles.Save(profile, s.AccountId);
    signal.Pulse(s.AccountId);
    return Results.Ok(new { ok = true, updatedAt = profile.UpdatedAt });
});

// ── 实时下发通道：儿童设备长轮询「配置有没有变」 ──
// 客户端带上自己已知的版本号 since 挂起请求；家长一保存就立即返回 changed=true，
// 客户端收到后调用 /api/settings 拉取并应用。没有变化时最多挂 25 秒再返回，
// 客户端拿到 changed=false 就重新发起，形成「近似推送」的效果。
// 这样既不依赖荣耀/华为等厂商推送通道（个人开发者拿不到），也不需要 WebSocket 中间件。
app.MapGet("/api/watch", async (string? token, long? since, CancellationToken ct, HttpContext ctx) =>
{
    var s = SessionOf(ctx, token);
    if (s is null) return Results.Unauthorized();

    var known = since ?? 0;
    var current = signal.VersionFor(s.AccountId);

    // 来之前已经变过了：立刻返回，不用挂起
    if (current != known)
        return Results.Ok(new { ok = true, version = current, changed = true });

    var latest = await signal.WaitAsync(s.AccountId, TimeSpan.FromSeconds(25), ct);

    // 客户端断开（关页面、切网络、重新发起）：返回空响应收尾，不当成错误
    if (latest is null) return Results.Empty;

    return Results.Ok(new { ok = true, version = latest.Value, changed = latest.Value != known });
});


// ── 健康检查：保留 ok 字段（老调用方依赖），并补充版本与运行状态 ──
app.MapGet("/api/health", () => Results.Ok(new
{
    ok = true,
    server = "KidsSafeServer",
    serverVersion = Releases.ServerVersion,
    startedAt = startedAt,
    uptimeSeconds = (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    watchVersion = signal.PulseCount,    // 所有账号累计的配置变更次数
    pulseCount = signal.PulseCount,
    lastPulseAt = signal.LastPulseUtc,
    accountCount = auth.Store.Count,     // 0 表示还没注册家长账号
    latestAppVersion = Releases.Latest.Version,
    latestAppVersionCode = Releases.Latest.VersionCode
}));

// ── 发布信息：给客户端「检查更新」和官网更新日志用的同一份数据 ──
// 全部来自 Releases.cs，改版本只改那一处。

// 简版（不含要点），用于列表
static object Brief(ReleaseNote r) => new
{
    r.Version,
    r.VersionCode,
    r.Date,
    r.Title,
    r.Tags
};

app.MapGet("/api/app/release", (int? limit) => Results.Ok(new
{
    ok = true,
    channel = Releases.Channel,
    serverVersion = Releases.ServerVersion,
    latest = Releases.Latest,
    history = Releases.Take(limit ?? 0)
}));

// 只要最新一版
app.MapGet("/api/app/release/latest", () => Results.Ok(new
{
    ok = true,
    channel = Releases.Channel,
    latest = Releases.Latest,
    downloadUrl,
    releasePage
}));

// 更新日志：默认全量，?limit=5 只看最近 5 版
app.MapGet("/api/app/changelog", (int? limit) => Results.Ok(new
{
    ok = true,
    total = Releases.All.Length,
    items = Releases.Take(limit ?? 0)
}));

// 客户端检查更新：传入自己的 versionCode，返回是否需要更新
// signatureChanged=true 表示跨越了签名变更点，不能覆盖安装，必须先卸载
app.MapGet("/api/app/update-check", (int versionCode, string? platform) =>
{
    var latest = Releases.Latest;
    var hasUpdate = versionCode < latest.VersionCode;
    return Results.Ok(new
    {
        ok = true,
        platform = platform ?? "android",
        currentVersionCode = versionCode,
        hasUpdate,
        latest = Brief(latest),
        latestHighlights = latest.Highlights,
        signatureChanged = versionCode < Releases.SignatureChangedAt,
        downloadUrl,
        releasePage
    });
});

// 指定版本的详情（版本号可带 v 前缀）
app.MapGet("/api/app/release/{version}", (string version) =>
{
    var note = Releases.Find(version);
    return note is null
        ? Results.NotFound(new { ok = false, error = "未找到该版本：" + version })
        : Results.Ok(new { ok = true, release = note });
});

// ── 安装包下载：扫描 wwwroot/downloads，发版时只要把新包丢进目录就会自动出现 ──
var downloadsDir = Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "downloads");
DownloadCatalog.Ensure(downloadsDir);

app.MapGet("/api/download/list", () =>
{
    var files = DownloadCatalog.List(downloadsDir);
    var android = files.FirstOrDefault(f => f.Platform == "android");
    var windows = files.FirstOrDefault(f => f.Platform == "windows");

    return Results.Ok(new
    {
        ok = true,
        latestAppVersion = Releases.Latest.Version,
        latestVersionCode = Releases.Latest.VersionCode,
        downloadUrl,          // 配置了外部下载地址（应用市场 / 对象存储）时优先用它
        files,
        preferred = new { android = android?.Url, windows = windows?.Url }
    });
});

// 短地址，方便口头告诉家长 / 打印在二维码里
app.MapGet("/download", () => Results.Redirect("/download.html"));
app.MapGet("/docs", () => Results.Redirect("/docs.html"));
app.MapGet("/server", () => Results.Redirect("/docs.html"));
app.MapGet("/login", () => Results.Redirect("/login.html"));

// ── 受控儿童内容 API（仅返回人工审核、安全的目录，不引用任何第三方）──
app.MapGet("/api/content/audio", () => Results.Ok(ContentSeed.Audio()));
app.MapGet("/api/content/video", () => Results.Ok(ContentSeed.Video()));
app.MapGet("/api/content/articles", () => Results.Ok(ContentSeed.Articles()));

// ── 电子课本：wwwroot/books/ 下放清单与原版 PDF，页图由构建时预先渲染 ──
// 为什么不直接给客户端 PDF：安卓 WebView 自己不会渲染 PDF，塞 20MB 原件给手机也慢。
// 这里把每页做成 jpg（120 DPI，约 115KB），客户端只按页取图，翻页快、可离线缓存。
var webRoot = app.Environment.WebRootPath
              ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

app.MapGet("/api/books", () =>
{
    var all = TextbookCatalog.All(webRoot);
    var editions = TextbookCatalog.Editions(webRoot)
        .Select(e => new
        {
            id = e.Id,
            name = e.Name,
            @short = e.Short,
            subject = e.Subject,
            subjectName = e.SubjectName,
            maxGrade = e.MaxGrade,
            books = all.Where(b => b.Edition == e.Id).ToList()
        })
        .Where(e => e.books.Count > 0)   // 一本页图都没有的版本不列出来，免得点开是空的
        .ToList();

    // 科目层：页面先选科目，再选版本。editions 仍然扁平返回，老客户端不受影响（全列出来）。
    var subjects = TextbookCatalog.Subjects(webRoot)
        .Select(s =>
        {
            var mine = all.Where(b => b.Subject == s.Id).ToList();
            return new
            {
                id = s.Id,
                name = s.Name,
                icon = s.Icon,
                total = mine.Count,
                current = mine.Count(b => !b.Legacy),
                editions = editions.Where(e => e.subject == s.Id).Select(e => e.id).ToList()
            };
        })
        .Where(s => s.total > 0)
        .ToList();

    return Results.Ok(new
    {
        ok = true,
        total = all.Count,
        current = all.Count(b => !b.Legacy),
        subjects,
        editions
    });
});

app.MapGet("/api/books/{id}", (string id) =>
{
    var book = TextbookCatalog.Find(webRoot, id);
    return book is null
        ? Results.NotFound(new { ok = false, error = "没有这本课本：" + id })
        : Results.Ok(new { ok = true, book });
});

// eBook 目录页（家长用电脑浏览器也能直接翻）
app.MapGet("/books", () => Results.Redirect("/books.html"));

// ── 故事大全（mxnzp）代理：密钥在服务端，统一缓存 + 限流，归一化后下发 ──
const string StoryUnavailable = "故事服务暂不可用：请确认家长设置中已开启在线内容，且服务器能访问外网。";

app.MapGet("/api/story/types", async (MxnzpStoryService svc) =>
{
    var data = await svc.GetTypesAsync();
    if (data is null) return Results.Problem(StoryUnavailable, statusCode: 503);
    return Results.Ok(new
    {
        Categories = data.Select(x => new { Id = x.type_id, Name = x.name }).ToList()
    });
});

app.MapGet("/api/story/list", async (MxnzpStoryService svc, int typeId, int page = 1) =>
{
    if (page < 1) page = 1;
    var data = await svc.GetListAsync(typeId, page);
    if (data is null) return Results.Problem(StoryUnavailable, statusCode: 503);
    return Results.Ok(new
    {
        Items = data.Select(x => new
        {
            Id = x.storyId,
            x.title,
            x.type,
            x.length,
            x.readTime,
            x.shortDesc
        }).ToList(),
        Page = page,
        HasMore = data.Count >= 10
    });
});

app.MapGet("/api/story/detail", async (MxnzpStoryService svc, long storyId) =>
{
    var d = await svc.GetDetailAsync(storyId);
    if (d is null) return Results.Problem(StoryUnavailable, statusCode: 503);
    return Results.Ok(new
    {
        Id = d.storyId,
        d.title,
        d.type,
        d.length,
        d.readTime,
        d.content
    });
});

app.MapGet("/api/story/search", async (MxnzpStoryService svc, string keyword, int page = 1) =>
{
    if (page < 1) page = 1;
    var data = await svc.SearchAsync(keyword, page);
    if (data is null) return Results.Problem(StoryUnavailable, statusCode: 503);
    return Results.Ok(new
    {
        Items = data.Select(x => new
        {
            Id = x.storyId,
            x.title,
            x.type,
            x.length,
            x.readTime,
            x.shortDesc
        }).ToList(),
        Page = page,
        HasMore = data.Count >= 10
    });
});

// 监听所有网卡，方便局域网内其他设备访问。
// 默认 5180；同一个项目要开第二个实例做验证时，用环境变量 KS_URLS=http://127.0.0.1:5199 换端口，
// 免得和自己的调试进程抢端口（app.Urls 一旦 Add 就会盖掉 ASPNETCORE_URLS / --urls）。
var urls = Environment.GetEnvironmentVariable("KS_URLS");
app.Urls.Add(string.IsNullOrWhiteSpace(urls) ? "http://0.0.0.0:5180" : urls);

// 启动提示：很多人第一次运行时不知道密码是什么，直接在控制台说清楚
var lan = app.Urls.FirstOrDefault() ?? "http://127.0.0.1:5180";
Console.WriteLine("──────────────────────────────────────────────");
Console.WriteLine($" 儿童安全桌面 · 家长服务端 {Releases.ServerVersion}");
Console.WriteLine($"  后台地址：{lan}/  （登录页 {lan}/login.html）");
Console.WriteLine(auth.Store.Count == 0
    ? $"  还没有家长账号：打开网页注册；也可以先用老密码 {authOptions.AdminPin} 登录"
    : $"  已注册 {auth.Store.Count} 个家长账号：请用账号密码登录；(老密码登录已{(auth.PinLoginEnabled ? "开启" : "关闭")})");
Console.WriteLine("──────────────────────────────────────────────");

app.Run();

public record LoginRequest(string? Pin, string? Username = null, string? Password = null, string? Code = null, string? Device = null);

public record RegisterRequest(string? Username, string? Password, string? DisplayName);

public record PasswordRequest(string? OldPassword, string? NewPassword);

public record ProfileRequest(string? DisplayName);
