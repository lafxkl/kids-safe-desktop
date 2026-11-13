using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 留守儿童关爱中心首页。
///
/// 设计上刻意「低门槛」：进来第一件事是点一个表情，不需要打字、不需要登录、
/// 不需要回答任何问题。写完心情也不强制写理由——愿意说才说。
/// </summary>
public partial class CareCenterPage : ContentPage
{
    private string _warm = string.Empty;

    public CareCenterPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BuildMoodGrid();
        BuildChannels();
        PickWarmWord();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    // ── 心情 ────────────────────────────────────────────────

    private void BuildMoodGrid()
    {
        MoodGrid.Children.Clear();

        for (var i = 0; i < CareCatalog.Moods.Length; i++)
        {
            var mood = CareCatalog.Moods[i];
            var cell = new Frame
            {
                Padding = new Thickness(4, 8),
                CornerRadius = 14,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#FFF7FA"),
                BorderColor = Colors.Transparent,
                BindingContext = mood,
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = mood.Emoji, FontSize = 26, HorizontalOptions = LayoutOptions.Center },
                        new Label
                        {
                            Text = mood.Label,
                            FontSize = 11.5,
                            TextColor = Color.FromArgb("#7A4A5C"),
                            HorizontalOptions = LayoutOptions.Center
                        }
                    }
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnMoodTapped;
            cell.GestureRecognizers.Add(tap);

            Grid.SetColumn(cell, i % 4);
            Grid.SetRow(cell, i / 4);
            MoodGrid.Children.Add(cell);
        }
    }

    private async void OnMoodTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not MoodKind mood) return;

        var existing = CareJournal.TodayEntry();
        var note = existing?.Note ?? string.Empty;

        if (existing is null)
        {
            // 第一次记录今天：可以补一句（不写也行）
            var input = await DisplayPromptAsync($"{mood.Emoji} 今天{mood.Label}",
                "想多说一句吗？不写也可以。", "写一句", "不用了", "比如：今天自己煮了面", maxLength: 40);
            if (!string.IsNullOrWhiteSpace(input)) note = input.Trim();
        }

        var (firstToday, award) = CareJournal.SaveMood(mood.Id, note);

        MoodSavedLabel.IsVisible = true;
        MoodSavedLabel.Text = firstToday
            ? $"已记下：{mood.Label}　+{award.Stars} ⭐（今天共 {award.Today} 颗）"
            : $"已改成：{mood.Label}";

        try { ServiceProvider.Get<ISoundService>().PlayCorrect(); } catch { /* 忽略 */ }

        Refresh();
    }

    private void Refresh()
    {
        StarPill.Text = $"⭐ {StarCenter.Total}";
        StreakLabel.Text = $"🔥 {CareJournal.Streak()} 天";

        var today = CareJournal.TodayEntry();
        if (today is not null)
        {
            GreetLabel.Text = $"今天：{today.Mood.Emoji} {today.Mood.Label}";
            MoodHintLabel.Text = string.IsNullOrWhiteSpace(today.Note)
                ? "想换一个心情，随时点就行"
                : $"“{today.Note}”";
        }
        else
        {
            GreetLabel.Text = "今天心情怎么样？";
            MoodHintLabel.Text = "点一个最像你的表情";
        }

        BuildRecentBar();

        var letters = CareJournal.Letters();
        LettersCountLabel.Text = letters.Count > 0
            ? $"已经写了 {letters.Count} 句，可以读给你听"
            : "写下来，也可以读给你听";
    }

    private void BuildRecentBar()
    {
        RecentBar.Children.Clear();

        foreach (var (day, entry) in CareJournal.Recent(7))
        {
            var col = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.Center };

            col.Add(new Frame
            {
                WidthRequest = 34,
                HeightRequest = 34,
                CornerRadius = 17,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = entry is null ? Color.FromArgb("#F1EFF3") : entry.Mood.Color,
                Content = new Label
                {
                    Text = entry?.Mood.Emoji ?? "·",
                    FontSize = 17,
                    TextColor = entry is null ? Color.FromArgb("#C9C3CE") : Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            });

            col.Add(new Label
            {
                Text = day == DateTime.Today ? "今天" : day.ToString("M/d"),
                FontSize = 9.5,
                TextColor = Color.FromArgb("#B0A2AC"),
                HorizontalOptions = LayoutOptions.Center
            });

            RecentBar.Children.Add(col);
        }
    }

    // ── 温暖的话 ────────────────────────────────────────────

    private void PickWarmWord()
    {
        var rnd = new Random();
        var next = CareCatalog.WarmWords[rnd.Next(CareCatalog.WarmWords.Length)];
        if (next == _warm && CareCatalog.WarmWords.Length > 1)
            next = CareCatalog.WarmWords[(Array.IndexOf(CareCatalog.WarmWords, next) + 1) % CareCatalog.WarmWords.Length];

        _warm = next;
        WarmLabel.Text = "“" + next + "”";
    }

    private void OnNextWarm(object sender, EventArgs e) => PickWarmWord();

    private void OnSpeakWarm(object sender, EventArgs e) => _ = Tts.SpeakAsync(_warm, "zh");

    // ── 求助渠道 ────────────────────────────────────────────

    private void BuildChannels()
    {
        ChannelHost.Children.Clear();

        foreach (var c in CareCatalog.Channels)
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

            grid.Add(new Label { Text = c.Emoji, FontSize = 20, VerticalOptions = LayoutOptions.Start }, 0, 0);

            var info = new VerticalStackLayout { Spacing = 1 };
            info.Add(new Label
            {
                Text = c.Urgent ? c.Name + "（紧急）" : c.Name,
                FontSize = 13.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = c.Urgent ? Color.FromArgb("#C62828") : Color.FromArgb("#4E3B45")
            });
            info.Add(new Label
            {
                Text = c.When,
                FontSize = 11.5,
                TextColor = Color.FromArgb("#8A7B84"),
                LineHeight = 1.35
            });
            grid.Add(info, 1, 0);

            if (!string.IsNullOrWhiteSpace(c.Number))
            {
                var number = c.Number;
                var call = new Button
                {
                    Text = "拨打",
                    FontSize = 12,
                    HeightRequest = 32,
                    Padding = new Thickness(10, 0),
                    CornerRadius = 16,
                    BackgroundColor = c.Urgent ? Color.FromArgb("#FFCDD2") : Color.FromArgb("#E3F2FD"),
                    TextColor = c.Urgent ? Color.FromArgb("#B71C1C") : Color.FromArgb("#1565C0"),
                    VerticalOptions = LayoutOptions.Center
                };
                call.Clicked += async (_, _) =>
                {
                    try
                    {
                        await Launcher.Default.OpenAsync($"tel:{number}");
                    }
                    catch
                    {
                        await DisplayAlert("拨号失败", $"请手动拨打 {number}", "好");
                    }
                };
                grid.Add(call, 2, 0);
            }

            ChannelHost.Children.Add(new Frame
            {
                Padding = new Thickness(10, 9),
                CornerRadius = 14,
                HasShadow = false,
                BackgroundColor = c.Urgent ? Color.FromArgb("#FFF5F5") : Color.FromArgb("#F7F9FC"),
                BorderColor = Colors.Transparent,
                Content = grid
            });
        }
    }

    // ── 导航 ────────────────────────────────────────────────

    private async void OnOpenJournal(object sender, EventArgs e)
        => await Navigation.PushAsync(new MoodJournalPage());

    private async void OnOpenDiary(object sender, EventArgs e)
        => await Navigation.PushAsync(new DiaryPage());

    private async void OnOpenLessons(object sender, EventArgs e)
        => await Navigation.PushAsync(new CareLessonPage("kids"));

    private async void OnOpenGrowthGuide(object sender, EventArgs e)
        => await Navigation.PushAsync(new ParentGuidePage());

    /// <summary>儿童心理学 · 自闭症关怀（公益科普，离线内置网页）。</summary>
    private async void OnOpenChildPsych(object sender, EventArgs e)
        => await Navigation.PushAsync(new ChildPsychPage());

    private async void OnOpenDepression(object sender, EventArgs e)
        => await Navigation.PushAsync(new DepressionPage());

    private async void OnOpenEdu36(object sender, EventArgs e)
        => await Navigation.PushAsync(new Edu36Page());

    private async void OnOpenParents(object sender, EventArgs e)
        => await Navigation.PushAsync(new CareLessonPage("parents"));

    private async void OnOpenBreathing(object sender, EventArgs e)
        => await Navigation.PushAsync(new BreathingPage());

    private async void OnOpenLetters(object sender, EventArgs e)
        => await Navigation.PushAsync(new LetterToParentsPage());

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
