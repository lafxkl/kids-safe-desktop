using KidsSafeDesktop.Services;
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Attach();
        RefreshPrivacyState();
        CatalogLoading.IsVisible = true;
        CatalogLoading.IsRunning = true;
        await _vm.LoadCatalogAsync();
        CatalogLoading.IsRunning = false;
        CatalogLoading.IsVisible = false;
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
}
