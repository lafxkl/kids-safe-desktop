using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 首次启动引导：家长密码 → 孩子信息 → 每日时长 → 权限申请（安卓）。
/// 完成后写入 <c>onboarding_done</c>，以后启动直接进首页。
/// 家长可在「家长面板 → 重新查看新手引导」再次打开。
/// </summary>
public partial class OnboardingPage : ContentPage
{
    /// <summary>可选头像。</summary>
    private static readonly string[] Avatars =
        { "🐯", "🐼", "🦊", "🐰", "🐨", "🐵", "🦄", "🐶", "🐱", "🐷", "🐸", "🐙" };

    private const string AvatarSelectedBg = "#FF7043";
    private const string AvatarNormalBg = "#FFFFFF";

    private readonly ISettingsService _settings;

    private int _step = 1;
    private int _totalSteps = 4;
    private string _avatar = "🐯";
    private readonly List<Border> _avatarBorders = new();

    public OnboardingPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _settings = ServiceProvider.Get<ISettingsService>();

#if !ANDROID
        _totalSteps = 3;   // 非安卓没有权限步骤
#endif

        BuildAvatarChoices();
        UpdateStep();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // 从系统设置页返回时刷新授权状态
        RefreshPermissionStatus();
    }

    // ── 步骤切换 ──────────────────────────────────────────────────────────

    private void UpdateStep()
    {
        Step1Panel.IsVisible = _step == 1;
        Step2Panel.IsVisible = _step == 2;
        Step3Panel.IsVisible = _step == 3;
        Step4Panel.IsVisible = _step == 4;

        BackBtn.IsVisible = _step > 1;

        (string title, string subtitle) = _step switch
        {
            1 => ("欢迎使用儿童安全桌面", "先设置一个家长密码，只有你知道"),
            2 => ("认识一下小朋友", "起个名字，再选一个喜欢的头像"),
            3 => ("安排每天的使用时间", "合理的时间，保护小眼睛"),
            4 => ("开启防退出保护", "最后一步，让孩子专注在桌面里"),
            _ => ("准备完成", "马上开始吧")
        };
        StepTitle.Text = title;
        StepSubtitle.Text = subtitle;

        NextBtn.Text = _step >= _totalSteps ? "开始使用 🎉" : "下一步";
        BuildDots();

        if (_step == 4) RefreshPermissionStatus();
    }

    private void BuildDots()
    {
        DotsRow.Children.Clear();
        for (var i = 1; i <= _totalSteps; i++)
        {
            var active = i == _step;
            DotsRow.Children.Add(new Border
            {
                WidthRequest = active ? 22 : 8,
                HeightRequest = 8,
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb(active ? "#FF7043" : "#FFCCBC"),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) }
            });
        }
    }

    // ── 头像选择 ──────────────────────────────────────────────────────────

    private void BuildAvatarChoices()
    {
        foreach (var emoji in Avatars)
        {
            var border = new Border
            {
                WidthRequest = 56,
                HeightRequest = 56,
                Margin = new Thickness(4),
                StrokeThickness = 2,
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb(AvatarNormalBg),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(18) },
                Content = new Label
                {
                    Text = emoji,
                    FontSize = 28,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var chosen = emoji;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectAvatar(chosen);
            border.GestureRecognizers.Add(tap);

            _avatarBorders.Add(border);
            AvatarRow.Children.Add(border);
        }

        SelectAvatar(_avatar);
    }

    private void SelectAvatar(string emoji)
    {
        _avatar = emoji;
        for (var i = 0; i < Avatars.Length; i++)
        {
            var selected = Avatars[i] == emoji;
            _avatarBorders[i].Stroke = Color.FromArgb(selected ? AvatarSelectedBg : AvatarNormalBg);
            _avatarBorders[i].BackgroundColor = Color.FromArgb(selected ? "#FFE0B2" : AvatarNormalBg);
        }
    }

    // ── 事件 ─────────────────────────────────────────────────────────────

    private void OnLimitChanged(object sender, ValueChangedEventArgs e)
    {
        LimitLabel.Text = $"{Math.Round(e.NewValue)} 分钟";
    }

    private async void OnNext(object sender, EventArgs e)
    {
        // 最后一步（安卓 4 步 / Windows 3 步）→ 直接完成。
        // 注意：这里必须用 >= _totalSteps 判断，不能写死 case 4，
        // 否则 Windows（3 步）永远走不到完成分支，会卡在最后一步出不去。
        if (_step >= _totalSteps)
        {
            await FinishAsync();
            return;
        }

        switch (_step)
        {
            case 1:
                if (!ValidatePin()) return;
                break;

            case 2:
                // 昵称留空时用默认值，不阻断流程
                break;
        }

        _step++;
        if (_step > _totalSteps) _step = _totalSteps;
        UpdateStep();
    }

    private void OnBack(object sender, EventArgs e)
    {
        if (_step <= 1) return;
        _step--;
        UpdateStep();
    }

    private bool ValidatePin()
    {
        var pin = (PinBox.Text ?? string.Empty).Trim();
        var pin2 = (PinBox2.Text ?? string.Empty).Trim();

        if (pin.Length != 4 || !pin.All(char.IsDigit))
        {
            ShowPinError("请输入 4 位数字密码");
            return false;
        }

        if (pin != pin2)
        {
            ShowPinError("两次输入的密码不一致");
            return false;
        }

        PinError.IsVisible = false;
        return true;
    }

    private void ShowPinError(string message)
    {
        PinError.Text = message;
        PinError.IsVisible = true;
    }

    // ── 保存 ─────────────────────────────────────────────────────────────

    private async Task FinishAsync()
    {
        try
        {
            var pin = (PinBox.Text ?? string.Empty).Trim();
            if (pin.Length == 4) _settings.SetPin(pin);

            var nick = (NickBox.Text ?? string.Empty).Trim();
            _settings.Current.Nickname = string.IsNullOrWhiteSpace(nick) ? "小朋友" : nick;
            _settings.Current.AvatarEmoji = _avatar;
            _settings.Current.DailyLimitMinutes = (int)Math.Round(LimitSlider.Value);
            _settings.Current.GamesEnabled = GamesSwitch.IsToggled;

            // 保存失败/卡住都不能阻断「进入儿童桌面」：SecureStorage 在个别 Windows
            // 环境下会一直不返回（无异常、无回调），此前表现为点「开始使用」毫无反应。
            var save = _settings.SaveAsync();
            await Task.WhenAny(save, Task.Delay(1500));

            Preferences.Set("onboarding_done", true);
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
            // 保存失败也允许进入（配置可用默认值），但不能卡在引导页
            Preferences.Set("onboarding_done", true);
        }

        await GoHomeAsync();
    }

    private async Task GoHomeAsync()
    {
        try { await this.FadeTo(0, 200); } catch { }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var page = new NavigationPage(new HomePage());
            var app = Application.Current;
            if (app is not null && app.Windows.Count > 0)
                app.Windows[0].Page = page;
        });

        try { await ServiceProvider.Get<IKioskService>().EnterKioskAsync(); }
        catch { }
    }

    // ── 安卓权限 ──────────────────────────────────────────────────────────

    private void RefreshPermissionStatus()
    {
#if ANDROID
        try
        {
            OverlayStatus.Text = Platforms.Android.AndroidKioskService.HasOverlayPermission()
                ? "已授权 ✓"
                : "未授权（点右侧开启）";

            BatteryStatus.Text = Platforms.Android.AndroidKioskService.IsIgnoringBatteryOptimizations()
                ? "已加入白名单 ✓"
                : "未授权（点右侧开启）";

            var isHome = Platforms.Android.AndroidKioskService.IsDefaultHome();
            HomeStatus.Text = isHome ? "已是默认桌面 ✓" : "未设置（点右侧设置）";
        }
        catch
        {
            // 状态查询失败不影响引导
        }
#endif
    }

    private async void OnRequestOverlay(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AndroidKioskService.RequestOverlayPermission();
        await Task.Delay(600);
        RefreshPermissionStatus();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnRequestBattery(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AndroidKioskService.RequestIgnoreBatteryOptimizations();
        await Task.Delay(600);
        RefreshPermissionStatus();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnSetDefaultHome(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AndroidKioskService.RequestDefaultHome(force: true);
        await Task.Delay(600);
        RefreshPermissionStatus();
#else
        await Task.CompletedTask;
#endif
    }

    protected override bool OnBackButtonPressed()
    {
        // 引导过程中不允许用返回键退出（安卓返回手势）
        if (_step > 1) { _step--; UpdateStep(); }
        return true;
    }
}
