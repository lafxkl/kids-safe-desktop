using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 操作帮助中心：分分类浏览使用说明，并提供系统语音（TTS）自检与配置引导。
/// </summary>
public partial class HelpPage : ContentPage
{
    private readonly HelpViewModel _vm;

    public HelpPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new HelpViewModel();
        BindingContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        BuildCategoryTabs();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HelpViewModel.SelectedCategory))
            UpdateCategoryTabs();
    }

    // ===== 分类切换按钮 =====
    private void BuildCategoryTabs()
    {
        CategoryStack.Children.Clear();
        foreach (var c in _vm.Categories)
        {
            var btn = new Button
            {
                Text = c,
                FontSize = 15,
                CornerRadius = 18,
                HeightRequest = 44,
                Padding = new Thickness(18, 0),
                Command = new Command(() => _vm.SelectCategory(c))
            };
            CategoryStack.Children.Add(btn);
        }
        UpdateCategoryTabs();
    }

    private void UpdateCategoryTabs()
    {
        foreach (var child in CategoryStack.Children)
        {
            if (child is not Button btn) continue;
            var selected = btn.Text == _vm.SelectedCategory;
            btn.BackgroundColor = selected ? Color.FromArgb("#5C6BC0") : Color.FromArgb("#FFFFFF");
            btn.TextColor = selected ? Colors.White : Color.FromArgb("#5D4037");
        }
    }

    // ===== 条目展开 / 收起 =====
    private void OnTopicTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is HelpTopic topic)
        {
            view.Pop();
            _vm.ToggleTopic(topic);
        }
    }

    // ===== 跳转到系统语音设置 =====
    private async void OnOpenTtsSettings(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var intent = new Android.Content.Intent("com.android.settings.TTS_SETTINGS");
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Platform.AppContext.StartActivity(intent);
#elif WINDOWS
            await Launcher.OpenAsync("ms-settings:speech");
#else
            await Launcher.OpenAsync("app-settings:");
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("打不开设置",
                "无法自动打开系统语音设置，请手动进入：\n" +
                "· Windows：设置 → 时间和语言 → 语音\n" +
                "· Android：设置 → 系统 → 语言和输入法 → 文字转语音(TTS)输出\n\n" +
                "错误信息：" + ex.Message, "好的");
        }
    }

    // ===== 恢复系统任务栏（自救入口） =====
    private async void OnRestoreTaskbar(object sender, EventArgs e)
    {
        try
        {
#if WINDOWS
            KidsSafeDesktop.Platforms.Windows.WindowsKioskService.RestoreTaskbar();
            await DisplayAlert("已尝试恢复",
                "已重新显示系统任务栏。\n\n" +
                "如果还是看不到，请按 Ctrl+Shift+Esc 打开任务管理器 →" +
                "「运行新任务」→ 输入 explorer.exe 并回车。",
                "好的");
#else
            await DisplayAlert("不适用",
                "恢复任务栏仅用于 Windows 电脑版（儿童模式会隐藏系统任务栏）。",
                "好的");
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("恢复失败", ex.Message, "好的");
        }
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = Navigation.PopAsync();
        return true;
    }
}
