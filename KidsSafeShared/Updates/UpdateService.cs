using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;
using Microsoft.Maui.ApplicationModel;

namespace KidsSafeDesktop.Updates;

/// <summary>
/// 增量更新的客户端实现。流程：要清单 → 验签 → 校验字段 → 只下差异文件 →
/// 逐个验 SHA-256 → 写落盘计划 → 交给平台执行器。
///
/// 几个刻意的取舍：
///   · 补丁不打断正在使用的孩子：下载在后台，生效放到「下次关闭 / 家长点更新」。
///   · 任何一步失败都只是「这次没更新」，绝不抛出到界面、绝不影响原有功能。
///   · 没有签名、路径不合法、版本更低 —— 一律整份丢弃，不做「部分安装」。
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string Category = "增量更新";

    /// <summary>整份补丁允许的最大体积，与验签层保持一致。</summary>
    private const long MaxTotalBytes = 512L * 1024 * 1024;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISettingsService _settings;
    private readonly object _gate = new();

    private UpdateManifest? _manifest;
    private string? _filesBase;
    private CancellationTokenSource? _cts;

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpdateService(ISettingsService settings)
    {
        _settings = settings;
    }

    public UpdateState State { get; private set; } = UpdateState.Idle;

    public string Status { get; private set; } = string.Empty;

    public string? AvailableVersion { get; private set; }

    public string? Notes { get; private set; }

    public double Progress { get; private set; }

    public string? LastResult { get; private set; }

    public bool HasPendingPatch
    {
        get
        {
            try
            {
#if ANDROID
                // Android 没有「待落盘」这回事：补丁下载完就写 active.json，
                // 之后每次冷启动都要预加载它。pending=true 表示「还没成功启动过一次」。
                var active = Path.Combine(UpdatePaths.WorkRoot, "active.json");
                if (!File.Exists(active))
                {
                    return false;
                }
                using var doc = JsonDocument.Parse(File.ReadAllBytes(active));
                return doc.RootElement.TryGetProperty("pending", out var pending) &&
                       pending.ValueKind == JsonValueKind.True;
#else
                return Directory.Exists(UpdatePaths.WorkRoot) &&
                       Directory.EnumerateDirectories(UpdatePaths.WorkRoot)
                                .Any(d => File.Exists(Path.Combine(d, "ready.flag")));
#endif
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    // ── 生命周期 ────────────────────────────────────────────────────────────

    public void Initialize()
    {
        try
        {
            CleanupOld();
            ReportLastResult();

            if (HasPendingPatch)
            {
                // 上次下载好了但还没装（比如中途断电）。不要重新下载，直接等生效。
                State = UpdateState.Ready;
                SetStatus(OperatingSystem.IsAndroid()
                    ? "有一个更新在等下次启动生效"
                    : "有一个更新在等下次关闭时安装");
            }
        }
        catch (Exception ex)
        {
            AppLog.W(Category, "初始化失败：" + ex.Message, ex);
        }
    }

    // ── 检查 ────────────────────────────────────────────────────────────────

    public async Task<bool> CheckAsync(CancellationToken ct = default)
    {
        if (State is UpdateState.Downloading or UpdateState.Applying)
        {
            return false;
        }
        // 已经有补丁等着生效，就别再下一份：等它装完再说，省流量也少一类状态。
        if (State == UpdateState.Ready)
        {
            SetStatus(OperatingSystem.IsAndroid()
                ? "更新已下载，下次启动生效"
                : "更新已下载，下次关闭时自动安装");
            return false;
        }

        var baseUrl = ServerBaseUrl();
        if (baseUrl is null)
        {
            SetStatus("没有连接家长服务器，不检查更新");
            return false;
        }

        SetState(UpdateState.Checking);
        SetStatus("正在检查更新…");
        SetProgress(0);

        try
        {
            var url = $"{baseUrl}/api/app/patch?platform={UpdatePaths.PlatformKey}" +
                      $"&from={Uri.EscapeDataString(UpdatePaths.HostVersion)}" +
                      $"&code={UpdatePaths.HostCode}";

            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Fail($"检查更新失败：服务器返回 HTTP {(int)response.StatusCode}");
            }

            var dto = await response.Content
                                    .ReadFromJsonAsync<PatchResponse>(JsonOptions, ct)
                                    .ConfigureAwait(false);
            if (dto is null || !dto.Ok)
            {
                return Fail("检查更新失败：服务器返回内容无法识别");
            }
            if (!dto.Available || string.IsNullOrWhiteSpace(dto.Manifest) ||
                string.IsNullOrWhiteSpace(dto.Signature))
            {
                SetState(UpdateState.UpToDate);
                SetStatus("已是最新版本");
                AppLog.I(Category, $"已是最新（{UpdatePaths.HostVersion}）");
                return false;
            }

            var body = Convert.FromBase64String(dto.Manifest);
            var signature = Convert.FromBase64String(dto.Signature);

            // 先验签、再解析 —— 顺序不能反。
            if (!UpdateVerifier.Verify(body, signature))
            {
                return Fail("更新包签名无效，已拒绝（服务器可能不是官方服务器）");
            }

            var manifest = UpdateManifest.Parse(body);
            if (manifest is null)
            {
                return Fail("更新清单格式不对");
            }

            var invalid = UpdateVerifier.Validate(manifest, UpdatePaths.PlatformKey,
                                                 UpdatePaths.HostVersion, UpdatePaths.HostCode,
                                                 DateTimeOffset.UtcNow);
            if (invalid is not null)
            {
                // 这不是故障，是「增量不适用」：比如壳工程接口变了、或跨了多个版本。
                // 界面据此提示「需要完整更新」，而不是「更新失败」。
                SetState(UpdateState.UpToDate);
                SetStatus("需要完整安装包更新：" + invalid);
                AppLog.I(Category, "增量不适用：" + invalid);
                return false;
            }

            _manifest = manifest;
            // 文件根地址由服务端给（可以挂到 CDN / 对象存储），没给就按约定拼。
            _filesBase = string.IsNullOrWhiteSpace(dto.BaseUrl)
                ? $"patches/{manifest.Platform}/current/files/"
                : dto.BaseUrl;
            AvailableVersion = manifest.Version;
            Notes = manifest.Notes;
            Raise(nameof(AvailableVersion));
            Raise(nameof(Notes));
            SetState(UpdateState.Available);
            SetStatus($"发现新版本 {manifest.Version}");
            AppLog.I(Category, $"发现增量更新 {manifest.Version}（{manifest.Files.Count} 个文件）");
            return true;
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.Idle);
            SetStatus("已取消");
            return false;
        }
        catch (Exception ex)
        {
            return Fail("检查更新失败：" + ex.Message);
        }
    }

    // ── 下载 ────────────────────────────────────────────────────────────────

    public async Task<bool> DownloadAsync(CancellationToken ct = default)
    {
        var manifest = _manifest;
        if (manifest is null)
        {
            return false;
        }
        if (State is UpdateState.Downloading or UpdateState.Applying)
        {
            return false;
        }

        var baseUrl = ServerBaseUrl();
        if (baseUrl is null)
        {
            return Fail("没有连接家长服务器");
        }

        var versionDir = UpdatePaths.VersionDir(manifest.Version);
        var payloadDir = UpdatePaths.PayloadDir(manifest.Version);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            Directory.CreateDirectory(payloadDir);

            // 先算差异：本地已经有同哈希的文件就不下载。
            // 这一步让「只改了一个 dll」的补丁真的只下那一个文件。
            // 比对基准：Windows 是安装目录；Android 要拿「当前生效的补丁」比，
            // 因为 APK 里那几个 dll 永远不会跟着补丁变，拿它们比会导致每次启动都重下。
            var diffRoot = DiffRoot();
            var pending = new List<UpdateFile>();
            long total = 0;
            foreach (var file in manifest.Files)
            {
                var local = UpdateVerifier.ResolveUnder(diffRoot, file.Path);
                if (local is null)
                {
                    return Fail("补丁里有不合法路径：" + file.Path);
                }
                if (File.Exists(local) &&
                    UpdateVerifier.Sha256OfFile(local).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                pending.Add(file);
                total += file.Size;
            }

            if (pending.Count == 0 && manifest.Removed.Count == 0)
            {
                SetState(UpdateState.UpToDate);
                SetStatus("内容已经是最新的，不需要下载");
                return false;
            }
            if (total > MaxTotalBytes)
            {
                return Fail("补丁体积异常，已放弃");
            }

            SetState(UpdateState.Downloading);
            SetStatus($"正在下载 {pending.Count} 个文件…");
            SetProgress(0);

            var filesBase = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"),
                                    _filesBase ?? $"patches/{manifest.Platform}/current/files/");
            long done = 0;
            foreach (var file in pending)
            {
                _cts.Token.ThrowIfCancellationRequested();

                var dest = Path.Combine(payloadDir, file.Path.Replace('\\', '/'));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                await DownloadOneAsync(new Uri(filesBase, file.Path.Replace('\\', '/')), dest,
                                       file, bytes =>
                                       {
                                           var current = done + bytes;
                                           SetProgress(total <= 0 ? 0 : Math.Min(1.0, current / (double)total));
                                       },
                                       _cts.Token).ConfigureAwait(false);

                done += file.Size;
                AppLog.I(Category, $"已下载 {file.Path}（{file.Size / 1024} KB）");
            }

            WritePlan(manifest, versionDir);

#if ANDROID
            // Android 没有「退出时落盘」：补丁下载完就可以写激活标记，
            // 真正的生效点是下一次冷启动（PatchBootstrapper 预加载）。
            WriteActiveMarker(manifest);
            SetProgress(1);
            SetState(UpdateState.Ready);
            SetStatus($"更新已下载（{FormatSize(total)}），下次启动生效");
#else
            await File.WriteAllTextAsync(Path.Combine(versionDir, "ready.flag"), string.Empty,
                                         _cts.Token).ConfigureAwait(false);
            SetProgress(1);
            SetState(UpdateState.Ready);
            SetStatus($"更新已就绪（{FormatSize(total)}），下次关闭时自动安装");
#endif
            AppLog.I(Category, $"补丁已就绪：{manifest.Version}，{pending.Count} 个文件，{total} 字节");
            return true;
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.Idle);
            SetStatus("已取消下载");
            return false;
        }
        catch (Exception ex)
        {
            return Fail("下载更新失败：" + ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ── 生效 ────────────────────────────────────────────────────────────────

    public async Task<bool> ApplyAsync(bool restart)
    {
        var manifest = _manifest ?? ReadManifestFromDisk();
        if (manifest is null || !HasPendingPatch)
        {
            SetStatus("没有待安装的更新");
            return false;
        }

        var versionDir = UpdatePaths.VersionDir(manifest.Version);
        SetState(UpdateState.Applying);

#if ANDROID
        // Android：写激活标记，下次冷启动由 PatchBootstrapper 预加载。
        // 这里不做「立即重启」—— 安卓进程被系统杀掉的时机不可控，
        // 与其在半路替换，不如老老实实等下一次冷启动。
        try
        {
            WriteActiveMarker(manifest);
            SetState(UpdateState.Ready);
            SetStatus("更新已准备好，重启应用后生效");
            AppLog.I(Category, "已写入 Android 补丁激活标记：" + manifest.Version);
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            return Fail("准备更新失败：" + ex.Message);
        }
#else
        var updater = UpdatePaths.UpdaterPath;
        if (!File.Exists(updater))
        {
            return Fail($"缺少更新组件 {UpdatePaths.UpdaterExe}，请用完整安装包更新");
        }

        try
        {
            var args = new StringBuilder()
                .Append("--apply \"").Append(versionDir).Append('"')
                .Append(" --app-dir \"").Append(UpdatePaths.InstallDir).Append('"')
                .Append(" --pid ").Append(Environment.ProcessId);
            if (restart)
            {
                args.Append(" --restart \"").Append(Environment.ProcessPath ?? string.Empty).Append('"');
            }

            Process.Start(new ProcessStartInfo(updater, args.ToString())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = UpdatePaths.InstallDir
            });

            SetStatus(restart ? "正在重启并安装更新…" : "退出后自动安装更新");
            AppLog.I(Category, $"已启动落盘器：{updater} {args}");

            if (restart)
            {
                // 给落盘器一点时间起来，再退出，避免它还没拿到句柄就被父进程退出影响。
                await Task.Delay(400).ConfigureAwait(false);
                MainThread.BeginInvokeOnMainThread(() =>
                    Microsoft.Maui.Controls.Application.Current?.Quit());
            }
            return true;
        }
        catch (Exception ex)
        {
            return Fail("启动更新程序失败：" + ex.Message);
        }
#endif
    }

    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (Exception)
        {
            // 已释放/已取消：忽略
        }
    }

    // ── 内部 ────────────────────────────────────────────────────────────────

    /// <summary>服务端地址（去掉尾部斜杠）；未启用或非法返回 null。</summary>
    private string? ServerBaseUrl()
    {
        var current = _settings.Current;
        if (!current.ServerEnabled || string.IsNullOrWhiteSpace(current.ServerUrl))
        {
            return null;
        }
        var url = current.ServerUrl.Trim().TrimEnd('/');
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return url;
    }

    /// <summary>
    /// 「本机现在实际在用的文件」根目录。
    /// Windows：就是安装目录。
    /// Android：如果已有生效的补丁，要拿补丁目录比 —— APK 里的 dll 永远是出厂那份，
    /// 拿它比会让每次启动都重下一遍同样的文件。
    /// </summary>
    private static string DiffRoot()
    {
#if ANDROID
        try
        {
            var active = Path.Combine(UpdatePaths.WorkRoot, "active.json");
            if (File.Exists(active))
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(active));
                if (doc.RootElement.TryGetProperty("directory", out var dir) &&
                    dir.GetString() is { Length: > 0 } relative)
                {
                    var root = Path.GetFullPath(Path.Combine(UpdatePaths.WorkRoot, relative));
                    if (Directory.Exists(root))
                    {
                        return root;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 读不出来就当没有补丁，退回安装目录
        }
#endif
        return UpdatePaths.InstallDir;
    }

    /// <summary>下载单个文件，边下边报进度，落盘后再验一次 SHA-256。</summary>
    private async Task DownloadOneAsync(Uri url, string destination, UpdateFile expected,
                                        Action<long> onBytes, CancellationToken ct)
    {
        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                                       .ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new IOException($"下载 {expected.Path} 失败：HTTP {(int)response.StatusCode}");
            }

            await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write,
                                                     FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    received += read;
                    onBytes(received);
                }
            }
        }

        var actual = UpdateVerifier.Sha256OfFile(destination);
        if (!actual.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(destination); } catch (Exception) { /* 下次覆盖 */ }
            throw new InvalidDataException($"{expected.Path} 校验失败（文件可能被改动过）");
        }
    }

    /// <summary>落盘计划：落盘器只认这个文件，不在计划里的东西一概不动。</summary>
    private static void WritePlan(UpdateManifest manifest, string versionDir)
    {
        var plan = new ApplyPlan
        {
            Version = manifest.Version,
            VersionCode = manifest.VersionCode,
            Files = manifest.Files
                             .Select(f => new ApplyPlanFile { Path = f.Path, Sha256 = f.Sha256 })
                             .ToList(),
            Removed = manifest.Removed.ToList()
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(plan, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        File.WriteAllBytes(Path.Combine(versionDir, "plan.json"), json);
    }

    /// <summary>Android 的激活标记：告诉 PatchBootstrapper 下次启动要预加载哪些 dll。</summary>
    private static void WriteActiveMarker(UpdateManifest manifest)
    {
        var assemblies = manifest.Files
                                 .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                 .Select(f => f.Path.Replace('\\', '/'))
                                 .ToList();
        if (assemblies.Count == 0)
        {
            throw new InvalidOperationException("这个补丁里没有可加载的程序集");
        }

        var marker = new ActivePatch
        {
            Version = manifest.Version,
            // 目录要指到 payload：下载的文件在那里，PatchBootstrapper 直接按这个相对路径拼
            Directory = $"{manifest.Version}/payload",
            Files = assemblies,
            Attempts = 0,
            Pending = true
        };
        var path = Path.Combine(UpdatePaths.WorkRoot, "active.json");
        Directory.CreateDirectory(UpdatePaths.WorkRoot);
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(marker, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    /// <summary>进程重启后 _manifest 丢了，从磁盘把清单还原出来。</summary>
    private UpdateManifest? ReadManifestFromDisk()
    {
        try
        {
            var candidates = Directory.EnumerateDirectories(UpdatePaths.WorkRoot)
                                      .Select(d => new FileInfo(Path.Combine(d, "plan.json")))
                                      .Where(f => f.Exists)
                                      .OrderByDescending(f => f.LastWriteTimeUtc);
            foreach (var candidate in candidates)
            {
                var parsed = JsonSerializer.Deserialize<ApplyPlan>(File.ReadAllBytes(candidate.FullName), JsonOptions);
                if (parsed is null)
                {
                    continue;
                }
                return new UpdateManifest
                {
                    Schema = 1,
                    Platform = UpdatePaths.PlatformKey,
                    Version = parsed.Version,
                    VersionCode = parsed.VersionCode,
                    Files = parsed.Files
                                    .Select(f => new UpdateFile { Path = f.Path, Sha256 = f.Sha256 })
                                    .ToList(),
                    Removed = parsed.Removed ?? new List<string>()
                };
            }
        }
        catch (Exception ex)
        {
            AppLog.W(Category, "读取落盘计划失败：" + ex.Message, ex);
        }
        return null;
    }

    /// <summary>读落盘器留下的结果，显示给家长，顺便清掉已经处理完的目录。</summary>
    public void ReportLastResult()
    {
        try
        {
            if (!Directory.Exists(UpdatePaths.WorkRoot))
            {
                return;
            }

            var results = Directory.EnumerateDirectories(UpdatePaths.WorkRoot)
                                   .Select(d => new FileInfo(Path.Combine(d, "result.json")))
                                   .Where(f => f.Exists)
                                   .OrderByDescending(f => f.LastWriteTimeUtc)
                                   .ToList();
            if (results.Count == 0)
            {
                return;
            }

            var newest = results[0];
            var json = JsonSerializer.Deserialize<ApplyResult>(File.ReadAllBytes(newest.FullName), JsonOptions);
            if (json is not null)
            {
                LastResult = json.Ok
                    ? $"已更新到 {json.Version}" + (json.RestartRequired ? "（下次启动生效）" : string.Empty)
                    : $"上次更新没有完成：{json.Message}（已回滚，仍在使用旧版本）";
                Raise(nameof(LastResult));
                AppLog.I(Category, "上次落盘结果：" + LastResult);
            }

            // 处理完的目录直接删掉：里面是几百 MB 的 payload，留着只会占地方。
            foreach (var dir in results.Select(f => f.Directory!).Distinct())
            {
                TryDelete(dir.FullName);
            }
        }
        catch (Exception ex)
        {
            AppLog.W(Category, "读取更新结果失败：" + ex.Message, ex);
        }
    }

    /// <summary>超过 7 天的暂存目录一律清掉（断电/崩溃留下的半成品）。</summary>
    private static void CleanupOld()
    {
        try
        {
            if (!Directory.Exists(UpdatePaths.WorkRoot))
            {
                return;
            }
            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var dir in Directory.EnumerateDirectories(UpdatePaths.WorkRoot))
            {
                var info = new DirectoryInfo(dir);
                // 有 ready.flag 但没 result.json 的是「等着装」的，不能删
                if (info.LastWriteTimeUtc < cutoff && !File.Exists(Path.Combine(dir, "ready.flag")))
                {
                    TryDelete(dir);
                }
            }
        }
        catch (Exception)
        {
            // 清理失败不影响主流程
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception)
        {
            // 文件被占用 / 权限不足：留着，下次再说
        }
    }

    private bool Fail(string message)
    {
        SetState(UpdateState.Failed);
        SetStatus(message);
        AppLog.W(Category, message);
        return false;
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    private void SetState(UpdateState state)
    {
        if (State == state)
        {
            return;
        }
        State = state;
        Raise(nameof(State));
    }

    private void SetStatus(string status)
    {
        Status = status;
        Raise(nameof(Status));
    }

    private void SetProgress(double value)
    {
        Progress = value;
        Raise(nameof(Progress));
    }

    /// <summary>属性变更一律回主线程，界面绑定不需要自己包一层。</summary>
    private void Raise(string property)
    {
        if (MainThread.IsMainThread)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property)));
        }
    }

    // ── 传输对象 ────────────────────────────────────────────────────────────

    private sealed class PatchResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("available")] public bool Available { get; set; }
        [JsonPropertyName("manifest")] public string? Manifest { get; set; }
        [JsonPropertyName("signature")] public string? Signature { get; set; }
        [JsonPropertyName("baseUrl")] public string? BaseUrl { get; set; }
    }
}

/// <summary>落盘计划，与 KidsSafeUpdater 共用的格式（字段名改动要两边一起改）。</summary>
public sealed class ApplyPlan
{
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("versionCode")] public int VersionCode { get; set; }
    [JsonPropertyName("files")] public List<ApplyPlanFile> Files { get; set; } = new();
    [JsonPropertyName("removed")] public List<string>? Removed { get; set; }
}

public sealed class ApplyPlanFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

/// <summary>落盘结果，由 KidsSafeUpdater 写、主程序读。</summary>
public sealed class ApplyResult
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("restartRequired")] public bool RestartRequired { get; set; }
    [JsonPropertyName("appliedAt")] public DateTimeOffset AppliedAt { get; set; }
}

/// <summary>Android 激活标记，由 PatchBootstrapper 读（它不能依赖这个类，字段名要对齐）。</summary>
public sealed class ActivePatch
{
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    /// <summary>相对 patches 目录的补丁位置，如 "1.21/payload"。</summary>
    [JsonPropertyName("directory")] public string Directory { get; set; } = string.Empty;
    [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
    /// <summary>连续启动失败计数；成功启动后由 PatchBootstrapper 归零。</summary>
    [JsonPropertyName("attempts")] public int Attempts { get; set; }
    /// <summary>true 表示刚下载、还没成功启动过一次（界面据此显示「等下次启动生效」）。</summary>
    [JsonPropertyName("pending")] public bool Pending { get; set; }
}
