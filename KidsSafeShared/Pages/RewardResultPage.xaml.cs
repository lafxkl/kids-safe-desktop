using KidsSafeDesktop.Models;
using Microsoft.Maui.Layouts;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 通用「本局结算 · 奖励页」。
///
/// 所有小游戏共用这一个页面：游戏侧只需要提供「游戏名 / 图标 / 成绩 / 星级」，
/// 记账（星星、连续天数、勋章）与动画都在这里完成，避免 19 个游戏各写一套结算。
/// 通过 <see cref="Helpers.RewardWatcher"/> 自动挂到各游戏的结束状态上。
/// </summary>
public partial class RewardResultPage : ContentPage
{
    private readonly RewardOutcome _outcome;
    private readonly AwardResult _award;
    private readonly Func<Task>? _onReplay;
    private bool _animated;

    public RewardResultPage(RewardOutcome outcome, AwardResult award, Func<Task>? onReplay = null)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _outcome = outcome;
        _award = award;
        _onReplay = onReplay;

        Bind();
    }

    private void Bind()
    {
        EmojiLabel.Text = _outcome.Emoji;
        TitleLabel.Text = _outcome.Title;
        HeadlineLabel.Text = _outcome.Headline;
        DetailLabel.Text = _outcome.Detail;
        CaptionLabel.Text = $"{StarCenter.KindName(_outcome.Kind)} · 本局结束";
        TotalPill.Text = $"⭐ {_award.Total}";

        AwardLabel.Text = _outcome.HasAward ? $"+{_outcome.Award} 颗星星" : "继续加油！";
        LedgerLabel.Text = $"一共 {_award.Total} 颗 · 今天 {_award.Today} 颗";
        StreakLabel.Text = _award.Streak >= 2 ? $"🔥 {_award.Streak} 天" : string.Empty;

        EncourageLabel.Text = string.IsNullOrWhiteSpace(_outcome.Encourage) ? BuildFallback() : _outcome.Encourage;

        NextTipLabel.Text = _outcome.NextTip;
        NextTipLabel.IsVisible = !string.IsNullOrWhiteSpace(_outcome.NextTip);

        BuildStars();
        BuildBadges();
    }

    private string BuildFallback() => _outcome.Stars switch
    {
        5 => "你这次做得又快又准，说明真的掌握了。给自己鼓个掌！",
        4 => "已经很棒了，只差一点点就满分，下次注意更细心就好。",
        3 => "完成了一局就是进步。玩得开心，比赢更重要。",
        2 => "有点难，但你没有放弃。慢慢来，多试几次就会了。",
        _ => "愿意尝试就很勇敢。先休息一下，下次再来挑战。"
    };

    private void BuildStars()
    {
        StarRow.Children.Clear();
        for (var i = 0; i < 5; i++)
        {
            var filled = i < _outcome.Stars;
            StarRow.Children.Add(new Label
            {
                Text = filled ? "⭐" : "☆",
                FontSize = 26,
                TextColor = filled ? Color.FromArgb("#FFC107") : Color.FromArgb("#DCD4BE"),
                Scale = 0,
                Opacity = 0
            });
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
            TextColor = Color.FromArgb("#8A6D00"),
            Margin = new Thickness(4, 4, 0, 0)
        });

        foreach (var b in badges)
        {
            var frame = new Frame
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
            };
            BadgeHost.Children.Add(frame);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_animated) return;
        _animated = true;

        try
        {
            EmojiBubble.Scale = 0.5;
            EmojiBubble.Opacity = 0;
            await Task.WhenAll(
                EmojiBubble.ScaleTo(1.08, 320, Easing.CubicOut),
                EmojiBubble.FadeTo(1, 260));

            await EmojiBubble.ScaleTo(1.0, 140, Easing.CubicIn);

            foreach (var child in StarRow.Children)
            {
                if (child is not Label star) continue;
                star.Scale = 0.2;
                star.Opacity = 0;
                _ = star.ScaleTo(1.25, 220, Easing.CubicOut)
                        .ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () => await star.ScaleTo(1.0, 120)));
                await Task.Delay(140);
            }

            AwardCard.Scale = 0.92;
            AwardCard.Opacity = 0;
            _ = AwardCard.FadeTo(1, 240);
            await AwardCard.ScaleTo(1.0, 260, Easing.CubicOut);

            FireConfetti();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Reward] 入场动画异常（已忽略）：{ex.Message}");
        }
    }

    /// <summary>彩带：纯装饰，用 emoji 标签做一次性下落，结束即移除，不常驻内存。</summary>
    private void FireConfetti()
    {
        var rnd = new Random();
        var glyphs = new[] { "🎉", "⭐", "✨", "🌟", "🎊", "💫", "🎈", "🏆" };
        var w = Width > 0 ? Width : 360;

        for (var i = 0; i < 16; i++)
        {
            var lbl = new Label
            {
                Text = glyphs[rnd.Next(glyphs.Length)],
                FontSize = 18 + rnd.Next(14),
                Opacity = 0
            };

            AbsoluteLayout.SetLayoutBounds(lbl, new Rect(rnd.NextDouble() * Math.Max(w - 44, 40), -50, 44, 44));
            AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
            ConfettiLayer.Children.Add(lbl);

            var fall = Height > 0 ? Height + 80 : 700;
            var duration = (uint)(1500 + rnd.Next(0, 900));

            lbl.TranslateTo(0, fall, duration, Easing.CubicIn)
               .ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => ConfettiLayer.Children.Remove(lbl)));

            lbl.FadeTo(0.95, 180);
            lbl.RotateTo(rnd.Next(-360, 360), duration);
        }
    }

    // ── 按钮 ────────────────────────────────────────────────

    private async void OnClose(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnReplay(object sender, EventArgs e)
    {
        if (_onReplay is not null)
        {
            try { await _onReplay(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Reward] 重开失败：{ex.Message}"); }
        }

        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async void OnStarBoard(object sender, EventArgs e)
        => await Navigation.PushAsync(new StarBoardPage());

    private async void OnBackToHub(object sender, EventArgs e)
    {
        // 一路退回「小游戏乐园」：结算页 + 游戏页都在栈顶之上
        while (Navigation.NavigationStack.Count > 2)
            await Navigation.PopAsync(false);
    }
}
