using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 诊断日志页：直接在应用里看 nav.log，不用再去手机目录里翻文件。
/// 真机上取日志最省事的路子就是这个页面 —— 截图或「复制全部」发给开发者即可。
/// </summary>
public partial class DiagnosticsPage : ContentPage
{
    public DiagnosticsPage()
    {
        InitializeComponent();

#if !ANDROID
        // 推送只接在安卓端，Windows 上这个按钮没有意义
        RetryPushButton.IsVisible = false;
#endif
    }

    private DateTime _lastRetry = DateTime.MinValue;

    private async void OnRetryPush(object sender, EventArgs e)
    {
#if ANDROID
        var wait = 30 - (int)(DateTime.Now - _lastRetry).TotalSeconds;
        if (wait > 0)
        {
            StatusLabel.Text = $"荣耀侧有频控，请等 {wait} 秒再试";
            return;
        }

        _lastRetry = DateTime.Now;
        StatusLabel.Text = "正在重新申请 token…";

        try
        {
            HonorPush.Restart();
            await Task.Delay(2500);   // 给 SDK 的回调留点时间
            Refresh();
            StatusLabel.Text = HonorPush.Token is null
                ? "仍未取到，看上面「最近错误」"
                : "已取到 token 🎉";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "重试失败：" + ex.Message;
        }
#else
        await Task.CompletedTask;
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AppLog.I("诊断", "打开诊断日志页");
        Refresh();
    }

    /// <summary>当前显示的是不是「运行日志」；false 表示在看 nav.log / crash.log。</summary>
    private bool _showRuntime = true;

    private void Refresh()
    {
        try
        {
            // 路径
            PathLabel.Text = DiagLog.Dir;
            PathHintLabel.Text = DiagLog.IsExternal
                ? "路径形如 Android/data/<包名>/files/logs（卸载应用时自动清除）"
                : "当前写在应用内部私有目录，手机文件管理器看不到 —— 请用本页「复制」或「导出」拿到手。";
            PathHintLabel.IsVisible = true;

            // 推送状态
#if ANDROID
            var kv = new List<string>
            {
                "是否支持：" + (HonorPush.IsSupported ? "是" : "否（非荣耀设备 / 未装荣耀推送服务，属正常）"),
                "token：" + (HonorPush.Token ?? "(未取到)"),
                "最近错误：" + HonorPushCodes.Explain(HonorPush.LastError),
                "最近消息：" + (HonorPush.LastMessage ?? "(无)")
            };
            PushLabel.Text = string.Join("\n", kv);

            // 包名与签名指纹：10202 时用这两个值和荣耀平台逐字核对
            var sha = AppSignature.Sha256;
            SignLabel.Text =
                "包名：" + AppSignature.PackageName + "\n" +
                "App ID：" + (AppSignature.HonorAppId ?? "(未读到)") + "\n" +
                "推送 SDK：" + (AppSignature.HonorSdkVersion ?? "(未读到)") + "\n" +
                "SHA-256：" + (sha ?? "(读取失败)") + "\n" +
                "去冒号：" + (AppSignature.Sha256Plain ?? "(读取失败)");
#else
            PushLabel.Text = "仅安卓端接入荣耀推送；Windows 端不适用。";
            SignLabel.Text = "仅安卓端需要指纹登记。";
#endif

            // 实时下发通道（全平台）
            try
            {
                var live = ServiceProvider.Get<ILiveSyncService>();
                var st = ServiceProvider.Get<ISettingsService>();
                LiveLabel.Text =
                    "状态：" + live.Status + "\n" +
                    "循环运行：" + (live.IsRunning ? "是" : "否") + "\n" +
                    "服务器：" + (string.IsNullOrWhiteSpace(st.Current.ServerUrl) ? "(未填写)" : st.Current.ServerUrl) +
                    " · 启用=" + (st.Current.ServerEnabled ? "是" : "否") +
                    " · 已登录=" + (string.IsNullOrWhiteSpace(st.Current.ServerToken) ? "否" : "是") +
                    "\n说明：家长在服务器上改完配置，这里会在 1 秒内变成「已更新 N 次」。";
            }
            catch (Exception ex)
            {
                LiveLabel.Text = "读取失败：" + ex.Message;
            }

            // 日志正文
            RenderLog();
        }
        catch (Exception ex)
        {
            LogLabel.Text = "读取失败：" + ex;
            StatusLabel.Text = "";
        }
    }

    /// <summary>按当前 tab 渲染日志正文。</summary>
    private void RenderLog()
    {
        if (_showRuntime)
        {
            var text = AppLog.Tail();
            LogLabel.Text = string.IsNullOrWhiteSpace(text)
                ? "(运行日志还没有内容 —— 正常使用一会儿再看，启动过程会自动记录)"
                : text;
            SizeLabel.Text = $"runtime.log {AppLog.Size / 1024.0:0.#} KB";
        }
        else
        {
            var nav = DiagLog.ReadTail(NavFx.LogFileName);
            var crash = DiagLog.ReadTail("crash.log", 16 * 1024);

            var text = "";
            if (string.IsNullOrWhiteSpace(nav)) text += "(nav.log 还没有内容)\n\n";
            else text += nav + "\n";

            if (!string.IsNullOrWhiteSpace(crash))
                text += "\n======== crash.log ========\n" + crash;

            LogLabel.Text = string.IsNullOrWhiteSpace(text) ? "(暂无日志)" : text;
            SizeLabel.Text = $"nav.log {DiagLog.SizeOf(NavFx.LogFileName) / 1024.0:0.#} KB";
        }

        TabRuntime.BackgroundColor = _showRuntime ? Color.FromArgb("#00897B") : Color.FromArgb("#37474F");
        TabRuntime.TextColor = _showRuntime ? Colors.White : Color.FromArgb("#B0BEC5");
        TabDetail.BackgroundColor = _showRuntime ? Color.FromArgb("#37474F") : Color.FromArgb("#00897B");
        TabDetail.TextColor = _showRuntime ? Color.FromArgb("#B0BEC5") : Colors.White;
    }

    private void OnShowRuntime(object sender, EventArgs e)
    {
        _showRuntime = true;
        StatusLabel.Text = "";
        RenderLog();
    }

    private void OnShowDetail(object sender, EventArgs e)
    {
        _showRuntime = false;
        StatusLabel.Text = "";
        RenderLog();
    }

    private async void OnClearLog(object sender, EventArgs e)
    {
        var what = _showRuntime ? "运行日志" : "nav.log / crash.log";
        if (!await DisplayAlert("清空日志", $"确定清空{what}？清空后就没办法再回看这次的问题了。", "清空", "算了"))
            return;

        if (_showRuntime)
        {
            AppLog.Clear();
            AppLog.I("诊断", "家长在诊断页清空了运行日志");
            StatusLabel.Text = "运行日志已清空";
        }
        else
        {
            try
            {
                var a = DiagLog.PathOf(NavFx.LogFileName);
                if (File.Exists(a)) File.Delete(a);
                var b = DiagLog.PathOf("crash.log");
                if (File.Exists(b)) File.Delete(b);
                StatusLabel.Text = "nav.log / crash.log 已清空";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "清空失败：" + ex.Message;
            }
        }

        RenderLog();
    }

    private async void OnExport(object sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(AppLog.FilePath))
            {
                StatusLabel.Text = "还没有运行日志可以导出";
                return;
            }

            var name = $"运行日志-{DateTime.Now:yyyyMMdd-HHmm}.log";
            var dest = Path.Combine(FileSystem.CacheDirectory, name);
            if (AppLog.ExportTo(dest) is null)
            {
                StatusLabel.Text = "导出失败，可以改用「复制」";
                return;
            }

#if ANDROID || IOS || MACCATALYST
            // 走系统分享：微信、邮箱、企业微信都能直接发出去，不需要任何存储权限
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "儿童安全桌面 · 运行日志",
                File = new ShareFile(dest),
            });
            StatusLabel.Text = "已打开分享，选一个应用发出去即可";
#else
            // Windows：在资源管理器里直接选中日志文件，拷给开发者最省事
            var psi = new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{AppLog.FilePath}\"")
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
            StatusLabel.Text = "已在资源管理器中定位到 runtime.log";
            await Task.CompletedTask;
#endif
        }
        catch (Exception ex)
        {
            // 用户取消分享也会走到这里，不当成错误
            AppLog.W("诊断", "导出日志未完成：" + ex.Message);
            StatusLabel.Text = "导出没有完成，可以改用「复制」";
        }
    }

    private void OnRefresh(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        Refresh();
    }

    private async void OnCopy(object sender, EventArgs e)
    {
        try
        {
            var body = "【日志目录】\n" + DiagLog.Dir
                     + "\n\n【推送】\n" + PushLabel.Text
                     + "\n\n【包名/签名】\n" + SignLabel.Text
                     + "\n\n【运行日志 runtime.log】\n" + AppLog.Tail(32 * 1024)
                     + "\n\n【页面日志 nav.log】\n" + DiagLog.ReadTail(NavFx.LogFileName, 16 * 1024);
            await Clipboard.SetTextAsync(body);
            StatusLabel.Text = "已复制，粘贴给开发者即可";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "复制失败：" + ex.Message;
        }
    }

    private async void OnBack(object sender, EventArgs e)
    {
        try { await Navigation.PopAsync(); } catch { }
    }
}
