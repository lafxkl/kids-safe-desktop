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
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            // 路径
            PathLabel.Text = DiagLog.Dir;
            PathHintLabel.Text = DiagLog.IsExternal
                ? "路径形如 Android/data/<包名>/files/logs（卸载应用时自动清除）"
                : "当前写在应用内部私有目录，手机文件管理器看不到 —— 请用本页「复制全部」导出。";
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
            var nav = DiagLog.ReadTail(NavFx.LogFileName);
            var crash = DiagLog.ReadTail("crash.log", 16 * 1024);

            var text = "";
            if (string.IsNullOrWhiteSpace(nav)) text += "(nav.log 还没有内容)\n\n";
            else text += nav + "\n";

            if (!string.IsNullOrWhiteSpace(crash))
                text += "\n======== crash.log ========\n" + crash;

            LogLabel.Text = string.IsNullOrWhiteSpace(text) ? "(暂无日志)" : text;
            StatusLabel.Text = $"nav.log {DiagLog.SizeOf(NavFx.LogFileName) / 1024.0:0.#} KB";
        }
        catch (Exception ex)
        {
            LogLabel.Text = "读取失败：" + ex;
            StatusLabel.Text = "";
        }
    }

    private void OnRefresh(object sender, EventArgs e) => Refresh();

    private async void OnCopy(object sender, EventArgs e)
    {
        try
        {
            var body = "【日志目录】\n" + DiagLog.Dir
                     + "\n\n【推送】\n" + PushLabel.Text
                     + "\n\n【包名/签名】\n" + SignLabel.Text
                     + "\n\n【日志】\n" + LogLabel.Text;
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
