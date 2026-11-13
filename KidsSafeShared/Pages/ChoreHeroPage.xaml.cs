using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 小小家务英雄：家务任务打卡 → 家长确认 → 家庭积分 → 奖励兑换 → 周报统计。
///
/// 角色模型刻意做成「同页双角色」：孩子看到的是打卡和兑换，切到家长模式才会出现
/// 确认 / 驳回 / 增删任务。切换需要家长密码（与全应用同一套 PIN）。
/// 积分账本独立于「我的星星」，因为兑换需要扣减，那是家务系统自己的经济规则。
/// </summary>
public partial class ChoreHeroPage : ContentPage
{
    private bool ParentMode => ChoreService.Store.ParentMode;

    private int _tab;                       // 0 任务 / 1 奖励 / 2 周报
    private int _weekOffset;
    private ChoreDifficulty? _taskFilter;   // null = 全部
    private bool _rewardAffordableOnly;
    private bool _parentVerifiedThisSession;

    private static readonly Color Yellow = Color.FromArgb("#FFD54F");
    private static readonly Color YellowDeep = Color.FromArgb("#F5C518");
    private static readonly Color Ink = Color.FromArgb("#5B4500");
    private static readonly Color Blue = Color.FromArgb("#4A9FCB");
    private static readonly Color White = Colors.White;

    public ChoreHeroPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        // 家长模式只在本次进入有效：不能因为上次切过就一直停在家长视角
        ChoreService.Store.ParentMode = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await ServiceProvider.Get<ISettingsService>().LoadAsync(); } catch { /* 用默认设置继续 */ }

        BuildChips();
        RenderAll();
    }

    // ── 渲染入口 ────────────────────────────────────────────

    private void RenderAll()
    {
        RenderRole();
        RenderPoints();
        RenderTasks();
        RenderRewards();
        RenderReport();
        ApplyTab();
    }

    private void RenderRole()
    {
        RoleLabel.Text = ParentMode ? "家庭积分（家长模式）" : "我的积分";
        RoleBtn.Text = ParentMode ? "🧒 我是孩子" : "👨‍👩‍👧 家长";
        RoleBtn.BackgroundColor = ParentMode ? Color.FromArgb("#8AB4FF") : Color.FromArgb("#FFFFFF99");
        AddTaskBtn.IsVisible = ParentMode;
        AddRewardBtn.IsVisible = ParentMode;
    }

    private void RenderPoints()
    {
        var p = ChoreService.Store.Points;
        PointsLabel.Text = p.Current.ToString();
        TotalLabel.Text = $"🏆 累计 {p.Total}";
        ExchangedLabel.Text = $"🎁 已兑 {p.Exchanged}";
    }

    // ── 任务 ────────────────────────────────────────────────

    private void RenderTasks()
    {
        var store = ChoreService.Store;
        var map = ChoreService.TodayMap();

        // 统计条：按全部任务算，不受筛选影响
        var done = map.Values.Count(r => r.Status == ChoreStatus.Confirmed);
        var pending = map.Values.Count(r => r.Status == ChoreStatus.Pending);
        StatDone.Text = done.ToString();
        StatPending.Text = pending.ToString();
        StatTodo.Text = Math.Max(0, store.Tasks.Count - done - pending).ToString();

        // 筛选按钮配色
        foreach (var child in TaskChips.Children)
        {
            if (child is not Button b) continue;
            var value = (b.CommandParameter as string) ?? "all";
            var selected = value switch
            {
                "all" => _taskFilter is null,
                "easy" => _taskFilter == ChoreDifficulty.Easy,
                "medium" => _taskFilter == ChoreDifficulty.Medium,
                _ => _taskFilter == ChoreDifficulty.Hard
            };
            b.BackgroundColor = selected ? Yellow : White;
            b.TextColor = selected ? Ink : Color.FromArgb("#7A6A3A");
        }

        TaskHost.Children.Clear();

        var list = store.Tasks
            .Where(t => _taskFilter is null || t.Difficulty == _taskFilter)
            .ToList();

        if (list.Count == 0)
        {
            TaskHost.Children.Add(EmptyHint("📭", "还没有任务，让家长添加一个吧"));
            return;
        }

        foreach (var task in list)
        {
            map.TryGetValue(task.Id, out var rec);
            TaskHost.Children.Add(BuildTaskCard(task, rec));
        }
    }

    private View BuildTaskCard(ChoreTask task, ChoreRecord? rec)
    {
        var status = rec?.Status;
        var card = new Frame
        {
            Padding = new Thickness(14, 12),
            CornerRadius = 18,
            HasShadow = false,
            BorderColor = Colors.Transparent,
            BackgroundColor = status switch
            {
                ChoreStatus.Pending => Color.FromArgb("#FFF8E8"),
                ChoreStatus.Confirmed => Color.FromArgb("#F0FBF0"),
                ChoreStatus.Rejected => Color.FromArgb("#FFF0F0"),
                _ => White
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 12
        };

        grid.Add(new Frame
        {
            WidthRequest = 46,
            HeightRequest = 46,
            CornerRadius = 23,
            Padding = 0,
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#FFF3D6"),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = task.Emoji,
                FontSize = 24,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        }, 0, 0);

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Add(new Label
        {
            Text = task.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4E3B12")
        });

        var meta = new HorizontalStackLayout { Spacing = 6 };
        meta.Add(new Label
        {
            Text = $"{task.DifficultyEmoji} {task.DifficultyLabel}",
            FontSize = 11,
            TextColor = task.DifficultyColor,
            BackgroundColor = task.DifficultyBg,
            Padding = new Thickness(6, 2)
        });
        meta.Add(new Label
        {
            Text = $"⭐ {task.Points} 分",
            FontSize = 11.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ink,
            BackgroundColor = Yellow,
            Padding = new Thickness(7, 2)
        });
        info.Add(meta);

        if (status == ChoreStatus.Rejected && !string.IsNullOrWhiteSpace(rec?.RejectReason))
            info.Add(new Label
            {
                Text = "原因：" + rec!.RejectReason,
                FontSize = 11,
                TextColor = Color.FromArgb("#C04848")
            });

        grid.Add(info, 1, 0);

        var actions = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        switch (status)
        {
            case null:
                actions.Add(SmallButton("✅ 完成", Color.FromArgb("#7BCB7B"), White, () => OnComplete(task)));
                break;

            case ChoreStatus.Pending when ParentMode:
                actions.Add(SmallButton("👍 确认", Color.FromArgb("#7BCB7B"), White, () => OnConfirm(task)));
                actions.Add(SmallButton("👎 驳回", Color.FromArgb("#FF7676"), White, () => OnReject(task)));
                break;

            case ChoreStatus.Pending:
                actions.Add(SmallButton("⏳ 待确认", Color.FromArgb("#E0E4E8"), Color.FromArgb("#8A8F98"), null));
                break;

            case ChoreStatus.Confirmed:
                actions.Add(SmallButton("✅ 已完成", Color.FromArgb("#DCEFDC"), Color.FromArgb("#4A8A4A"), null));
                break;

            default:
                actions.Add(SmallButton("🔄 重做", Color.FromArgb("#FFB74D"), Ink, () => OnComplete(task)));
                break;
        }

        grid.Add(actions, 2, 0);

        var root = new VerticalStackLayout { Spacing = 6 };
        root.Add(grid);

        if (ParentMode)
        {
            var manage = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End };
            manage.Add(LinkButton("✏️ 编辑", () => OnEditTask(task)));
            if (task.Custom)
                manage.Add(LinkButton("🗑️ 删除", () => OnDeleteTask(task)));
            root.Add(manage);
        }

        card.Content = root;
        return card;
    }

    private async void OnComplete(ChoreTask task)
    {
        ChoreService.Complete(task.Id);
        RenderPoints();
        RenderTasks();
        try { ServiceProvider.Get<ISoundService>().PlayCorrect(); } catch { /* 无声设备忽略 */ }
        await DisplayAlert("打卡成功", ParentMode
            ? "已记录，点「确认」就会加到积分里。"
            : "已交给爸爸妈妈确认，等他们说「做得好」就有积分啦！", "好");
    }

    private async void OnConfirm(ChoreTask task)
    {
        var bonus = ChoreService.Confirm(task.Id);
        RenderPoints();
        RenderTasks();
        RenderReport();
        try { ServiceProvider.Get<ISoundService>().PlayCorrect(); } catch { /* 忽略 */ }

        var msg = $"「{task.Name}」+{task.Points} 分，已加到孩子的积分里。";
        if (bonus > 0)
            msg += $"\n\n🎉 还有额外奖励 +{bonus} 分（全部完成或连续打卡）。";

        await DisplayAlert("确认完成", msg, "好");
    }

    private async void OnReject(ChoreTask task)
    {
        var reason = await DisplayPromptAsync("驳回打卡", "写一句具体的原因，孩子才知道怎么改：",
            accept: "提交", cancel: "取消", placeholder: "例如：桌面还有水渍", maxLength: 30);

        if (reason is null) return;
        ChoreService.Reject(task.Id, reason);
        RenderTasks();
    }

    private async void OnDeleteTask(ChoreTask task)
    {
        var ok = await DisplayAlert("删除任务", $"确定删除「{task.Name}」吗？", "删除", "取消");
        if (!ok) return;
        ChoreService.DeleteTask(task.Id);
        RenderTasks();
        RenderReport();
    }

    private async void OnEditTask(ChoreTask task)
    {
        var edited = await EditTaskAsync(task);
        if (edited is null) return;
        ChoreService.SaveTask(edited);
        RenderTasks();
    }

    private async void OnAddTask(object sender, EventArgs e)
    {
        var created = await EditTaskAsync(null);
        if (created is null) return;
        ChoreService.SaveTask(created);
        RenderTasks();
    }

    private Task<ChoreTask?> EditTaskAsync(ChoreTask? existing)
    {
        var tcs = new TaskCompletionSource<ChoreTask?>();
        _ = Navigation.PushModalAsync(new ChoreEditPage(existing, tcs));
        return tcs.Task;
    }

    // ── 奖励商店 ────────────────────────────────────────────

    private void RenderRewards()
    {
        RenderRewardChips();

        var store = ChoreService.Store;
        var points = store.Points.Current;

        RewardHost.Children.Clear();

        var list = store.Rewards
            .Where(r => !_rewardAffordableOnly || r.Points <= points)
            .ToList();

        if (list.Count == 0)
        {
            RewardHost.Children.Add(EmptyHint("🎁", _rewardAffordableOnly ? "积分还不够兑换，继续做家务吧" : "还没有奖励"));
        }
        else
        {
            foreach (var reward in list)
                RewardHost.Children.Add(BuildRewardCard(reward, points));
        }

        RedemptionHost.Children.Clear();
        if (store.Redemptions.Count == 0)
        {
            RedemptionHost.Children.Add(EmptyHint("📭", "还没有兑换记录"));
        }
        else
        {
            foreach (var r in store.Redemptions.AsEnumerable().Reverse().Take(20))
            {
                RedemptionHost.Children.Add(RowCard(
                    r.RewardEmoji,
                    r.RewardName,
                    $"{r.Date} {r.Time}",
                    $"-{r.Points} 分",
                    Color.FromArgb("#C04848")));
            }
        }
    }

    private void RenderRewardChips()
    {
        if (RewardChips.Children.Count == 0)
        {
            RewardChips.Children.Add(Chip("全部奖励", !_rewardAffordableOnly, () =>
            {
                _rewardAffordableOnly = false;
                RenderRewards();
            }));
            RewardChips.Children.Add(Chip("✨ 我能兑换", _rewardAffordableOnly, () =>
            {
                _rewardAffordableOnly = true;
                RenderRewards();
            }));
            return;
        }

        if (RewardChips.Children[0] is Button all)
        {
            all.BackgroundColor = _rewardAffordableOnly ? White : Yellow;
            all.TextColor = _rewardAffordableOnly ? Color.FromArgb("#7A6A3A") : Ink;
        }
        if (RewardChips.Children[1] is Button aff)
        {
            aff.BackgroundColor = _rewardAffordableOnly ? Yellow : White;
            aff.TextColor = _rewardAffordableOnly ? Ink : Color.FromArgb("#7A6A3A");
        }
    }

    private View BuildRewardCard(ChoreReward reward, int points)
    {
        var canAfford = points >= reward.Points;

        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Add(new Label { Text = reward.Emoji, FontSize = 34, HorizontalOptions = LayoutOptions.Center });
        stack.Add(new Label
        {
            Text = reward.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4E3B12"),
            HorizontalTextAlignment = TextAlignment.Center
        });
        stack.Add(new Label
        {
            Text = $"⭐ {reward.Points} 分",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = White,
            BackgroundColor = Blue,
            Padding = new Thickness(8, 2),
            HorizontalOptions = LayoutOptions.Center
        });
        if (!string.IsNullOrWhiteSpace(reward.Description))
            stack.Add(new Label
            {
                Text = reward.Description,
                FontSize = 11,
                TextColor = Color.FromArgb("#9C8A5A"),
                HorizontalTextAlignment = TextAlignment.Center
            });

        stack.Add(SmallButton(canAfford ? "🎁 兑换" : "积分不足",
            canAfford ? Color.FromArgb("#FFA94D") : Color.FromArgb("#E0E4E8"),
            canAfford ? White : Color.FromArgb("#8A8F98"),
            canAfford ? () => OnRedeem(reward) : null));

        if (ParentMode)
        {
            var manage = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.Center };
            manage.Add(LinkButton("✏️", () => OnEditReward(reward)));
            if (reward.Custom)
                manage.Add(LinkButton("🗑️", () => OnDeleteReward(reward)));
            stack.Add(manage);
        }

        return new Frame
        {
            Padding = new Thickness(12, 14),
            CornerRadius = 18,
            HasShadow = false,
            BackgroundColor = White,
            BorderColor = Colors.Transparent,
            Content = stack
        };
    }

    private async void OnRedeem(ChoreReward reward)
    {
        var (ok, msg) = ChoreService.Redeem(reward.Id);
        RenderPoints();
        RenderRewards();

        if (ok)
        {
            try { ServiceProvider.Get<ISoundService>().PlayCorrect(); } catch { /* 忽略 */ }
            await DisplayAlert("🎁 兑换成功", msg + "\n\n拿给爸爸妈妈看，让他们帮你实现吧！", "好");
        }
        else
        {
            await DisplayAlert("还不能兑换", msg, "好");
        }
    }

    private async void OnDeleteReward(ChoreReward reward)
    {
        var ok = await DisplayAlert("删除奖励", $"确定删除「{reward.Name}」吗？", "删除", "取消");
        if (!ok) return;
        ChoreService.DeleteReward(reward.Id);
        RenderRewards();
    }

    private async void OnEditReward(ChoreReward reward)
    {
        var edited = await EditRewardAsync(reward);
        if (edited is null) return;
        ChoreService.SaveReward(edited);
        RenderRewards();
    }

    private async void OnAddReward(object sender, EventArgs e)
    {
        var created = await EditRewardAsync(null);
        if (created is null) return;
        ChoreService.SaveReward(created);
        RenderRewards();
    }

    private Task<ChoreReward?> EditRewardAsync(ChoreReward? existing)
    {
        var tcs = new TaskCompletionSource<ChoreReward?>();
        _ = Navigation.PushModalAsync(new ChoreRewardEditPage(existing, tcs));
        return tcs.Task;
    }

    // ── 周报 ────────────────────────────────────────────────

    private void RenderReport()
    {
        var stats = ChoreService.WeekStats(_weekOffset);

        WeekLabel.Text = _weekOffset switch
        {
            0 => "本周",
            -1 => "上周",
            1 => "下周",
            < 0 => $"前 {-_weekOffset} 周",
            _ => $"后 {_weekOffset} 周"
        };

        var totalPoints = stats.Sum(s => s.Points);
        WeekPoints.Text = totalPoints.ToString();
        WeekTasks.Text = stats.Sum(s => s.Count).ToString();
        WeekStreak.Text = ChoreService.StreakDays().ToString();

        RenderChart(stats);
        RenderTop3();

        CheckinHost.Children.Clear();
        var keys = ChoreService.WeekDates(_weekOffset).Select(d => d.ToString("yyyy-MM-dd")).ToHashSet();
        var recs = ChoreService.Store.Records
            .Where(r => keys.Contains(r.Date))
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Time)
            .Take(20)
            .ToList();

        if (recs.Count == 0)
        {
            CheckinHost.Children.Add(EmptyHint("📭", "这一周还没有打卡记录"));
            return;
        }

        foreach (var r in recs)
        {
            var icon = r.Status == ChoreStatus.Confirmed ? "✅" : r.Status == ChoreStatus.Pending ? "⏳" : "❌";
            var right = r.Status switch
            {
                ChoreStatus.Confirmed => $"+{r.Points} 分",
                ChoreStatus.Pending => "待确认",
                _ => "已驳回"
            };

            CheckinHost.Children.Add(RowCard(
                r.TaskEmoji,
                $"{icon} {r.TaskName}",
                $"{r.Date} {r.Time}",
                right,
                r.Status == ChoreStatus.Confirmed ? Color.FromArgb("#4A8A4A") : Color.FromArgb("#9C8A5A")));
        }
    }

    private void RenderChart(List<ChoreService.DayStat> stats)
    {
        ChartHost.Children.Clear();
        ChartHost.ColumnDefinitions.Clear();
        ChartHost.RowDefinitions.Clear();
        ChartHost.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        var max = Math.Max(stats.Max(s => s.Points), 10);

        for (var i = 0; i < stats.Count; i++)
        {
            ChartHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var s = stats[i];

            var col = new VerticalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Center
            };

            if (s.Points > 0)
                col.Add(new Label
                {
                    Text = s.Points.ToString(),
                    FontSize = 10.5,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#7A6A3A"),
                    HorizontalOptions = LayoutOptions.Center
                });

            var height = Math.Max(4, 118.0 * s.Points / max);
            col.Add(new BoxView
            {
                HeightRequest = height,
                WidthRequest = 20,
                CornerRadius = 8,
                Color = s.IsToday ? Blue : s.Points > 0 ? Yellow : Color.FromArgb("#E0E4E8"),
                VerticalOptions = LayoutOptions.End
            });

            col.Add(new Label
            {
                Text = "周" + s.WeekLabel,
                FontSize = 10.5,
                TextColor = s.IsToday ? Blue : Color.FromArgb("#9C8A5A"),
                HorizontalOptions = LayoutOptions.Center
            });

            Grid.SetColumn(col, i);
            ChartHost.Children.Add(col);
        }
    }

    private void RenderTop3()
    {
        Top3Host.Children.Clear();
        var top = ChoreService.TopTasks(_weekOffset);

        if (top.Count == 0)
        {
            Top3Host.Children.Add(EmptyHint("📊", "这一周还没有完成的记录"));
            return;
        }

        var rankColors = new[]
        {
            Color.FromArgb("#FFD700"),
            Color.FromArgb("#C0C0C0"),
            Color.FromArgb("#CD7F32")
        };

        for (var i = 0; i < top.Count; i++)
        {
            var item = top[i];
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            grid.Add(new Frame
            {
                WidthRequest = 26,
                HeightRequest = 26,
                CornerRadius = 13,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = rankColors[i],
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = (i + 1).ToString(),
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }, 0, 0);

            grid.Add(new Label { Text = item.Emoji, FontSize = 20, VerticalOptions = LayoutOptions.Center }, 1, 0);
            grid.Add(new Label
            {
                Text = item.Name,
                FontSize = 14.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#4E3B12"),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);
            grid.Add(new Label
            {
                Text = $"{item.Count} 次",
                FontSize = 12.5,
                TextColor = Color.FromArgb("#9C8A5A"),
                VerticalOptions = LayoutOptions.Center
            }, 3, 0);

            Top3Host.Children.Add(new Frame
            {
                Padding = new Thickness(12, 9),
                CornerRadius = 14,
                HasShadow = false,
                BackgroundColor = White,
                Content = grid
            });
        }
    }

    private void OnPrevWeek(object sender, EventArgs e)
    {
        _weekOffset--;
        RenderReport();
    }

    private void OnNextWeek(object sender, EventArgs e)
    {
        _weekOffset++;
        RenderReport();
    }

    // ── Tab ────────────────────────────────────────────────

    private void OnTabTasks(object sender, EventArgs e) { _tab = 0; ApplyTab(); }
    private void OnTabRewards(object sender, EventArgs e) { _tab = 1; ApplyTab(); }
    private void OnTabReport(object sender, EventArgs e) { _tab = 2; ApplyTab(); }

    private void ApplyTab()
    {
        TasksSection.IsVisible = _tab == 0;
        RewardsSection.IsVisible = _tab == 1;
        ReportSection.IsVisible = _tab == 2;

        StyleTab(TabTasks, _tab == 0);
        StyleTab(TabRewards, _tab == 1);
        StyleTab(TabReport, _tab == 2);
    }

    private static void StyleTab(Button btn, bool on)
    {
        btn.BackgroundColor = on ? Blue : White;
        btn.TextColor = on ? White : Color.FromArgb("#5B4500");
    }

    // ── 角色切换 ────────────────────────────────────────────

    private async void OnRoleToggle(object sender, EventArgs e)
    {
        var store = ChoreService.Store;

        if (ParentMode)
        {
            store.ParentMode = false;
            ChoreService.Save();
            RenderAll();
            return;
        }

        if (!_parentVerifiedThisSession)
        {
            var settings = ServiceProvider.Get<ISettingsService>();

            if (settings.HasPin)
            {
                var pin = await DisplayPromptAsync("家长验证", "请输入家长密码", "确定", "取消",
                    "4 位密码", maxLength: 8, keyboard: Keyboard.Numeric);

                if (pin is null) return;
                if (!settings.VerifyPin(pin))
                {
                    await DisplayAlert("密码不对", "再试一次吧。", "好");
                    return;
                }
            }

            _parentVerifiedThisSession = true;
        }

        store.ParentMode = true;
        ChoreService.Save();
        RenderAll();
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    // ── 小组件 ──────────────────────────────────────────────

    private void BuildChips()
    {
        if (TaskChips.Children.Count > 0) return;

        TaskChips.Children.Add(Chip("全部", true, () => { _taskFilter = null; RenderTasks(); }, "all"));
        TaskChips.Children.Add(Chip("🟢 简单", false, () => { _taskFilter = ChoreDifficulty.Easy; RenderTasks(); }, "easy"));
        TaskChips.Children.Add(Chip("🟡 中等", false, () => { _taskFilter = ChoreDifficulty.Medium; RenderTasks(); }, "medium"));
        TaskChips.Children.Add(Chip("🔴 困难", false, () => { _taskFilter = ChoreDifficulty.Hard; RenderTasks(); }, "hard"));
    }

    private Button Chip(string text, bool on, Action tap, string? tag = null)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(14, 0),
            CornerRadius = 18,
            BackgroundColor = on ? Yellow : White,
            TextColor = on ? Ink : Color.FromArgb("#7A6A3A"),
            CommandParameter = tag
        };
        btn.Clicked += (_, _) => tap();
        return btn;
    }

    private Button SmallButton(string text, Color bg, Color fg, Action? tap)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 12.5,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 34,
            Padding = new Thickness(10, 0),
            CornerRadius = 12,
            BackgroundColor = bg,
            TextColor = fg,
            IsEnabled = tap is not null
        };
        if (tap is not null) btn.Clicked += (_, _) => tap();
        return btn;
    }

    private Button LinkButton(string text, Action tap)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 12,
            HeightRequest = 26,
            Padding = new Thickness(4, 0),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#7A6A3A")
        };
        btn.Clicked += (_, _) => tap();
        return btn;
    }

    private static View EmptyHint(string emoji, string text)
        => new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(0, 18),
            Children =
            {
                new Label { Text = emoji, FontSize = 34, HorizontalOptions = LayoutOptions.Center },
                new Label
                {
                    Text = text,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#9C8A5A"),
                    HorizontalOptions = LayoutOptions.Center
                }
            }
        };

    private static View BuildStack(string title, string sub, double size)
        => new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#4E3B12") },
                new Label { Text = sub, FontSize = 11, TextColor = Color.FromArgb("#9C8A5A") }
            }
        };

    private static View RowCard(string emoji, string title, string sub, string right, Color rightColor)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Children =
            {
                new Label { Text = emoji, FontSize = 22, VerticalOptions = LayoutOptions.Center },
                BuildStack(title, sub, 14),
                new Label
                {
                    Text = right,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = rightColor,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

        Grid.SetColumn(grid.Children[1] as View ?? new Label(), 1);
        Grid.SetColumn(grid.Children[2] as View ?? new Label(), 2);

        return new Frame
        {
            Padding = new Thickness(12, 10),
            CornerRadius = 14,
            HasShadow = false,
            BackgroundColor = White,
            Content = grid
        };
    }
}

/// <summary>添加 / 编辑家务任务的弹窗（用代码搭，避免为一个小表单再开一堆 XAML）。</summary>
internal sealed class ChoreEditPage : ContentPage
{
    private readonly ChoreTask? _existing;
    private readonly TaskCompletionSource<ChoreTask?> _tcs;
    private bool _done;

    private readonly Entry _emoji = new() { Placeholder = "图标，如 🧹", MaxLength = 4, FontSize = 17 };
    private readonly Entry _name = new() { Placeholder = "任务名称，如 扫地", MaxLength = 12, FontSize = 17 };
    private readonly Entry _points = new() { Placeholder = "积分，如 5", Keyboard = Keyboard.Numeric, FontSize = 17 };
    private ChoreDifficulty _difficulty = ChoreDifficulty.Easy;
    private readonly Button _easy = new();
    private readonly Button _medium = new();
    private readonly Button _hard = new();

    public ChoreEditPage(ChoreTask? existing, TaskCompletionSource<ChoreTask?> tcs)
    {
        _existing = existing;
        _tcs = tcs;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#FFF9E0");

        if (existing is not null)
        {
            _emoji.Text = existing.Emoji;
            _name.Text = existing.Name;
            _points.Text = existing.Points.ToString();
            _difficulty = existing.Difficulty;
        }

        Content = Build();
        RefreshDifficulty();
    }

    private View Build()
    {
        var stack = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(20, 24) };

        stack.Add(new Label
        {
            Text = _existing is null ? "➕ 添加家务任务" : "✏️ 编辑任务",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B4500"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        stack.Add(Field("图标", _emoji));
        stack.Add(Field("名称", _name));
        stack.Add(Field("积分", _points));

        stack.Add(new Label { Text = "难度", FontSize = 13, TextColor = Color.FromArgb("#9C8A5A") });
        var diffRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
        SetupDiffButton(_easy, "🟢 简单", ChoreDifficulty.Easy);
        SetupDiffButton(_medium, "🟡 中等", ChoreDifficulty.Medium);
        SetupDiffButton(_hard, "🔴 困难", ChoreDifficulty.Hard);
        diffRow.Add(_easy, 0, 0);
        diffRow.Add(_medium, 1, 0);
        diffRow.Add(_hard, 2, 0);
        stack.Add(diffRow);

        var buttons = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Text = "取消", FontSize = 16, HeightRequest = 48, CornerRadius = 16, BackgroundColor = Color.FromArgb("#E0E4E8"), TextColor = Color.FromArgb("#5B4500") };
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        var save = new Button { Text = "保存", FontSize = 16, FontAttributes = FontAttributes.Bold, HeightRequest = 48, CornerRadius = 16, BackgroundColor = Color.FromArgb("#6EC1E4"), TextColor = Colors.White };
        save.Clicked += async (_, _) => await SaveAsync();
        buttons.Add(cancel, 0, 0);
        buttons.Add(save, 1, 0);
        stack.Add(buttons);

        return new ScrollView { Content = stack };
    }

    private static View Field(string title, Entry entry) => new VerticalStackLayout
    {
        Spacing = 4,
        Children =
        {
            new Label { Text = title, FontSize = 13, TextColor = Color.FromArgb("#9C8A5A") },
            new Frame
            {
                Padding = new Thickness(12, 4),
                CornerRadius = 12,
                HasShadow = false,
                BackgroundColor = Colors.White,
                Content = entry
            }
        }
    };

    private void SetupDiffButton(Button btn, string text, ChoreDifficulty diff)
    {
        btn.Text = text;
        btn.FontSize = 13;
        btn.HeightRequest = 42;
        btn.CornerRadius = 12;
        btn.Clicked += (_, _) =>
        {
            _difficulty = diff;
            RefreshDifficulty();
        };
    }

    private void RefreshDifficulty()
    {
        foreach (var (btn, diff) in new[] { (_easy, ChoreDifficulty.Easy), (_medium, ChoreDifficulty.Medium), (_hard, ChoreDifficulty.Hard) })
        {
            var on = _difficulty == diff;
            btn.BackgroundColor = on ? Color.FromArgb("#FFD54F") : Colors.White;
            btn.TextColor = on ? Color.FromArgb("#5B4500") : Color.FromArgb("#9C8A5A");
        }
    }

    private async Task SaveAsync()
    {
        var name = _name.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("还差一点", "给任务起个名字吧。", "好");
            return;
        }

        var points = int.TryParse(_points.Text, out var p) && p > 0
            ? p
            : _difficulty switch
            {
                ChoreDifficulty.Easy => 5,
                ChoreDifficulty.Medium => 10,
                _ => 15
            };

        var task = new ChoreTask
        {
            Id = _existing?.Id ?? string.Empty,
            Emoji = string.IsNullOrWhiteSpace(_emoji.Text) ? "🧹" : _emoji.Text!.Trim(),
            Name = name,
            Points = Math.Clamp(points, 1, 100),
            Difficulty = _difficulty,
            Custom = _existing?.Custom ?? true
        };

        await CloseAsync(task);
    }

    private async Task CloseAsync(ChoreTask? result)
    {
        if (_done) return;
        _done = true;
        _tcs.TrySetResult(result);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }
}

/// <summary>添加 / 编辑奖励的弹窗。</summary>
internal sealed class ChoreRewardEditPage : ContentPage
{
    private readonly ChoreReward? _existing;
    private readonly TaskCompletionSource<ChoreReward?> _tcs;
    private bool _done;

    private readonly Entry _emoji = new() { Placeholder = "图标，如 🍦", MaxLength = 4, FontSize = 17 };
    private readonly Entry _name = new() { Placeholder = "奖励名称，如 吃冰淇淋", MaxLength = 16, FontSize = 17 };
    private readonly Entry _points = new() { Placeholder = "所需积分，如 30", Keyboard = Keyboard.Numeric, FontSize = 17 };
    private readonly Entry _desc = new() { Placeholder = "描述（可不填）", MaxLength = 24, FontSize = 15 };

    public ChoreRewardEditPage(ChoreReward? existing, TaskCompletionSource<ChoreReward?> tcs)
    {
        _existing = existing;
        _tcs = tcs;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#FFF9E0");

        if (existing is not null)
        {
            _emoji.Text = existing.Emoji;
            _name.Text = existing.Name;
            _points.Text = existing.Points.ToString();
            _desc.Text = existing.Description;
        }

        Content = Build();
    }

    private View Build()
    {
        var stack = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(20, 24) };

        stack.Add(new Label
        {
            Text = _existing is null ? "➕ 添加奖励" : "✏️ 编辑奖励",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B4500"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        stack.Add(Field("图标", _emoji));
        stack.Add(Field("名称", _name));
        stack.Add(Field("所需积分", _points));
        stack.Add(Field("描述", _desc));

        var buttons = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Text = "取消", FontSize = 16, HeightRequest = 48, CornerRadius = 16, BackgroundColor = Color.FromArgb("#E0E4E8"), TextColor = Color.FromArgb("#5B4500") };
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        var save = new Button { Text = "保存", FontSize = 16, FontAttributes = FontAttributes.Bold, HeightRequest = 48, CornerRadius = 16, BackgroundColor = Color.FromArgb("#6EC1E4"), TextColor = Colors.White };
        save.Clicked += async (_, _) => await SaveAsync();
        buttons.Add(cancel, 0, 0);
        buttons.Add(save, 1, 0);
        stack.Add(buttons);

        return new ScrollView { Content = stack };
    }

    private static View Field(string title, Entry entry) => new VerticalStackLayout
    {
        Spacing = 4,
        Children =
        {
            new Label { Text = title, FontSize = 13, TextColor = Color.FromArgb("#9C8A5A") },
            new Frame
            {
                Padding = new Thickness(12, 4),
                CornerRadius = 12,
                HasShadow = false,
                BackgroundColor = Colors.White,
                Content = entry
            }
        }
    };

    private async Task SaveAsync()
    {
        var name = _name.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("还差一点", "给奖励起个名字吧。", "好");
            return;
        }

        if (!int.TryParse(_points.Text, out var points) || points <= 0)
        {
            await DisplayAlert("积分不对", "请填一个大于 0 的积分数。", "好");
            return;
        }

        var reward = new ChoreReward
        {
            Id = _existing?.Id ?? string.Empty,
            Emoji = string.IsNullOrWhiteSpace(_emoji.Text) ? "🎁" : _emoji.Text!.Trim(),
            Name = name,
            Points = Math.Clamp(points, 1, 9999),
            Description = _desc.Text?.Trim() ?? string.Empty,
            Custom = _existing?.Custom ?? true
        };

        await CloseAsync(reward);
    }

    private async Task CloseAsync(ChoreReward? result)
    {
        if (_done) return;
        _done = true;
        _tcs.TrySetResult(result);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }
}
