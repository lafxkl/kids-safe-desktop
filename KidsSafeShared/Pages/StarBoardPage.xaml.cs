using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 我的星星：总览 + 勋章墙 + 最近记录。
/// 结算页与激励页都能进来，所以这里必须能在任何时刻独立渲染，
/// 不依赖外部传参，所有数据都从 <see cref="StarCenter"/> 现取。
/// </summary>
public partial class StarBoardPage : ContentPage
{
    public StarBoardPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        var total = StarCenter.Total;
        var badges = StarCenter.Badges();
        var unlocked = badges.Count(b => b.Unlocked);

        TotalLabel.Text = total.ToString();
        TodayLabel.Text = StarCenter.Today.ToString();
        StreakLabel.Text = $"{StarCenter.Streak} 天";
        SubLabel.Text = $"玩过 {StarCenter.DistinctGamesPlayed} 款游戏 · 记录保存在本机";
        BadgeSummaryLabel.Text = $"已解锁 {unlocked} / {badges.Count} 枚";

        RenderBadges(badges);
        RenderRecords();
    }

    /// <summary>勋章墙：三列自适应，用 FlexLayout 而不是 CollectionView，避免和外部 ScrollView 嵌套。</summary>
    private void RenderBadges(List<StarBadge> badges)
    {
        BadgeHost.Children.Clear();

        foreach (var badge in badges)
        {
            var cell = new Frame
            {
                Padding = new Thickness(6, 10),
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = badge.Unlocked ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#F7F4EC"),
                BorderColor = badge.Unlocked ? Color.FromArgb("#FFD54F") : Color.FromArgb("#EAE3D2"),
                WidthRequest = 104,
                Margin = new Thickness(0, 0, 8, 8),
                Content = new VerticalStackLayout
                {
                    Spacing = 3,
                    Children =
                    {
                        new Label
                        {
                            Text = badge.LockEmoji,
                            FontSize = 25,
                            HorizontalOptions = LayoutOptions.Center,
                            Opacity = badge.Unlocked ? 1 : 0.45
                        },
                        new Label
                        {
                            Text = badge.Name,
                            FontSize = 12,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = badge.Unlocked ? Color.FromArgb("#6B5A2A") : Color.FromArgb("#A79878"),
                            HorizontalOptions = LayoutOptions.Center,
                            HorizontalTextAlignment = TextAlignment.Center,
                            LineBreakMode = LineBreakMode.TailTruncation
                        },
                        new Label
                        {
                            Text = badge.StateText,
                            FontSize = 10,
                            TextColor = Color.FromArgb("#A79878"),
                            HorizontalOptions = LayoutOptions.Center
                        }
                    }
                }
            };

            // 点一下看说明
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                var status = badge.Unlocked
                    ? "已获得 ✓"
                    : "还没解锁，进度：" + badge.Progress;
                await DisplayAlert($"{badge.Emoji} {badge.Name}", badge.Description + "\n\n" + status, "好");
            };
            cell.GestureRecognizers.Add(tap);

            BadgeHost.Children.Add(cell);
        }
    }

    private void RenderRecords()
    {
        RecordHost.Children.Clear();
        var records = StarCenter.Recent(20);

        if (records.Count == 0)
        {
            RecordHost.Children.Add(new Label
            {
                Text = "还没有记录。去玩一局游戏，或者学一次吧～",
                FontSize = 13,
                TextColor = Color.FromArgb("#9C8A5A"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16)
            });
            return;
        }

        foreach (var r in records)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12
            };

            grid.Add(new Label { Text = r.Emoji, FontSize = 22, VerticalOptions = LayoutOptions.Center }, 0, 0);

            var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
            info.Add(new Label
            {
                Text = r.Title,
                FontSize = 14.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5D4E2A"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
            info.Add(new Label
            {
                Text = $"{StarCenter.KindName(r.Kind)} · {r.TimeText}",
                FontSize = 11,
                TextColor = Color.FromArgb("#9C8A5A")
            });
            grid.Add(info, 1, 0);

            grid.Add(new Label
            {
                Text = r.StarsText,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#EF6C00"),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            RecordHost.Children.Add(new Frame
            {
                Padding = new Thickness(12, 10),
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                Content = grid
            });
        }
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnReset(object sender, EventArgs e)
    {
        var ok = await DisplayAlert("清空星星记录？",
            "会把星星总数、勋章和记录全部清零。\n\n只影响这台设备，做完不能恢复。",
            "清空", "算了");
        if (!ok) return;

        StarCenter.Reset();
        Render();
    }
}
