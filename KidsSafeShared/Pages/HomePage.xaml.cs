using System.Collections.ObjectModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace KidsSafeDesktop.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private readonly ObservableCollection<LauncherItem> _launcherItems = new();

    public HomePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new HomeViewModel();
        BindingContext = _vm;
        _vm.AppLaunchRequested += OnAppLaunchRequested;
        _vm.ParentRequested += (_, _) => _ = GoParentAsync();
        _vm.AccountRequested += (_, _) => _ = GoAccountAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ServiceProvider.Get<ISettingsService>().LoadAsync();
        ThemeService.Instance.Init();
        await _vm.RefreshAppsAsync();
        BuildLauncher();
        ApplyResponsiveSpan(Width);
        _vm.Start();
        _ = _vm.LoadWeatherAsync();
        var st = ServiceProvider.Get<IScreenTimeService>();
        st.LimitReached += OnLimitReached;
        _vm.UpdateRemaining();
        _ = ProbeTtsAsync();

        // 按当前时间重算护眼遮罩（定时护眼可能跨入/跨出生效时段）
        EyeCare.Refresh();
        _vm.RefreshEyeCare();
    }

    private static bool _ttsProbed;

    /// <summary>启动时探测 TTS 是否可用；不可用时给出可操作的安装指引。</summary>
    private async Task ProbeTtsAsync()
    {
        if (_ttsProbed) return;
        _ttsProbed = true;
        var (available, detail) = await Helpers.Tts.ProbeAsync();
        if (available) return;

        var openHelp = await DisplayAlert("语音播报不可用",
            "当前设备未检测到可用的语音包（TTS），朗读功能将无法出声。\n\n" +
            "可以在「操作帮助 → 语音配置」里，按步骤为你的设备安装语音包。\n\n" +
            "诊断信息：" + detail, "打开帮助", "知道了");

        if (openHelp)
            await Navigation.PushAsync(new HelpPage());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Stop();
        ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached;
    }

    protected override bool OnBackButtonPressed()
    {
        // 儿童模式下禁止返回（家长入口有独立鉴权）
        return true;
    }

    /// <summary>
    /// 只负责两件与页面自身相关的事：大时钟的字号分档、空状态提示。
    /// 图标列数/尺寸不再在这里算——统一交给 LauncherGridView + Helpers/LauncherMetrics，
    /// 这样首页和「小游戏乐园 / 学习天地 / 英语学习」在同一个窗口宽度下必然一样大。
    /// </summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        ApplyResponsiveSpan(width);
    }

    private double _lastClockFont = -1;

    private void ApplyResponsiveSpan(double width)
    {
        try
        {
            if (double.IsNaN(width) || width <= 0) return;

            // 手机竖屏时把大时钟缩小，把屏幕空间让给下面的图标网格
            double clock = width switch
            {
                < 420 => 32,
                < 520 => 38,
                < 720 => 44,
                _ => 52
            };
            if (Math.Abs(clock - _lastClockFont) >= 0.5)
            {
                _lastClockFont = clock;
                if (ClockLabel is not null) ClockLabel.FontSize = clock;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Home] 调整时钟字号出错(已忽略): {ex}");
        }
    }

    /// <summary>构建统一的启动器图标列表：内置功能 + 家长允许的外部应用。</summary>
    private void BuildLauncher()
    {
        _launcherItems.Clear();

        // 内置功能（手机桌面式图标 + 名称）
        // 游戏乐园：内置小游戏 + 15 款网页小游戏合并为同一个入口（分类在大厅里切换）
        AddBuiltIn("🎮", "游戏乐园", "#7E57C2", () => Nav(new GamesHubPage()));
        AddBuiltIn("🎵", "听一听", "#FF8A65", () => Nav(new MediaHubPage()));
        AddBuiltIn("🛡️", "安全知识", "#E53935", () => Nav(new SafetyKnowledgePage()));
        AddBuiltIn("📚", "口诀乐园", "#8E24AA", () => Nav(new MathRhymesPage()));
        AddBuiltIn("📖", "学习天地", "#29B6F6", () => Nav(new LearnWorldPage()));
        AddBuiltIn("📕", "电子课本", "#00897B", () => Nav(new TextbookShelfPage()));
        AddBuiltIn("📜", "古诗学习", "#8D6E63", () => Nav(new PoemWorldPage()));
        AddBuiltIn("🌙", "星夜故事屋", "#3F51B5", () => Nav(new StoryHousePage()));
        AddBuiltIn("✨", "无限想象", "#7B6BFF", () => Nav(new StoryLoomPage()));
        AddBuiltIn("🧹", "家务小英雄", "#F5A623", () => Nav(new ChoreHeroPage()));
        AddBuiltIn("💗", "关爱中心", "#EC407A", () => Nav(new CareCenterPage()));
        AddBuiltIn("🔐", "我的日记", "#5E35B1", () => Nav(new DiaryPage()));
        AddBuiltIn("🔤", "英语学习", "#7E57C2", () => Nav(new EnglishWorldPage()));
        AddBuiltIn("🗣️", "语音训练", "#AB47BC", () => Nav(new NsfdPage()));
        // 特需关爱（视障 / 听障 / 社交情绪训练），与「语音训练」同属康复类，放在一起便于家长找到
        AddBuiltIn("🌱", "特需关爱", "#00897B", () => Nav(new SpecialHubPage()));
        AddBuiltIn("🚀", "科学实验室", "#00B8D4", () => Nav(new ScienceLabPage()));
        AddBuiltIn("👀", "眼保健操", "#26A69A", () => Nav(new EyeExercisePage()));
        AddBuiltIn("🧍", "身体百科", "#EF6C00", () => Nav(new BodyEncyclopediaPage()));
        AddBuiltIn("❓", "操作帮助", "#5C6BC0", () => Nav(new HelpPage()));

        // 家长允许的外部应用（动态）
        foreach (var a in _vm.Apps)
            _launcherItems.Add(LauncherItem.FromApp(a));

        AppsGrid.ItemsSource = _launcherItems;
        if (EmptyHint is not null) EmptyHint.IsVisible = _launcherItems.Count == 0;
    }

    /// <summary>
    /// 在线内容关闭时需要隐藏的入口。
    /// 依据：家长中心「允许在线内容」关掉后，听一听 / 星夜故事屋 / 电子课本的内容源
    /// 只剩零星内置内容（主体在家长服务器上），留着会让孩子点进去看到大片空白。
    /// 其余入口（游戏乐园、学习天地等）与在线内容无关，不受影响。
    /// </summary>
    private static readonly HashSet<string> OnlineOnlyEntries = new()
    {
        "听一听", "星夜故事屋", "电子课本"
    };

    private void AddBuiltIn(string emoji, string name, string accent, Action launch)
    {
        // 游戏入口受家长开关控制
        if (name == "游戏乐园" && !_vm.GamesVisible) return;

        // 在线内容关闭时，纯在线来源的入口整体隐藏
        if (OnlineOnlyEntries.Contains(name) && !_vm.OnlineContentVisible) return;

        _launcherItems.Add(new LauncherItem
        {
            Name = name,
            Emoji = emoji,
            Accent = Color.FromArgb(accent),
            Launch = launch
        });
    }

    private void Nav(Page page) => Navigation.Push(page);

    /// <summary>
    /// 磁贴点击（由 LauncherGridView 统一去重后抛出，磁贴本身的「按下去」动画也由它处理）。
    /// 外部应用走系统启动，内置功能走页面导航。
    /// </summary>
    private async void OnLauncherTapped(object? sender, LauncherItem item)
    {
        if (item is null) return;

        if (item.App is not null)
            _vm.RequestLaunch(item.App);
        else
        {
            try
            {
                item.Launch?.Invoke();
            }
            catch (Exception ex)
            {
                // 子页面构造/导航异常时给出提示，避免整 app 闪退
                System.Diagnostics.Debug.WriteLine($"[Home] 启动功能出错: {ex}");
                await DisplayAlert("打不开", "这个功能暂时无法打开：" + ex.Message, "好的");
            }
        }
    }

    private async void OnThemeTapped(object sender, EventArgs e)
    {
        await Navigation.PushAfterPressAsync(sender as View, () => new ThemePickerPage());
    }

    private void OnWeatherTapped(object sender, EventArgs e)
    {
        if (sender is View view) view.Pop();
        _ = _vm.LoadWeatherAsync();
    }

    private async void OnAccountTapped(object sender, EventArgs e)
    {
        await GoAccountAsync(sender as View);
    }

    private async Task GoAccountAsync(View? pressed = null)
    {
        await Navigation.PushAfterPressAsync(pressed, () => new AccountPage());
    }

    private async void OnAvatarTapped(object sender, EventArgs e)
    {
        if (sender is View view) view.Pop();

        var current = _vm.Nickname == "小朋友" ? string.Empty : _vm.Nickname;
        var result = await DisplayPromptAsync(
            "你的名字",
            "给自己起一个可爱的名字吧～",
            initialValue: current,
            maxLength: 8,
            placeholder: "例如：小星星",
            accept: "确定",
            cancel: "取消");

        // 点了「确定」（即使是空）才保存；取消则不动
        if (result is not null)
        {
            await _vm.SetNicknameAsync(result);
            ServiceProvider.Get<ISoundService>().PlayCorrect();
        }
    }

    private async void OnAppLaunchRequested(object? sender, AppItem app)
    {
        try
        {
            await ServiceProvider.Get<IAppLauncherService>().LaunchAsync(app);
        }
        catch (Exception ex)
        {
            await DisplayAlert("打不开", "这个应用暂时无法启动：" + ex.Message, "好的");
        }
    }

    private async void OnParentClicked(object sender, EventArgs e) => await GoParentAsync();

    /// <summary>右下角「家长」胶囊按钮（Border + TapGesture，不用 Button 以免被全局样式染成绿色方块）。</summary>
    private async void OnParentTapped(object sender, EventArgs e)
    {
        if (sender is View view) view.Pop();
        await GoParentAsync();
    }

    private async Task GoParentAsync()
    {
        await Navigation.PushAsync(new ParentGatePage());
    }

    private async void OnLimitReached(object? sender, EventArgs e)
    {
        // 防止重复压栈
        if (Navigation.NavigationStack.LastOrDefault() is LockoutPage) return;
        await Navigation.PushAsync(new LockoutPage());
    }
}
