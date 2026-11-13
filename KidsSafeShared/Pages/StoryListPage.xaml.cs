using System.Collections.ObjectModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 故事列表页：按分类浏览，或用关键词搜索（来自家长服务器代理的故事大全）。
/// 支持分页「加载更多」。
/// </summary>
public partial class StoryListPage : ContentPage
{
    private readonly IStoryService _story;
    private readonly string _mode;     // "category" 或 "search"
    private readonly int _typeId;
    private readonly string _keyword;
    private int _page = 1;
    private bool _loading;
    private bool _done;

    public ObservableCollection<StorySummary> Stories { get; } = new();

    // 分类浏览
    public StoryListPage(int typeId, string categoryName, string emoji) : this()
    {
        _mode = "category";
        _typeId = typeId;
        TitleLabel.Text = emoji + " " + categoryName;
    }

    // 关键词搜索
    public StoryListPage(string keyword) : this()
    {
        _mode = "search";
        _keyword = keyword;
        TitleLabel.Text = "🔍 " + keyword;
    }

    private StoryListPage()
    {
        InitializeComponent();

        // 学习激励入口：孩子自己点「学完了」，进入激励页并记星星
        LearnCheckIn.Attach(this, "故事屋", "📖", "story_online", 2, "在故事屋听故事");
        NavigationPage.SetHasNavigationBar(this, false);
        _story = ServiceProvider.Get<IStoryService>();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Stories.Count == 0)
            await LoadPage(1);
    }

    private async Task LoadPage(int page)
    {
        if (_loading) return;
        _loading = true;
        Busy.IsVisible = true;
        Busy.IsRunning = true;

        List<StorySummary> items = _mode == "category"
            ? await _story.GetListAsync(_typeId, page)
            : await _story.SearchAsync(_keyword, page);

        Busy.IsRunning = false;
        Busy.IsVisible = false;
        _loading = false;

        items ??= new List<StorySummary>();

        if (page == 1 && items.Count == 0)
        {
            EmptyHint.IsVisible = true;
            LoadMore.IsVisible = false;
            return;
        }

        foreach (var it in items)
            Stories.Add(it);

        CountLabel.Text = $"共 {Stories.Count} 篇";
        if (items.Count < 10) _done = true;
        LoadMore.IsVisible = !_done;
    }

    private async void OnStoryTapped(object sender, EventArgs e)
    {
        if (sender is not View view || view.BindingContext is not StorySummary s) return;
        ServiceProvider.Get<ISoundService>().PlayClick();
        await Navigation.PushAsync(new StoryReaderPage(s.Id, s.Title, s.Type));
    }

    private async void OnLoadMore(object sender, EventArgs e)
    {
        if (_loading || _done) return;
        _page++;
        await LoadPage(_page);
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
