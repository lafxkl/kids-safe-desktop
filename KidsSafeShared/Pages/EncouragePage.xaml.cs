using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 通用「学习激励页」。
///
/// 定位和游戏结算页刻意区分：游戏是「一局结束」，学习是「今天又前进了一步」，
/// 所以这里讲的是——今天的累计、具体做到的事、下一步做什么，而不是比分和输赢。
/// 学习页通过 <see cref="Helpers.LearnCheckIn"/> 把入口挂到页面右下角，孩子自己点「学完了」。
/// </summary>
public partial class EncouragePage : ContentPage
{
    private readonly RewardOutcome _outcome;
    private readonly AwardResult _award;
    private readonly Func<Task>? _onContinue;
    private bool _animated;

    /// <summary>每天的学习小目标（次）。达成后文案自动变成「目标达成」。</summary>
    private const int DailyGoal = 2;

    private static readonly (string Text, string From)[] Quotes =
    {
        ("不积跬步，无以至千里。", "荀子"),
        ("读书破万卷，下笔如有神。", "杜甫"),
        ("温故而知新，可以为师矣。", "孔子"),
        ("少壮不努力，老大徒伤悲。", "汉乐府"),
        ("学而不思则罔，思而不学则殆。", "孔子"),
        ("书山有路勤为径，学海无涯苦作舟。", "韩愈"),
        ("每天进步一点点，一年就是 365 步。", "成长小提醒"),
        ("问问题不是笨，是脑子在长大。", "成长小提醒"),
        ("学不会的时候，说明你正在学新东西。", "成长小提醒"),
        ("会休息的孩子，才学得更久。", "成长小提醒"),
        ("把学到的东西讲给别人听，就真的记住了。", "成长小提醒"),
        ("慢慢来，比较快。", "成长小提醒")
    };

    public EncouragePage(RewardOutcome outcome, AwardResult award, Func<Task>? onContinue = null)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _outcome = outcome;
        _award = award;
        _onContinue = onContinue;

        Bind();
    }

    private void Bind()
    {
        EmojiLabel.Text = _outcome.Emoji;
        TitleLabel.Text = _outcome.Title;
        HeadlineLabel.Text = string.IsNullOrWhiteSpace(_outcome.Headline) ? "又学会一点点！" : _outcome.Headline;
        DetailLabel.Text = _outcome.Detail;
        CaptionLabel.Text = $"{StarCenter.KindName(_outcome.Kind)} · 今日打卡";
        TotalPill.Text = $"⭐ {_award.Total}";

        AwardLabel.Text = _outcome.HasAward ? $"+{_outcome.Award} 颗星星" : "继续加油！";
        LedgerLabel.Text = $"一共 {_award.Total} 颗 · 今天 {_award.Today} 颗";
        StreakLabel.Text = _award.Streak >= 2 ? $"🔥 {_award.Streak} 天" : string.Empty;

        EncourageLabel.Text = string.IsNullOrWhiteSpace(_outcome.Encourage) ? BuildFallback() : _outcome.Encourage;

        var q = Quotes[Math.Abs(Environment.TickCount / 1000) % Quotes.Length];
        QuoteLabel.Text = "“" + q.Text + "”";
        QuoteFromLabel.Text = "—— " + q.From;

        BuildGoal();
        BuildBadges();
    }

    private string BuildFallback() => "学习不是比谁快，而是比谁能坚持。今天你来了，就已经赢过昨天的自己。";

    private void BuildGoal()
    {
        var todayLearns = StarCenter.Recent(80).Count(r => r.Kind == "learn" && r.Time.Date == DateTime.Today);
        GoalCount.Text = $"{Math.Min(todayLearns, DailyGoal)} / {DailyGoal} 次";

        if (todayLearns >= DailyGoal)
        {
            GoalTitle.Text = "今日小目标 · 已达成 🎉";
            GoalHint.Text = "了不起！今天的学习目标完成了，可以放心去玩啦。";
            GoalFill.Color = Color.FromArgb("#66BB6A");
        }
        else
        {
            GoalTitle.Text = "今日小目标";
            GoalHint.Text = todayLearns == 0
                ? "今天还没开始，学一次试试？"
                : "再来一次就达成今天的小目标啦。";
            GoalFill.Color = Color.FromArgb("#64B5F6");
        }
    }

    private void BuildBadges()
    {
        BadgeHost.Children.Clear();
        var badges = _award.NewBadges;
        BadgeHost.IsVisible = badges.Count > 0;
        if (badges.Count == 0) return;

        BadgeHost.Children.Add(new Label
        {
            Text = "🎖️ 解锁新勋章",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3E4A63"),
            Margin = new Thickness(4, 4, 0, 0)
        });

        foreach (var b in badges)
        {
            BadgeHost.Children.Add(new Frame
            {
                Padding = new Thickness(14, 10),
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#FFF3C4"),
                BorderColor = Color.FromArgb("#FFD54F"),
                Content = new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label { Text = b.Emoji, FontSize = 26, VerticalOptions = LayoutOptions.Center },
                        new VerticalStackLayout
                        {
                            Spacing = 1,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label { Text = b.Name, FontSize = 15.5, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#7A5A00") },
                                new Label { Text = b.Description, FontSize = 12, TextColor = Color.FromArgb("#9C8646") }
                            }
                        }
                    }
                }
            });
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        UpdateGoalBar();

        if (_animated) return;
        _animated = true;

        try
        {
            EmojiBubble.Scale = 0.6;
            EmojiBubble.Opacity = 0;
            await Task.WhenAll(EmojiBubble.ScaleTo(1.06, 300, Easing.CubicOut), EmojiBubble.FadeTo(1, 240));
            _ = EmojiBubble.ScaleTo(1.0, 120, Easing.CubicIn);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Encourage] 入场动画异常（已忽略）：{ex.Message}");
        }
    }

    private void UpdateGoalBar()
    {
        var todayLearns = StarCenter.Recent(80).Count(r => r.Kind == "learn" && r.Time.Date == DateTime.Today);
        var ratio = Math.Clamp(todayLearns / (double)DailyGoal, 0, 1);
        var usable = (Width > 100 ? Width : 360) - 78;
        GoalFill.WidthRequest = Math.Max(6, usable * ratio);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 100) UpdateGoalBar();
    }

    // ── 按钮 ────────────────────────────────────────────────

    private async void OnClose(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnContinue(object sender, EventArgs e)
    {
        if (_onContinue is not null)
        {
            try { await _onContinue(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Encourage] 继续学习失败：{ex.Message}"); }
        }
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async void OnStarBoard(object sender, EventArgs e)
        => await Navigation.PushAsync(new StarBoardPage());
}
