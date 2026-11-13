using System.Collections.ObjectModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 古诗学习首页：按主题筛选 + 诗词列表。
/// 内容全部来自 <see cref="PoemData"/>，离线可用；点卡片进入详情页朗读、看注释译文。
/// </summary>
public partial class PoemWorldPage : ContentPage
{
    private readonly ObservableCollection<Poem> _poems = new();
    private readonly List<Button> _chips = new();
    private string _category = "全部";

    public PoemWorldPage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "古诗学习", "📜", "poem", 2, "在古诗学习里读诗");
        NavigationPage.SetHasNavigationBar(this, false);

        PoemList.ItemsSource = _poems;
        BuildChips();
        SelectCategory("全部");
    }

    private void BuildChips()
    {
        ChipBar.Children.Clear();
        _chips.Clear();

        AddChip("全部", PoemData.All.Count);
        foreach (var c in PoemData.Categories)
            AddChip(c, PoemData.CountOf(c));
    }

    private void AddChip(string name, int count)
    {
        var emoji = name == "全部" ? "📚" : PoemData.EmojiOf(name);
        var btn = new Button
        {
            Text = $"{emoji} {name} {count}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 40,
            CornerRadius = 20,
            Padding = new Thickness(14, 0),
            BorderWidth = 0,
            CommandParameter = name
        };
        btn.Clicked += OnChipClicked;
        ChipBar.Children.Add(btn);
        _chips.Add(btn);
    }

    private void OnChipClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string name)
            SelectCategory(name);
    }

    private void SelectCategory(string name)
    {
        _category = name;

        _poems.Clear();
        foreach (var p in PoemData.ByCategory(name))
            _poems.Add(p);

        TotalLabel.Text = name == "全部" ? $"共 {PoemData.All.Count} 首" : $"{_poems.Count} 首";
        UpdateChipStyles();
    }

    private void UpdateChipStyles()
    {
        foreach (var chip in _chips)
        {
            var name = chip.CommandParameter as string ?? "全部";
            var on = name == _category;
            chip.BackgroundColor = on ? Color.FromArgb("#5D4037") : Colors.White;
            chip.TextColor = on ? Colors.White : Color.FromArgb("#5D4037");
        }
    }

    private async void OnPoemTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not Poem poem) return;

        var list = _poems.ToList();
        var index = list.IndexOf(poem);
        if (index < 0) index = 0;

        await Navigation.PushAsync(new PoemDetailPage(list, index));
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
