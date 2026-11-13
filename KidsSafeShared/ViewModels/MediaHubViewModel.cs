using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 媒体中心视图模型：汇聚「宝宝听 / 宝宝看 / 亲子阅」三类内容。
/// 数据来自 <see cref="IContentService"/>——以随包离线内容为基础，
/// 开启在线后从家长自建服务器拉取经审核的补充内容。
/// </summary>
public class MediaHubViewModel : BaseViewModel
{
    private readonly IContentService _content;
    private int _selectedTab;
    private bool _loading;

    public ObservableCollection<KidsAudioGroup> AudioGroups { get; } = new();
    public ObservableCollection<ContentGroup> VideoGroups { get; } = new();
    public ObservableCollection<ContentGroup> ArticleGroups { get; } = new();

    /// <summary>0=宝宝听 1=宝宝看 2=亲子阅。</summary>
    public int SelectedTab
    {
        get => _selectedTab;
        set { _selectedTab = value; OnPropertyChanged(); }
    }

    public bool Loading
    {
        get => _loading;
        set { _loading = value; OnPropertyChanged(); }
    }

    public MediaHubViewModel(IContentService content)
    {
        _content = content;
    }

    public async Task InitializeAsync()
    {
        if (Loading) return;
        Loading = true;
        try
        {
            var audio = await _content.GetAudioGroupsAsync();
            AudioGroups.Clear();
            foreach (var g in audio) AudioGroups.Add(g);

            var video = await _content.GetVideoGroupsAsync();
            VideoGroups.Clear();
            foreach (var g in video) VideoGroups.Add(g);

            var articles = await _content.GetArticleGroupsAsync();
            ArticleGroups.Clear();
            foreach (var g in articles) ArticleGroups.Add(g);
        }
        finally
        {
            Loading = false;
        }
    }
}
