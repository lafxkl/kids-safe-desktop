using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 特需关爱板块首页：按「学习通道」把训练分成三组（视觉障碍 / 听觉障碍 / 社交情绪），
/// 每个模块卡片都写明目标、机制、剂量与要领，点击进入对应的专用小游戏。
/// </summary>
public partial class SpecialHubPage : ContentPage
{
    public SpecialHubPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BindableLayout.SetItemsSource(VisionList, SpecialTrainingData.Vision);
        BindableLayout.SetItemsSource(HearingList, SpecialTrainingData.Hearing);
        BindableLayout.SetItemsSource(SocialList, SpecialTrainingData.Social);
    }

    private async void OnModuleTapped(object sender, EventArgs e)
    {
        if ((sender as View)?.BindingContext is not SpecialModule m) return;

        ServiceProvider.Get<ISoundService>().PlayClick();

        try
        {
            await Navigation.PushAfterPressAsync(sender as View, () => PageFor(m));
        }
        catch (Exception ex)
        {
            await DisplayAlert("打不开", "这个训练暂时无法打开：" + ex.Message, "好的");
        }
    }

    private static Page PageFor(SpecialModule m) => m.Title switch
    {
        "声音寻宝" => new SoundTreasureGamePage(),
        "听音复述" => new SoundSequenceGamePage(),
        "听声辨物" => new SoundOddOneGamePage(),
        "节奏光影" => new RhythmLightGamePage(),
        "手语图卡" => new SignCardGamePage(),
        "表情配对" => new EmotionMatchGamePage(),
        _ => new SpecialHubPage()
    };

    private void OnBack(object sender, EventArgs e) => _ = Navigation.PopAsync();

    /// <summary>家长心理守护指南（给大人看的科普，不是训练）。</summary>
    private async void OnParentGuide(object sender, EventArgs e)
    {
        await Navigation.PushAfterPressAsync(sender as View, () => new SpecialParentGuidePage());
    }
}
