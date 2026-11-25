using KidsSafeDesktop.Helpers;
using Microsoft.Maui.Controls;
using System.Text;
using System.Text.Json;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 「小游戏工坊」：用一个 WebView 装一份 H5 编辑器（四端共用 Resources/Raw/Maker/index.html）。
///
/// 页面自己能做的（拖拽、内置素材、存档、试玩）全在网页里；
/// 只有两件事必须宿主代劳，因为网页做不到：
///   1. 调系统选图器（FilePicker）把本地图片喂进页面；
///   2. 把作品存成文件发出去（Share），以及从文件读回来。
/// 桥接口见 index.html 里的 <c>window.KS</c>。
/// </summary>
public partial class GameMakerPage : ContentPage
{
    private bool _loaded;

    /// <summary>单张图片上限：超过就劝退。网页会把图压到 320px 再存，这里只是防止把 10MB 原图
    /// 当字符串塞进 JS 桥（安卓上会明显卡顿甚至崩）。</summary>
    private const long MaxImageBytes = 8L * 1024 * 1024;

    public GameMakerPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        MakerView.Navigating += OnNavigating;
        ConfigurePlatformWebView();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        LoadingMask.IsVisible = true;
        try
        {
            var url = await Task.Run(() => MakerAssets.EnsureExtractedAsync());
            MakerView.Source = new UrlWebViewSource { Url = url };
        }
        finally
        {
            LoadingMask.IsVisible = false;
        }
    }

    // ===== 工具栏 =====
    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    private void OnReload(object sender, EventArgs e) => MakerView.Reload();

    // ===== 导入素材 =====
    private async void OnImportAsset(object sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "选一张图片当素材",
                FileTypes = FilePickerFileType.Images
            });
            if (file is null) return;

            LoadingLabel.Text = "🖼 正在读取图片…";
            LoadingMask.IsVisible = true;

            byte[] bytes;
            await using (var src = await file.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await src.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            if (bytes.LongLength == 0)
            {
                Toast("这张图读不出来，换一张试试");
                return;
            }
            if (bytes.LongLength > MaxImageBytes)
            {
                Toast($"这张图太大了（{bytes.LongLength / 1024 / 1024} MB），换张小一点的");
                return;
            }

            // 先在宿主侧缩到 480px：网页还会再压到 320px 存档，
            // 这一步只为把过桥的字符串从几 MB 降到几十 KB。
            bytes = ImageShrinker.TryShrink(bytes, 480) ?? bytes;

            // 有些平台给的 ContentType 是 application/octet-stream（尤其是从文件管理器选的），
            // 而页面只收 image/*：不顺带纠正的话会「点了没反应」，最难查。
            var declared = file.ContentType ?? string.Empty;
            var mime = declared.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? declared
                : "image/png";
            var dataUrl = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);

            var payload = JsonSerializer.Serialize(new
            {
                name = file.FileName,
                mime,
                dataUrl
            });

            await WebViewBridge.EvaluateAsync(MakerView, $"window.KS.importAsset({payload})");
        }
        catch (Exception ex)
        {
            AppLog.W("工坊", "导入素材失败：" + ex.Message);
            Toast("导入没成功：" + ex.Message);
        }
        finally
        {
            LoadingMask.IsVisible = false;
        }
    }

    // ===== 作品导入导出 =====
    private async void OnImportProject(object sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "选一个作品文件（.json）"
            });
            if (file is null) return;

            string text;
            await using (var src = await file.OpenReadAsync())
            using (var reader = new StreamReader(src, Encoding.UTF8))
            {
                text = await reader.ReadToEndAsync();
            }
            if (string.IsNullOrWhiteSpace(text)) return;

            // 网页那边按 UTF-8 解 base64，这里必须先转成 UTF-8 字节，
            // 直接 Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) 会丢掉 BOM 顺序问题
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            var jsonLiteral = JsonSerializer.Serialize(b64);
            var result = await WebViewBridge.EvaluateAsync(
                MakerView, $"window.KS.loadProjectBase64({jsonLiteral})");

            if (result == "ok") Toast("已打开：" + file.FileName);
            else if (result is not null) Toast("这个文件不是作品：" + result);
        }
        catch (Exception ex)
        {
            AppLog.W("工坊", "导入作品失败：" + ex.Message);
            Toast("打开失败：" + ex.Message);
        }
    }

    private async void OnExportProject(object sender, EventArgs e)
    {
        try
        {
            var b64 = await WebViewBridge.EvaluateAsync(MakerView, "window.KS.currentProjectBase64()");
            if (string.IsNullOrEmpty(b64))
            {
                Toast("先打开或新建一个作品再导出");
                return;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            var name = ReadProjectName(json);
            var safe = Sanitize(name);
            var path = Path.Combine(FileSystem.CacheDirectory, safe + ".json");
            await File.WriteAllTextAsync(path, json, new UTF8Encoding(false));

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "导出作品",
                File = new ShareFile(path, "application/json")
            });
        }
        catch (Exception ex)
        {
            AppLog.W("工坊", "导出作品失败：" + ex.Message);
            Toast("导出失败：" + ex.Message);
        }
    }

    private static string ReadProjectName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("name", out var n))
            {
                var s = n.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s!;
            }
        }
        catch { /* 坏文件就当没名字 */ }
        return "我的小游戏";
    }

    private static string Sanitize(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
        }
        var s = sb.ToString().Trim();
        if (s.Length == 0) s = "我的小游戏";
        return s.Length > 60 ? s[..60] : s;
    }

    private void Toast(string text)
    {
        // 没有引第三方 Toast 组件，直接用系统弹窗之外的轻量提示：这里改成状态标签
        LoadingLabel.Text = text;
        LoadingMask.IsVisible = false;
        AppLog.I("工坊", text);
        _ = ShowHintAsync(text);
    }

    private async Task ShowHintAsync(string text)
    {
        try { await DisplayAlert("小游戏工坊", text, "知道了"); }
        catch { /* 页面已关 */ }
    }

    // ===== WebView 生命周期 =====
    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // 只放行本地文件：作品里可能有外链，一律交给系统浏览器
        if (e.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return;

        e.Cancel = true;
        _ = OpenExternalAsync(e.Url);
    }

    private static async Task OpenExternalAsync(string url)
    {
        try { await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(url); }
        catch { /* 打不开就算了 */ }
    }

    private void ConfigurePlatformWebView()
    {
        MakerView.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (MakerView.Handler?.PlatformView is Android.Webkit.WebView awv)
            {
                var s = awv.Settings;
                s.JavaScriptEnabled = true;
                s.DomStorageEnabled = true;      // 作品就存在 localStorage 里，少了它一切都不保存
                s.AllowFileAccess = true;
                s.AllowContentAccess = true;
                s.AllowFileAccessFromFileURLs = true;
                s.MediaPlaybackRequiresUserGesture = false;

                s.UseWideViewPort = true;
                s.LoadWithOverviewMode = false;
                s.SetSupportZoom(false);
                s.BuiltInZoomControls = false;
                s.DisplayZoomControls = false;
                s.TextZoom = 100;

                awv.OverScrollMode = Android.Views.OverScrollMode.Never;
            }
#endif
        };
    }

    protected override bool OnBackButtonPressed()
    {
        // 网页内部有「返回大厅」按钮，但安卓物理返回键不能直接退出应用，
        // 交给页面自己判断当前在编辑还是在大厅。
        WebViewBridge.FireAndForget(MakerView, "window.KS.goLibrary && window.KS.goLibrary();");
        return true;
    }
}
