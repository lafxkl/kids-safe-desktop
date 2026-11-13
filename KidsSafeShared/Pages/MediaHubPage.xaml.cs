using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

public partial class MediaHubPage : ContentPage
{
    private readonly MediaHubViewModel _vm;
    private readonly IStoryService _story;
    private bool _miniSpinning;
    private bool _categoriesLoaded;

    public MediaHubPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _vm = new MediaHubViewModel(ServiceProvider.Get<IContentService>());
        _story = ServiceProvider.Get<IStoryService>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ThemeService.Instance.Init();

        UpdateTabs();
        UpdateOnlineBadge();

        PlaybackHub.StateChanged += OnPlaybackStateChanged;

        if (_vm.AudioGroups.Count == 0)
        {
            LoadingRing.IsVisible = true;
            LoadingRing.IsRunning = true;
            await _vm.InitializeAsync();
            LoadingRing.IsRunning = false;
            LoadingRing.IsVisible = false;
            RefreshMiniPlayer();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        PlaybackHub.StateChanged -= OnPlaybackStateChanged;
        StopMiniSpin();
    }

    // ── 在线状态徽标 ────────────────────────────────────────────
    private void UpdateOnlineBadge()
    {
        var online = ServiceProvider.Get<IContentService>().OnlineEnabled;
        OnlineBadge.Text = online ? "🌐 在线" : "📦 离线";
        OnlineBadge.TextColor = online ? Colors.Teal : Color.FromArgb("#00897B");
    }

    // ── Tab 切换 ───────────────────────────────────────────────
    private void OnTabAudio(object sender, EventArgs e) { _vm.SelectedTab = 0; UpdateTabs(); }
    private void OnTabVideo(object sender, EventArgs e) { _vm.SelectedTab = 1; UpdateTabs(); }
    private void OnTabStory(object sender, EventArgs e) { _vm.SelectedTab = 3; UpdateTabs(); EnsureStoryLoaded(); }
    private void OnTabArticle(object sender, EventArgs e) { _vm.SelectedTab = 2; UpdateTabs(); }

    private void UpdateTabs()
    {
        PanelAudio.IsVisible = _vm.SelectedTab == 0;
        PanelVideo.IsVisible = _vm.SelectedTab == 1;
        PanelArticle.IsVisible = _vm.SelectedTab == 2;
        PanelStory.IsVisible = _vm.SelectedTab == 3;

        TabAudio.BackgroundColor = _vm.SelectedTab == 0 ? Color.FromArgb("#FF7043") : Color.FromArgb("#E0E0E0");
        TabAudio.TextColor = _vm.SelectedTab == 0 ? Colors.White : Color.FromArgb("#555555");
        TabVideo.BackgroundColor = _vm.SelectedTab == 1 ? Color.FromArgb("#42A5F5") : Color.FromArgb("#E0E0E0");
        TabVideo.TextColor = _vm.SelectedTab == 1 ? Colors.White : Color.FromArgb("#555555");
        TabStory.BackgroundColor = _vm.SelectedTab == 3 ? Color.FromArgb("#AB47BC") : Color.FromArgb("#E0E0E0");
        TabStory.TextColor = _vm.SelectedTab == 3 ? Colors.White : Color.FromArgb("#555555");
        TabArticle.BackgroundColor = _vm.SelectedTab == 2 ? Color.FromArgb("#66BB6A") : Color.FromArgb("#E0E0E0");
        TabArticle.TextColor = _vm.SelectedTab == 2 ? Colors.White : Color.FromArgb("#555555");
    }

    // ── 故事屋：拉取分类（仅在线时）────────────────────────────
    private async void EnsureStoryLoaded()
    {
        if (_categoriesLoaded) return;
        _categoriesLoaded = true;

        if (!_story.OnlineEnabled)
        {
            ShowStoryHint("📡 故事屋需要开启「在线内容」：\n在家长设置里填写服务器地址，并打开在线内容开关。");
            return;
        }

        StoryLoading.IsVisible = true;
        StoryLoading.IsRunning = true;
        var cats = await _story.GetCategoriesAsync();
        StoryLoading.IsRunning = false;
        StoryLoading.IsVisible = false;

        if (cats.Count == 0)
        {
            ShowStoryHint("😢 暂时没有拿到故事分类，请稍后重试，\n或确认服务器能连接外网。");
            return;
        }

        StoryHint.IsVisible = false;
        CategoryList.Children.Clear();
        foreach (var c in cats)
        {
            var card = new Frame
            {
                CornerRadius = 20,
                HasShadow = false,
                Padding = new Thickness(14, 12),
                BackgroundColor = Color.FromArgb("#F3E5F5"),
                BorderColor = Colors.Transparent
            };
            var inner = new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.Center };
            inner.Children.Add(new Frame
            {
                WidthRequest = 46,
                HeightRequest = 46,
                CornerRadius = 23,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#AB47BC"),
                Content = new Label
                {
                    Text = c.Emoji,
                    FontSize = 24,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                }
            });
            var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            text.Children.Add(new Label { Text = c.Name, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#4A148C") });
            text.Children.Add(new Label { Text = "点开听故事", FontSize = 12, TextColor = Color.FromArgb("#8E24AA") });
            inner.Children.Add(text);
            card.Content = inner;

            var tap = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
            tap.Tapped += (_, _) =>
            {
                ServiceProvider.Get<ISoundService>().PlayClick();
                Navigation.PushAsync(new StoryListPage(c.Id, c.Name, c.Emoji));
            };
            card.GestureRecognizers.Add(tap);
            CategoryList.Children.Add(card);
        }
    }

    private void ShowStoryHint(string text)
    {
        StoryHint.Text = text;
        StoryHint.IsVisible = true;
    }

    private async void OnStorySearch(object sender, EventArgs e)
    {
        var kw = (StorySearch.Text ?? "").Trim();
        if (kw.Length == 0) return;
        ServiceProvider.Get<ISoundService>().PlayClick();
        await Navigation.PushAsync(new StoryListPage(kw));
    }

    // ── 卡片点击 ───────────────────────────────────────────────
    private async void OnAudioTapped(object sender, EventArgs e)
    {
        if (sender is not View view || view.BindingContext is not KidsAudioItem item) return;
        ServiceProvider.Get<ISoundService>().PlayClick();

        var group = _vm.AudioGroups.FirstOrDefault(g => g.Items.Contains(item));
        if (group is null) return;
        await Navigation.PushAfterPressAsync(view, () => new AudioPlayerPage(group, item));
    }

    private async void OnVideoTapped(object sender, EventArgs e)
    {
        if (sender is not View view || view.BindingContext is not ContentItem item) return;
        ServiceProvider.Get<ISoundService>().PlayClick();
        await Navigation.PushAfterPressAsync(view, () => new VideoPlayerPage(item));
    }

    private async void OnArticleTapped(object sender, EventArgs e)
    {
        if (sender is not View view || view.BindingContext is not ContentItem item) return;
        ServiceProvider.Get<ISoundService>().PlayClick();
        await Navigation.PushAfterPressAsync(view, () => new ArticleReaderPage(item));
    }

    // ── 迷你悬浮播放器 ─────────────────────────────────────────
    private void OnMiniTapped(object sender, EventArgs e)
    {
        ServiceProvider.Get<ISoundService>().PlayClick();
        PlaybackHub.Reopen();
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => RefreshMiniPlayer();

    private void RefreshMiniPlayer()
    {
        if (!PlaybackHub.HasItem) { MiniPlayer.IsVisible = false; StopMiniSpin(); return; }

        MiniPlayer.IsVisible = true;
        MiniIcon.Text = PlaybackHub.CurrentEmoji ?? "🎵";
        MiniTitle.Text = PlaybackHub.CurrentTitle ?? string.Empty;
        MiniState.Text = PlaybackHub.IsPlaying ? "正在播放" : "已暂停";
        if (PlaybackHub.IsPlaying) StartMiniSpin();
        else StopMiniSpin();
    }

    private void StartMiniSpin()
    {
        if (_miniSpinning) return;
        _miniSpinning = true;
        var anim = new Animation(v => MiniIcon.Rotation = v, 0, 360);
        anim.Commit(this, "miniSpin", 16, 3000, Easing.Linear, null, () => _miniSpinning);
    }

    private void StopMiniSpin()
    {
        _miniSpinning = false;
        this.AbortAnimation("miniSpin");
        MiniIcon.Rotation = 0;
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
