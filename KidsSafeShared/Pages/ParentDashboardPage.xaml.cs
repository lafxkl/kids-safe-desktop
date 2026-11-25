using KidsSafeDesktop.Services;
using KidsSafeDesktop.Updates;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class ParentDashboardPage : ContentPage
{
    private readonly ParentDashboardViewModel _vm;

    public ParentDashboardPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new ParentDashboardViewModel();
        BindingContext = _vm;
    }

    /// <summary>把本应用设为系统默认桌面（安卓 RoleManager），上滑/Home 就会回到本应用。</summary>
    private void OnSetDefaultHome(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AndroidKioskService.RequestDefaultHome(force: true);
#else
        _ = DisplayAlert("仅安卓支持", "把本应用设为默认桌面只在安卓设备上有效。", "好的");
#endif
    }

    /// <summary>申请「显示在其他应用上层」权限：Android 10+ 后台把本应用拉回前台需要它。</summary>
    private void OnRequestOverlay(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AndroidKioskService.RequestOverlayPermission();
#else
        _ = DisplayAlert("仅安卓支持", "该权限只在安卓设备上有效。", "好的");
#endif
    }

    /// <summary>重新打开首次设置引导（不清除已有配置，走一遍即可覆盖）。</summary>
    private async void OnReplayOnboarding(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new OnboardingPage());
    }

    private async void OnOpenDiagnostics(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new DiagnosticsPage());
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
        }
    }

    /// <summary>申请忽略电池优化，避免荣耀/华为等机型在切后台后直接清理进程。</summary>
    private void OnIgnoreBattery(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AndroidKioskService.RequestIgnoreBatteryOptimizations();
#else
        _ = DisplayAlert("仅安卓支持", "电池优化白名单只在安卓设备上有效。", "好的");
#endif
    }

    /// <summary>打开「锁定模式」后立刻尝试进入系统屏幕固定，不用重启应用。</summary>
    private void OnKioskLockToggled(object sender, ToggledEventArgs e)
    {
#if ANDROID
        // 走钩子而不是直接引用 MainActivity：共享库不能依赖具体 App 的入口类
        if (e.Value) Platforms.Android.AndroidHooks.ApplyLockTaskNow?.Invoke();
#endif
    }

    /// <summary>
    /// 无障碍守护：系统不允许应用自己开启，只能把家长送到设置页。
    /// 已经开了就明确告诉一声，避免家长来回找。
    /// </summary>
    private async void OnEnableAccessibility(object sender, EventArgs e)
    {
#if ANDROID
        var on = Platforms.Android.AndroidHooks.IsAccessibilityGuardOn?.Invoke() ?? false;
        if (on)
        {
            await DisplayAlert("已开启", "无障碍守护正在运行：孩子打开系统设置、应用商店或卸载界面时会自动退回儿童桌面。", "好的");
            return;
        }
        await DisplayAlert("去系统设置里开启",
            "接下来会打开「设置 → 无障碍」，请在列表里找到「儿童安全桌面」并打开。\n\n" +
            "这个服务只看「哪个应用到了前台」，不读取输入内容、不截屏。", "去开启");
        Platforms.Android.AndroidHooks.OpenAccessibilitySettings?.Invoke();
#else
        await DisplayAlert("仅安卓支持", "无障碍守护只在安卓设备上有效。", "好的");
#endif
    }

    /// <summary>设备管理员：激活后卸载界面会先要求取消激活（家长要卸载时在这里解除）。</summary>
    private async void OnToggleDeviceAdmin(object sender, EventArgs e)
    {
#if ANDROID
        var on = Platforms.Android.AndroidHooks.IsDeviceAdminOn?.Invoke() ?? false;
        if (on)
        {
            var ok = await DisplayAlert("解除设备管理员",
                "解除后本应用可以被直接卸载（家长控制也随之失效）。确定解除吗？", "解除", "取消");
            if (ok)
            {
                Platforms.Android.AndroidHooks.DisableDeviceAdmin?.Invoke();
                _vm.RefreshGuardState();
            }
            return;
        }

        Platforms.Android.AndroidHooks.RequestDeviceAdmin?.Invoke();
        _vm.RefreshGuardState();
#else
        await DisplayAlert("仅安卓支持", "设备管理员只在安卓设备上有效。", "好的");
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Attach();
        // 无障碍 / 设备管理员 / 开机自启都是在系统页面里改的状态，
        // 每次进面板都要重新读一次，否则开关显示的还是上次的旧值。
        _vm.RefreshGuardState();
        RefreshPrivacyState();
        CatalogLoading.IsVisible = true;
        CatalogLoading.IsRunning = true;
        await _vm.LoadCatalogAsync();
        CatalogLoading.IsRunning = false;
        CatalogLoading.IsVisible = false;

        // 更新卡片：没检查过就顺手查一次（只下几十 KB 的清单，失败也只写状态文字）
        _vm.AutoCheckUpdate();

        // 反馈卡片：进面板时补传上次断网攒下的反馈；补传在后台做，不挡界面。
        // 用 GetOrDefault：家长端（KidsSafeParent）没有注册反馈服务，不能因为少了它崩在 OnAppearing。
        RefreshFeedbackPending();
        var feedback = ServiceProvider.GetOrDefault<IFeedbackService>();
        if (feedback is not null)
        {
            _ = feedback.FlushAsync().ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(RefreshFeedbackPending));
        }
    }

    /// <summary>「本机还有 N 条没发出去」只在真有积压时显示，平时不占地方。</summary>
    private void RefreshFeedbackPending()
    {
        try
        {
            var pending = ServiceProvider.GetOrDefault<IFeedbackService>()?.PendingCount ?? 0;
            FeedbackPendingLabel.IsVisible = pending > 0;
            FeedbackPendingLabel.Text = pending > 0
                ? $"本机还有 {pending} 条反馈没发出去，联网后会自动补传"
                : string.Empty;
        }
        catch
        {
            FeedbackPendingLabel.IsVisible = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Detach();
    }

    private void OnLimitChanged(object sender, ValueChangedEventArgs e)
    {
        // 数值已通过 TwoWay 绑定写回 VM，这里用于即时反馈
        LimitLabel.Text = $"{_vm.DailyLimitMinutes} 分钟";
    }

    private void OnAppChecked(object sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox cb && cb.BindingContext is CatalogEntry entry)
            _vm.ToggleApp(entry);
    }

    private void OnAddSite(object sender, EventArgs e) => _vm.AddSite();

    private void OnClearLeaderboard(object sender, EventArgs e)
        => _vm.ClearLeaderboard();

    /// <summary>打开小游戏工坊（网页编辑器）。</summary>
    private async void OnOpenMaker(object sender, EventArgs e)
        => await Navigation.PushAsync(new GameMakerPage());

    private void OnRemoveSite(object sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string site)
            _vm.RemoveSite(site);
    }

    private async void OnSave(object sender, EventArgs e)
    {
        await _vm.SaveAsync();
        // 保存后返回儿童主页（主页 OnAppearing 会刷新磁贴）
        await Navigation.PopToRootAsync();
    }

    private async void OnBackHome(object sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
    }

    private async void OnExit(object sender, EventArgs e)
    {
        var ok = await DisplayAlert("退出儿童模式", "确定要退出吗？退出后儿童桌面将关闭。", "确定", "取消");
        if (ok)
            await ServiceProvider.Get<IKioskService>().ExitAppAsync();
    }

    private async void OnAccount(object sender, EventArgs e)
        => await Navigation.PushAsync(new AccountPage());

    // ── 反馈与问卷 ────────────────────────────────────────────────────────

    private async void OnOpenFeedback(object sender, EventArgs e)
        => await Navigation.PushAsync(new FeedbackPage());

    private async void OnOpenSatisfaction(object sender, EventArgs e)
        => await Navigation.PushAsync(new SatisfactionPage());

    private async void OnOpenSurvey(object sender, EventArgs e)
        => await Navigation.PushAsync(new SurveyPage());

    // ── 隐私与合规 ────────────────────────────────────────────────────────

    /// <summary>显示当前同意状态：版本号 + 同意时间（撤回后显示「未同意」）。</summary>
    private void RefreshPrivacyState()
    {
        try
        {
            PrivacyStateLabel.Text = PrivacyConsent.HasAgreed
                ? $"当前政策版本 v{PrivacyConsent.CurrentVersion}，已于 " +
                  (PrivacyConsent.AgreedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—") + " 同意"
                : "尚未同意当前版本的隐私政策，下次启动会重新询问";
        }
        catch
        {
            PrivacyStateLabel.Text = string.Empty;
        }
    }

    private async void OnOpenPrivacyPolicy(object sender, EventArgs e)
        => await Navigation.PushAsync(new PrivacyPolicyPage());

    private async void OnOpenSdkList(object sender, EventArgs e)
        => await Navigation.PushAsync(new ThirdPartySdkPage());

    /// <summary>
    /// 撤回授权：按《个人信息保护法》第 15 条，个人有权撤回同意。
    /// 撤回后不立即删除数据（用户可能只是误触），但要停止后续处理并重新征询。
    /// </summary>
    private async void OnRevokePrivacy(object sender, EventArgs e)
    {
        if (!PrivacyConsent.HasAgreed)
        {
            await DisplayAlert("提示", "当前尚未同意隐私政策，无需撤回。", "好的");
            return;
        }

        var ok = await DisplayAlert("撤回隐私授权",
            "撤回后：\n• 下次启动会重新询问是否同意\n• 在重新同意之前，应用不读取本地设置、不连服务器、不申请推送 token\n\n已保存在本机的数据不会自动删除，需要的话可在同一页面手动清除应用数据。",
            "确认撤回", "取消");
        if (!ok) return;

        PrivacyConsent.Revoke();
        KidsSafeDesktop.Helpers.AppLog.W("隐私", "家长已撤回隐私授权");
        KidsSafeDesktop.Helpers.DiagLog.Append(KidsSafeDesktop.Helpers.NavFx.LogFileName, "[隐私] 家长已撤回隐私授权");
        RefreshPrivacyState();

        // 立即停止依赖同意的后台处理
        try { ServiceProvider.Get<ILiveSyncService>()?.Stop(); } catch { }

        await DisplayAlert("已撤回", "隐私授权已撤回，下次启动会重新征得同意。", "好的");
    }


    private async void OnServerLogin(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ServerUrl))
        {
            await DisplayAlert("提示", "请先填写服务器地址", "好的");
            return;
        }
        await _vm.LoginServerAsync();
        await _settingsSave();
    }

    private async void OnServerSync(object sender, EventArgs e)
    {
        if (!_vm.ServerEnabled)
        {
            await DisplayAlert("提示", "请先启用家长端服务器", "好的");
            return;
        }
        await _vm.SyncServerAsync();
        await _settingsSave();
    }

    private async Task _settingsSave()
    {
        // 地址/启用状态已通过绑定写入 settings，这里确保落盘
        await ServiceProvider.Get<ISettingsService>().SaveAsync();
    }

    // ── 软件更新 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 一个按钮走完「检查 → 下载 → 安装」：按当前状态决定这一步做什么，
    /// 家长不需要理解增量更新的过程。
    /// </summary>
    private async void OnUpdateAction(object sender, EventArgs e)
    {
        try
        {
            var u = ServiceProvider.Get<IUpdateService>();

            switch (u.State)
            {
                case UpdateState.Available:
                {
                    if (!await _vm.DownloadUpdateAsync())
                    {
                        await DisplayAlert("更新未完成", u.Status, "好的");
                        return;
                    }

                    // Android 只能在下次冷启动预加载，这里直接写激活标记，不用家长再点一次。
                    if (OperatingSystem.IsAndroid())
                    {
                        await _vm.ApplyUpdateAsync(restart: false);
                        await DisplayAlert("更新已下载", "下次打开应用时生效。", "好的");
                        return;
                    }

                    var now = await DisplayAlert("更新已下载",
                        $"{u.Status}\n\n现在重启应用完成安装？\n选「稍后」则在退出应用时自动安装。",
                        "马上重启", "稍后");
                    await _vm.ApplyUpdateAsync(restart: now);
                    break;
                }

                case UpdateState.Ready:
                {
                    if (OperatingSystem.IsAndroid())
                    {
                        await _vm.ApplyUpdateAsync(restart: false);
                        await DisplayAlert("已准备好", "下次打开应用时生效。", "好的");
                        break;
                    }

                    var ok = await DisplayAlert("安装更新",
                        "应用会关闭，几秒后自动重新打开。\n更新失败会自动回滚到当前版本。",
                        "现在安装", "取消");
                    if (ok) await _vm.ApplyUpdateAsync(restart: true);
                    break;
                }

                default:
                {
                    var found = await _vm.CheckUpdateAsync();
                    if (!found) await DisplayAlert("软件更新", u.Status, "好的");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
            await DisplayAlert("更新出错", ex.Message, "好的");
        }
    }

    private void OnUpdateCancel(object sender, EventArgs e) => _vm.CancelUpdate();
}
