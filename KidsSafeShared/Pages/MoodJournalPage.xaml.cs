using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>心情日记：14 天趋势 + 统计 + 全部记录。</summary>
public partial class MoodJournalPage : ContentPage
{
    public MoodJournalPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        Render();
    }

    private void Render()
    {
        var recent = CareJournal.Recent(14);
        var all = CareJournal.All();

        // 趋势
        TrendHost.Children.Clear();
        TrendHost.ColumnDefinitions.Clear();
        TrendHost.RowDefinitions.Clear();
        TrendHost.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        for (var i = 0; i < recent.Count; i++)
        {
            TrendHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (day, entry) = recent[i];

            var col = new VerticalStackLayout
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Center
            };

            col.Add(new Label
            {
                Text = entry?.Mood.Emoji ?? "·",
                FontSize = 15,
                HorizontalOptions = LayoutOptions.Center
            });

            col.Add(new BoxView
            {
                WidthRequest = 18,
                HeightRequest = entry is null ? 14 : 44,
                CornerRadius = 6,
                Color = entry?.Mood.Color ?? Color.FromArgb("#EFEAF0"),
                VerticalOptions = LayoutOptions.End
            });

            col.Add(new Label
            {
                Text = day == DateTime.Today ? "今" : day.Day.ToString(),
                FontSize = 9,
                TextColor = day == DateTime.Today ? Color.FromArgb("#E91E63") : Color.FromArgb("#B0A2AC"),
                HorizontalOptions = LayoutOptions.Center
            });

            Grid.SetColumn(col, i);
            TrendHost.Children.Add(col);
        }

        // 统计
        DaysLabel.Text = all.Count.ToString();
        StreakLabel.Text = CareJournal.Streak().ToString();

        var most = all.GroupBy(e => e.MoodId)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault();
        MostLabel.Text = most is null ? "—" : CareCatalog.MoodOf(most.Key).Emoji;

        // 一句话观察（只描述事实，不做评价）
        var recorded = recent.Count(r => r.Entry is not null);
        if (all.Count == 0)
        {
            InsightLabel.Text = "还没有记录。心情不好说出口的时候，点一个表情就够了——不用解释。";
        }
        else
        {
            var text = $"最近 14 天你记录了 {recorded} 天。";
            var lonelyCount = all.Count(e => e.MoodId == "lonely");
            var sadCount = all.Count(e => e.MoodId == "sad");

            if (lonelyCount + sadCount >= 3)
                text += "「想人」和「难过」出现得比较多。这不代表你不好，只是你最近确实有点累。要不要试试「呼吸放松」，或者打 12355 找个人说说？";
            else if (recorded >= 5)
                text += "能坚持记录自己的心情，是很厉害的一件事——很多人一辈子都做不到。";
            else
                text += "想到就来记一下，不用每天都记。";

            InsightLabel.Text = text;
        }

        // 历史
        HistoryHost.Children.Clear();
        var ordered = all.OrderByDescending(e => e.Date).ToList();

        if (ordered.Count == 0)
        {
            HistoryHost.Children.Add(new Label
            {
                Text = "还没有记录",
                FontSize = 13,
                TextColor = Color.FromArgb("#B08A98"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16)
            });
            return;
        }

        foreach (var entry in ordered)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            grid.Add(new Label
            {
                Text = entry.Mood.Emoji,
                FontSize = 24,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var info = new VerticalStackLayout { Spacing = 1 };
            info.Add(new Label
            {
                Text = entry.Mood.Label,
                FontSize = 14.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5D3A47")
            });
            if (!string.IsNullOrWhiteSpace(entry.Note))
                info.Add(new Label
                {
                    Text = entry.Note,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#8A7B84"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 2
                });
            grid.Add(info, 1, 0);

            grid.Add(new Label
            {
                Text = entry.Date.Length >= 10 ? entry.Date[5..] : entry.Date,
                FontSize = 11.5,
                TextColor = Color.FromArgb("#B0A2AC"),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            HistoryHost.Children.Add(new Frame
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
}
