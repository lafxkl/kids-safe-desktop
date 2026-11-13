using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 家长心理守护指南（给大人看，不是给孩子玩的）。
/// 四个分栏：该做的 / 别做的 / 换句话 / 该就医。
/// 写作立场：不指责家长、只给可执行动作；「别做的」每一条都配替代做法，
/// 因为只讲「不要」家长做不到，给了「换成什么」才做得到。
/// </summary>
public partial class SpecialParentGuidePage : ContentPage
{
    private static readonly string[] TabAccents = { "#2E7D32", "#C62828", "#5E35B1", "#E65100" };

    public SpecialParentGuidePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BindableLayout.SetItemsSource(RulesHost, ParentMentalHealthData.GroundRules);
        BindableLayout.SetItemsSource(DoHost, ParentMentalHealthData.DoTips);
        BindableLayout.SetItemsSource(AvoidHost, ParentMentalHealthData.AvoidTips);
        BindableLayout.SetItemsSource(PhraseHost, ParentMentalHealthData.Phrases);
        BindableLayout.SetItemsSource(UrgentHost,
            ParentMentalHealthData.Warnings.Where(w => w.Urgent).ToList());
        BindableLayout.SetItemsSource(NormalHost,
            ParentMentalHealthData.Warnings.Where(w => !w.Urgent).ToList());
        BindableLayout.SetItemsSource(HotlineHost, ParentMentalHealthData.Hotlines);

        Select(0);
    }

    private void OnTabDo(object sender, EventArgs e) => Select(0);
    private void OnTabAvoid(object sender, EventArgs e) => Select(1);
    private void OnTabPhrase(object sender, EventArgs e) => Select(2);
    private void OnTabWarn(object sender, EventArgs e) => Select(3);

    private void Select(int index)
    {
        try
        {
            PaneDo.IsVisible = index == 0;
            PaneAvoid.IsVisible = index == 1;
            PanePhrase.IsVisible = index == 2;
            PaneWarn.IsVisible = index == 3;

            StyleTab(TabDo, TabDoText, index == 0, TabAccents[0]);
            StyleTab(TabAvoid, TabAvoidText, index == 1, TabAccents[1]);
            StyleTab(TabPhrase, TabPhraseText, index == 2, TabAccents[2]);
            StyleTab(TabWarn, TabWarnText, index == 3, TabAccents[3]);

            _ = Body.ScrollToAsync(0, 0, false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[家长指南] 切换分栏失败: {ex}");
        }
    }

    private static void StyleTab(Border tab, Label text, bool selected, string accent)
    {
        tab.BackgroundColor = selected ? Color.FromArgb(accent) : Colors.White;
        text.TextColor = selected ? Colors.White : Color.FromArgb("#616161");
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
