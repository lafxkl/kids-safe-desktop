using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Pages;

/// <summary>星夜故事屋：离线故事库（分类 / 搜索 / 收藏）。</summary>
public partial class StoryHousePage : ContentPage
{
    private const string FavKey = "story_fav_v1";

    private string _category = "全部";
    private string _search = string.Empty;
    private bool _favOnly;

    public StoryHousePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        // 学习打卡入口（听完故事可以领星星，也可以从激励页看到累计）
        LearnCheckIn.Attach(this, "故事屋", "🌙", "story_house", 2, "在故事屋听故事");

        BuildChips();
        Render();
    }

    // ── 收藏 ────────────────────────────────────────────────

    public static HashSet<string> Favorites()
    {
        try
        {
            var raw = Preferences.Get(FavKey, string.Empty);
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        }
        catch { return new HashSet<string>(); }
    }

    public static bool IsFavorite(string id) => Favorites().Contains(id);

    public static bool ToggleFavorite(string id)
    {
        var set = Favorites();
        var on = !set.Contains(id);
        if (on) set.Add(id); else set.Remove(id);
        try { Preferences.Set(FavKey, string.Join(',', set)); } catch { /* 忽略 */ }
        return on;
    }

    // ── 渲染 ────────────────────────────────────────────────

    private void BuildChips()
    {
        ChipBar.Children.Clear();
        foreach (var cat in StoryHouseCatalog.Categories)
        {
            var btn = new Button
            {
                Text = cat == "全部" ? "✨ 全部" : cat,
                FontSize = 13,
                HeightRequest = 36,
                Padding = new Thickness(14, 0),
                CornerRadius = 18,
                CommandParameter = cat
            };
            btn.Clicked += (_, _) =>
            {
                _category = cat;
                StyleChips();
                Render();
            };
            ChipBar.Children.Add(btn);
        }
        StyleChips();
    }

    private void StyleChips()
    {
        foreach (var child in ChipBar.Children)
        {
            if (child is not Button b) continue;
            var on = (b.CommandParameter as string) == _category;
            b.BackgroundColor = on ? Color.FromArgb("#8AB4FF") : Color.FromArgb("#1C244E");
            b.TextColor = on ? Color.FromArgb("#0A0E2A") : Color.FromArgb("#AAB2D8");
            b.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void Render()
    {
        var list = StoryHouseCatalog.ByCategory(_category);

        if (!string.IsNullOrWhiteSpace(_search))
        {
            var q = _search.Trim();
            list = list.Where(s => s.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || s.Summary.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (_favOnly)
        {
            var favs = Favorites();
            list = list.Where(s => favs.Contains(s.Id)).ToList();
        }

        StoryList.ItemsSource = list;
        SubtleLabel.Text = _favOnly
            ? $"我的收藏 · {list.Count} 个故事"
            : $"共 {list.Count} 个故事，点一下就能听";

        FavBtn.Text = _favOnly ? "⭐" : "☆";
        FavBtn.BackgroundColor = _favOnly ? Color.FromArgb("#FFD98A") : Color.FromArgb("#1C244E");
        FavBtn.TextColor = _favOnly ? Color.FromArgb("#3A2C00") : Color.FromArgb("#FFE9A8");
    }

    // ── 交互 ────────────────────────────────────────────────

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue ?? string.Empty;
        Render();
    }

    private void OnToggleFavFilter(object sender, EventArgs e)
    {
        _favOnly = !_favOnly;
        Render();
    }

    private async void OnStoryTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not BedtimeStory story) return;
        await Navigation.PushAsync(new StoryPlayerPage(story));
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();   // 从播放页回来时刷新收藏状态
    }
}
