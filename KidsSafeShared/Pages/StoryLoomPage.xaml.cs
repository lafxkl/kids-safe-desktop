using System.Text;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls.Shapes;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 🌙 无限想象：和五个小伙伴一起编今晚的故事。
///
/// 移植自 Dreamloom（华为云码道参赛作品），保留了三件事：
/// 1. 「说一句小心愿 → 五个角色依次加工 → 四段故事」的共创流程；
/// 2. 六维语言潜能的成长轨迹（只在本机，不排名）；
/// 3. 灵感词条 + 原话识别，让孩子的参与感落在具体元素上。
///
/// 改了的地方：
/// - 原版靠浏览器 TTS / 本地 Node 代理，这里用系统 TTS，逐段朗读并高亮当前段；
/// - 原版数据在 localStorage，这里进 Preferences，与星星中心共用「只存本机」的约定；
/// - 大模型入口收进家长 PIN 后面且默认关闭（儿童数据不外发的默认值）。
/// </summary>
public partial class StoryLoomPage : ContentPage
{
    private const int MaxPicked = 6;
    private const int DailyStarCap = 3;      // 每天最多为 3 个故事记星，防刷
    private const int StarsPerStory = 2;

    private readonly List<LoomChip> _picked = new();
    private readonly List<Label> _paraLabels = new();
    private readonly Dictionary<string, (Label Name, Label Msg, Frame Row)> _rows = new();
    private readonly LoomRadarDrawable _radar = new();

    private CancellationTokenSource? _ttsCts;
    private LoomStory? _story;
    private bool _busy;
    private bool _speaking;
    private bool _revealing;

    public StoryLoomPage()
    {
        InitializeComponent();

        // 顶部自带「← 返回 / 标题 / 星星」一条，若再让系统导航栏出现，
        // 会在渐变底色上方多出一条白条（标题还会重复一次），所以统一关掉。
        NavigationPage.SetHasNavigationBar(this, false);

        StoryLoomStore.Load();
        RadarView.Drawable = _radar;

        BuildChips();
        RenderPicked();
        RenderLibrary();
        RenderGrowth();
        UpdateStars();
        SetGreeting();
        UpdateAiStatus();

        ReadBtn.IsEnabled = false;
        SaveBtn.IsEnabled = false;

        // 没有系统识别器的设备（多数 Windows 机器、个别定制安卓）直接不显示麦克风，
        // 而不是点了以后弹「不支持」——看不到按钮比点了没反应更不让人困惑。
        VoiceBtn.IsVisible = VoiceInput.IsSupported;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateStars();
        UpdateAiStatus();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTts();
    }

    // ══════════ 顶部 ══════════

    private async void OnBack(object sender, EventArgs e)
    {
        StopTts();
        await Navigation.PopAsync();
    }

    private void SetGreeting()
    {
        var h = DateTime.Now.Hour;
        var g = h is >= 5 and < 11 ? "早上好" : h is >= 11 and < 18 ? "下午好" : h >= 18 ? "晚上好" : "夜深了";
        GreetLabel.Text = $"{g}，小星 🌟";
    }

    private void UpdateStars() => StarLabel.Text = $"⭐ {StarCenter.Total}";

    private void UpdateAiStatus()
    {
        var enabled = StoryLoomAi.Enabled;
        AiStatusLabel.Text = enabled
            ? $"已开启 · {StoryLoomAi.Model}"
            : "未开启 · 用内置模板写故事";
        AiStatusLabel.TextColor = enabled ? Color.FromArgb("#34C759") : Color.FromArgb("#7C86B8");
    }

    // ══════════ Tab ══════════

    private void OnTabCreative(object sender, EventArgs e) => SwitchTab(0);
    private void OnTabLibrary(object sender, EventArgs e) => SwitchTab(1);
    private void OnTabGrowth(object sender, EventArgs e) => SwitchTab(2);

    private void SwitchTab(int index)
    {
        CreativeScroll.IsVisible = index == 0;
        LibraryScroll.IsVisible = index == 1;
        GrowthScroll.IsVisible = index == 2;

        StyleTab(TabCreative, index == 0);
        StyleTab(TabLibrary, index == 1);
        StyleTab(TabGrowth, index == 2);

        if (index == 1) RenderLibrary();
        if (index == 2) RenderGrowth();
        if (index != 0) StopTts();
    }

    private static void StyleTab(Button b, bool on)
    {
        b.BackgroundColor = on ? Color.FromArgb("#2A3568") : Colors.Transparent;
        b.TextColor = on ? Color.FromArgb("#FFE9A8") : Color.FromArgb("#AAB2D8");
    }

    // ══════════ 灵感词条 ══════════

    private void BuildChips()
    {
        if (ChipBar.Children.Count > 0) return;

        foreach (var chip in StoryLoomCatalog.Chips)
        {
            var b = new Button
            {
                Text = $"{chip.Emoji} {chip.Text}",
                FontSize = 12.5,
                HeightRequest = 34,
                Padding = new Thickness(12, 0),
                CornerRadius = 17,
                BackgroundColor = Color.FromArgb("#1C244E"),
                TextColor = Color.FromArgb("#C9D2F5"),
                BorderColor = Color.FromArgb("#2A3568"),
                BorderWidth = 1,
                BindingContext = chip
            };
            b.Clicked += OnChipClicked;
            ChipBar.Children.Add(b);
        }
    }

    private void OnChipClicked(object? sender, EventArgs e)
    {
        if (sender is not Button b || b.BindingContext is not LoomChip chip) return;

        var exist = _picked.FirstOrDefault(p => p.Text == chip.Text);
        if (exist is not null)
        {
            _picked.Remove(exist);
            b.BackgroundColor = Color.FromArgb("#1C244E");
            b.TextColor = Color.FromArgb("#C9D2F5");
            RenderPicked();
            return;
        }

        if (_picked.Count >= MaxPicked)
        {
            Buzz("灵感太多啦，先放下一个 🎈");
            return;
        }

        _picked.Add(chip);
        b.BackgroundColor = Color.FromArgb("#3A3070");
        b.TextColor = Color.FromArgb("#FFE9A8");
        RenderPicked();
    }

    private void RenderPicked()
    {
        PickedBar.Children.Clear();
        foreach (var p in _picked.ToList())
        {
            var tag = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Stroke = Color.FromArgb("#4A3C86"),
                BackgroundColor = Color.FromArgb("#3A3070"),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 8, 8),
                Content = new Label
                {
                    Text = $"{p.Emoji} {p.Text} ✕",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#FFE9A8")
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _picked.Remove(p);
                RenderPicked();
                SyncChipVisual(p.Text);
            };
            tag.GestureRecognizers.Add(tap);
            PickedBar.Children.Add(tag);
        }
        PickedBar.IsVisible = _picked.Count > 0;
    }

    private void SyncChipVisual(string text)
    {
        foreach (var child in ChipBar.Children)
            if (child is Button b && b.BindingContext is LoomChip c && c.Text == text)
            {
                b.BackgroundColor = Color.FromArgb("#1C244E");
                b.TextColor = Color.FromArgb("#C9D2F5");
            }
    }

    // ══════════ 生成 ══════════

    private async void OnWishCompleted(object sender, EventArgs e) => await RunAsync();

    private async void OnGenerate(object sender, EventArgs e) => await RunAsync();

    /// <summary>
    /// 语音输入。第一次使用要让家长过 PIN —— 麦克风这件事必须家长知情同意，
    /// 与他/她对「大模型生成」的开关是同一套规矩。
    /// </summary>
    private async void OnVoice(object sender, EventArgs e)
    {
        if (_busy) return;

        if (!VoiceInput.IsSupported)
        {
            await DisplayAlert("这台设备没有语音识别", "直接打字也一样可以编故事。", "好");
            return;
        }

        if (!VoiceInput.Enabled)
        {
            var agree = await DisplayAlert(
                "先问问家长",
                "语音输入会把你说的话交给手机自带的语音识别来听写。\n\n" +
                "它不会上传到我们的服务器，但麦克风这件事需要家长点头。",
                "叫家长来", "先算了");

            if (!agree) return;

            var settings = ServiceProvider.Get<ISettingsService>();
            if (settings.HasPin)
            {
                var pin = await DisplayPromptAsync("家长验证", "请输入家长密码", "确定", "取消",
                    "家长密码", maxLength: 8, keyboard: Keyboard.Numeric);

                if (pin is null) return;
                if (!settings.VerifyPin(pin))
                {
                    await DisplayAlert("密码不对", "再试一次吧。", "好");
                    return;
                }
            }

            VoiceInput.Enabled = true;
        }

        VoiceBtn.IsEnabled = false;
        VoiceBtn.Text = "…";

        try
        {
            var said = await VoiceInput.ListenAsync("说出你想听的故事");

            if (string.IsNullOrWhiteSpace(said))
            {
                Buzz("没听清，再说一次吧 🎤");
                return;
            }

            WishEntry.Text = said;
            await RunAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 语音输入失败：{ex}");
            Buzz("语音没成功，可以直接打字");
        }
        finally
        {
            VoiceBtn.IsEnabled = true;
            VoiceBtn.Text = "🎤";
        }
    }

    private async Task RunAsync()
    {
        if (_busy) return;

        var text = WishEntry.Text?.Trim() ?? "";
        if (text.Length == 0 && _picked.Count == 0)
        {
            Buzz("说点什么，或点个小元素吧 🌙");
            return;
        }

        _busy = true;
        GenerateBtn.IsEnabled = false;
        ReadBtn.IsEnabled = false;
        SaveBtn.IsEnabled = false;
        StoryCard.IsVisible = false;
        StopTts();

        try
        {
            // 1. 认出元素：先看孩子原话，再用点选的词补齐
            var idea = text.Length > 0 ? StoryLoomEngine.ExtractKeys(text) : new LoomIdea();
            foreach (var (key, _) in StoryLoomCatalog.Lex)
            {
                if (idea.Has(key)) continue;
                var pick = _picked.FirstOrDefault(p => p.Group == key);
                if (pick is not null) idea.Set(key, NormalizeChipWord(key, pick.Text));
            }

            StoryLoomEngine.Rectify(idea);
            idea.Wish = text.Length > 0 ? text : null;
            idea.Topic = StoryLoomEngine.TopicOf(text, idea);

            StoryLoomStore.PushRecord("child",
                text.Length > 0 ? $"（{text}）" : "我想用这些元素编故事。");

            var useAi = await StoryLoomAi.ReadyAsync();
            var aiTask = useAi
                ? StoryLoomAi.GenerateAsync(idea, idea.Wish)
                : Task.FromResult<(LoomStory? Story, string? Error)>((null, null));

            BuildPipeline();

            // 2. 五个小伙伴依次加工
            await StepAsync("spark", 620, () =>
            {
                var title = idea.Topic is null
                    ? $"{idea.Role}和{idea.Creature}的{StoryLoomEngine.MoodOf(idea)}"
                    : $"{idea.Role}与『{idea.Topic}』的奇妙夜";
                var msg = $"{(idea.Topic is null ? "" : $"抓到了你的心愿「{idea.Topic}」！")}抓到了：" +
                          string.Join(" · ", new[] { idea.Role, idea.Scene, idea.Item, idea.Creature }
                              .Where(s => !string.IsNullOrEmpty(s)));
                StoryLoomStore.PushRecord("spark", $"{msg}。主题：《{title}》");
                return msg;
            });

            await StepAsync("scene", 620, () =>
            {
                var msg = $"场景：{idea.Scene}，{idea.Weather}" +
                          (string.IsNullOrEmpty(idea.Sound) ? "" : $"，{idea.Sound}") + "。围绕同一个心愿展开故事。";
                StoryLoomStore.PushRecord("scene", msg);
                return msg;
            });

            // 3. 创作向导：真正写故事的地方（大模型优先，失败回退模板）
            SetRowBusy("guide", useAi ? "🧭 创作向导 正在编排情节逻辑…" : "🧭 创作向导 正在…");
            LoomStory? story = null;
            string? aiError = null;
            if (useAi)
            {
                var (aiStory, aiErr) = await aiTask;
                story = aiStory;
                aiError = aiErr;
            }
            story ??= StoryLoomEngine.BuildTemplate(idea);
            await Task.Delay(useAi ? 240 : 700);

            var guideMsg = story.Outline.Count > 0
                ? $"逻辑线：{string.Join(" → ", story.Outline.Take(4))}"
                : "先遇见小动静，再勇敢帮忙，最后安心入睡——结构排好了。";
            StoryLoomStore.PushRecord("guide", guideMsg);
            SetRowDone("guide", guideMsg);

            await StepAsync("polish", 560, () =>
            {
                var msg = $"润色完成，语气更温柔：以「{story.Mood}」收束，落到暖暖的睡意上。";
                StoryLoomStore.PushRecord("polish", msg);
                return msg;
            });

            // 4. 天赋瞭望归档
            SetRowBusy("talent", "🔭 天赋瞭望 正在归档…");
            await Task.Delay(600);
            StoryLoomStore.Bump(story);
            var talentMsg = $"记录：采纳了「{string.Join("、", new[] { idea.Role, idea.Creature, idea.Emotion }.Where(s => !string.IsNullOrEmpty(s)))}」等灵感，写入成长星图。";
            StoryLoomStore.PushRecord("talent", talentMsg);
            SetRowDone("talent", "潜能已更新：想象力 +1 · 词汇 +1 · 表达 +1");

            await Task.Delay(260);

            _story = story;
            RenderStory(story, story.IsAi);
            AwardStars(story);

            if (aiError is not null && useAi)
                Buzz("大模型没接通，已用内置模板写完这个故事");

            RenderGrowth();
            UpdateStars();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 生成失败：{ex}");
            Buzz("故事创作遇到问题，请再试一次");
        }
        finally
        {
            _busy = false;
            GenerateBtn.IsEnabled = true;
        }
    }

    /// <summary>词条文本对齐词库标准词（点的是「蜂蜜」，词库是「一罐蜂蜜」）。</summary>
    private static string NormalizeChipWord(string group, string chipText)
    {
        var items = StoryLoomCatalog.Lex.FirstOrDefault(x => x.Key == group).Items;
        if (items is null) return chipText;

        foreach (var (word, _) in items)
            if (word.Contains(chipText, StringComparison.Ordinal) || chipText.Contains(word, StringComparison.Ordinal))
                return word;

        return chipText;
    }

    // ══════════ 流水线 ══════════

    private void BuildPipeline()
    {
        PipelineBox.Children.Clear();
        _rows.Clear();
        PipelineBox.IsVisible = true;

        foreach (var agent in StoryLoomCatalog.Agents)
            PipelineBox.Children.Add(BuildRow(agent));
    }

    private View BuildRow(LoomAgent agent)
    {
        var dot = new Label
        {
            Text = agent.Emoji,
            FontSize = 18,
            WidthRequest = 34,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var name = new Label
        {
            Text = agent.Name,
            FontSize = 12.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(agent.Accent)
        };

        var msg = new Label
        {
            Text = "",
            FontSize = 11,
            TextColor = Color.FromArgb("#8C95C4"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(name);
        info.Children.Add(msg);

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8,
            Padding = new Thickness(12, 9)
        };
        grid.Add(dot, 0, 0);
        grid.Add(info, 1, 0);

        var frame = new Frame
        {
            Padding = 0,
            CornerRadius = 14,
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#161D44"),
            BorderColor = Color.FromArgb("#232C58"),
            Content = grid
        };

        _rows[agent.Key] = (name, msg, frame);
        return frame;
    }

    private void SetRowBusy(string key, string label)
    {
        if (!_rows.TryGetValue(key, out var row)) return;
        row.Name.Text = label;
        row.Msg.Text = "正在…";
        row.Msg.TextColor = Color.FromArgb("#8C95C4");
        row.Row.BorderColor = Color.FromArgb("#3A4680");
    }

    private void SetRowDone(string key, string text)
    {
        if (!_rows.TryGetValue(key, out var row)) return;
        var agent = StoryLoomCatalog.Agent(key);
        row.Name.Text = $"{agent.Emoji} {agent.Name} ✓";
        row.Msg.Text = text;
        row.Msg.TextColor = Color.FromArgb("#AAB2D8");
        row.Row.BorderColor = Color.FromArgb("#2A3568");
    }

    private async Task StepAsync(string key, int delayMs, Func<string> work)
    {
        var agent = StoryLoomCatalog.Agent(key);
        SetRowBusy(key, $"{agent.Emoji} {agent.Name} 正在…");
        await Task.Delay(delayMs);
        var msg = work();
        SetRowDone(key, msg);
    }

    // ══════════ 故事卡 ══════════

    private void RenderStory(LoomStory story, bool isAi)
    {
        StoryTitle.Text = $"🌛 {story.Title}";
        StoryBadge.Text = isAi ? "AI 编写" : "内置模板";
        StoryBadge.TextColor = isAi ? Color.FromArgb("#B9AEFF") : Color.FromArgb("#C9D2F5");
        StoryMood.Text = story.Mood;

        ParaBox.Children.Clear();
        _paraLabels.Clear();

        foreach (var p in story.Paragraphs)
        {
            var label = new Label
            {
                Text = p,
                FontSize = 14.5,
                LineHeight = 1.55,
                TextColor = Color.FromArgb("#DCE3FF"),
                Opacity = 0
            };
            _paraLabels.Add(label);
            ParaBox.Children.Add(label);
        }

        StoryCard.IsVisible = true;
        ReadBtn.IsEnabled = true;
        SaveBtn.IsEnabled = true;
        ReadBtn.Text = "🔊 读一读";

        _ = RevealAsync();
    }

    /// <summary>
    /// 逐段浮现。原版是逐字打字机，这里改成逐段淡入：
    /// 一段 60–80 字，逐字刷在低端安卓机上会掉帧，而「一段一段长出来」的节奏感是一样的。
    /// </summary>
    private async Task RevealAsync()
    {
        _revealing = true;
        foreach (var label in _paraLabels.ToList())
        {
            if (!_revealing) return;
            await label.FadeTo(1, 420, Easing.CubicOut);
            await Task.Delay(180);
        }
        _revealing = false;
    }

    private void OnShowFull(object sender, EventArgs e)
    {
        _revealing = false;
        foreach (var label in _paraLabels)
        {
            label.Opacity = 1;
            label.CancelAnimations();
        }
    }

    // ══════════ 朗读 ══════════

    private async void OnReadStory(object sender, EventArgs e)
    {
        if (_speaking)
        {
            StopTts();
            return;
        }

        if (_story is null) return;

        _speaking = true;
        ReadBtn.Text = "⏹ 停下";
        _ttsCts = new CancellationTokenSource();
        var ct = _ttsCts.Token;

        try
        {
            var pitch = VoiceStyles.Current.Pitch;
            for (var i = 0; i < _story.Paragraphs.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                Highlight(i);
                await Tts.SpeakAsync(_story.Paragraphs[i], "zh", ct, pitch);
            }

            if (!ct.IsCancellationRequested)
                await Tts.SpeakAsync("晚安，明天见。", "zh", ct, pitch);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 朗读失败：{ex.Message}");
        }
        finally
        {
            Highlight(-1);
            StopTts();
        }
    }

    private void Highlight(int index)
    {
        for (var i = 0; i < _paraLabels.Count; i++)
        {
            var on = i == index;
            _paraLabels[i].TextColor = on ? Color.FromArgb("#FFE9A8") : Color.FromArgb("#DCE3FF");
            _paraLabels[i].FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void StopTts()
    {
        try { _ttsCts?.Cancel(); } catch { /* 忽略 */ }
        _ttsCts?.Dispose();
        _ttsCts = null;
        _speaking = false;
        if (ReadBtn is not null) ReadBtn.Text = "🔊 读一读";
        Highlight(-1);
    }

    // ══════════ 收藏 ══════════

    private void OnSaveStory(object sender, EventArgs e)
    {
        if (_story is null) return;

        if (StoryLoomStore.AddStory(_story))
        {
            Buzz("已收藏到故事库 ⭐");
            RenderLibrary();
            RenderGrowth();
        }
        else
        {
            Buzz("这个故事已经收藏过了 ⭐");
        }
    }

    // ══════════ 星星 ══════════

    private void AwardStars(LoomStory story)
    {
        var key = $"loom_{DateTime.Today:yyyyMMdd}";
        if (StarCenter.CountOf(key) >= DailyStarCap) return;

        try
        {
            StarCenter.Award("story", key, story.Title, "🌙", StarsPerStory, "共创故事");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 记星失败：{ex.Message}");
        }
    }

    // ══════════ 故事库 ══════════

    private void RenderLibrary()
    {
        LibraryBox.Children.Clear();

        var items = StoryLoomStore.Library;
        LibraryHint.IsVisible = true;
        LibraryHint.Text = items.Count == 0
            ? "生成一个故事，点「⭐ 收藏」就会飞到这里"
            : $"共 {items.Count} 个故事 · 点开可以再听一遍，也能删除";

        foreach (var s in items)
        {
            var emoji = new Label
            {
                Text = s.Emoji,
                FontSize = 22,
                WidthRequest = 34,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var title = new Label
            {
                Text = s.Title,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFE9A8"),
                LineBreakMode = LineBreakMode.TailTruncation
            };

            var preview = new Label
            {
                Text = s.Preview,
                FontSize = 11.5,
                TextColor = Color.FromArgb("#8C95C4"),
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            var meta = new Label
            {
                Text = $"{s.TimeText} · {s.Mood}",
                FontSize = 10.5,
                TextColor = Color.FromArgb("#6E79A8")
            };

            var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
            info.Children.Add(title);
            info.Children.Add(preview);
            info.Children.Add(meta);

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 10,
                Padding = new Thickness(12, 10)
            };
            grid.Add(emoji, 0, 0);
            grid.Add(info, 1, 0);

            var frame = new Frame
            {
                Padding = 0,
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#1C244E"),
                BorderColor = Color.FromArgb("#2A3568"),
                Content = grid
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OpenStoryAsync(s);
            frame.GestureRecognizers.Add(tap);

            LibraryBox.Children.Add(frame);
        }
    }

    private async Task OpenStoryAsync(LoomSavedStory saved)
    {
        StopTts();
        await Navigation.PushAsync(new StoryLoomViewPage(saved));
        RenderLibrary();
    }

    // ══════════ 成长 ══════════

    private void RenderGrowth()
    {
        StatStory.Text = StoryLoomStore.StoryCount.ToString();
        StatSession.Text = StoryLoomStore.Sessions.ToString();
        StatRecord.Text = StoryLoomStore.RecordCount.ToString();
        StatKeyword.Text = StoryLoomStore.KeywordCount.ToString();

        _radar.Values = StoryLoomCatalog.Dimensions
            .ToDictionary(d => d.Key, d => StoryLoomStore.ValueOf(d.Key));
        RadarView.Invalidate();

        RenderDims();
        RenderTalents();
        RenderRecords();
    }

    private void RenderDims()
    {
        DimsBox.Children.Clear();

        foreach (var d in StoryLoomCatalog.Dimensions)
        {
            var value = StoryLoomStore.ValueOf(d.Key);
            var delta = StoryLoomStore.DeltaOf(d.Key);

            var name = new Label
            {
                Text = delta > 0 ? $"{d.Emoji} {d.Name}  ▲+{delta}" : $"{d.Emoji} {d.Name}",
                FontSize = 12.5,
                TextColor = Color.FromArgb("#C9D2F5")
            };

            var val = new Label
            {
                Text = $"{value}/10",
                FontSize = 11.5,
                TextColor = Color.FromArgb("#8C95C4"),
                HorizontalOptions = LayoutOptions.End
            };

            var head = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            head.Add(name, 0, 0);
            head.Add(val, 1, 0);

            // 进度条：用列宽比例当填充，避免在 Measure 之前拿不到实际宽度
            var bar = new Grid
            {
                HeightRequest = 7,
                BackgroundColor = Color.FromArgb("#232C58"),
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(Math.Max(value, 0.6), GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(Math.Max(10 - value, 0.6), GridUnitType.Star))
                }
            };
            bar.Add(new BoxView { Color = Color.FromArgb(d.Accent), CornerRadius = 4 }, 0, 0);
            bar.Add(new BoxView { Color = Colors.Transparent }, 1, 0);

            var box = new VerticalStackLayout { Spacing = 4 };
            box.Children.Add(head);
            box.Children.Add(bar);
            DimsBox.Children.Add(box);
        }
    }

    private void RenderTalents()
    {
        TalentsBox.Children.Clear();

        var strongest = StoryLoomStore.StrongestDimension();
        var head = new Label
        {
            Text = strongest is null
                ? "💫 天赋卡 · 随着共创慢慢显现"
                : $"💫 天赋卡 · 最近「{strongest.Name}」长得最快",
            FontSize = 12.5,
            TextColor = Color.FromArgb("#8C95C4")
        };
        TalentsBox.Children.Add(head);

        foreach (var t in StoryLoomCatalog.Talents)
        {
            var icon = new Label { Text = t.Icon, FontSize = 20, VerticalOptions = LayoutOptions.Center };
            var name = new Label
            {
                Text = t.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(t.Accent)
            };
            var desc = new Label
            {
                Text = t.Desc,
                FontSize = 11.5,
                TextColor = Color.FromArgb("#AAB2D8"),
                LineBreakMode = LineBreakMode.WordWrap
            };

            var badges = new HorizontalStackLayout { Spacing = 6 };
            foreach (var b in t.Badges)
                badges.Children.Add(new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 9 },
                    Stroke = Color.FromArgb("#2A3568"),
                    BackgroundColor = Color.FromArgb("#232C58"),
                    Padding = new Thickness(8, 3),
                    Content = new Label { Text = b, FontSize = 10, TextColor = Color.FromArgb("#C9D2F5") }
                });

            var right = new VerticalStackLayout { Spacing = 4 };
            right.Children.Add(name);
            right.Children.Add(desc);
            right.Children.Add(badges);

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 10,
                Padding = new Thickness(12, 10)
            };
            grid.Add(icon, 0, 0);
            grid.Add(right, 1, 0);

            TalentsBox.Children.Add(new Frame
            {
                Padding = 0,
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#1C244E"),
                BorderColor = Color.FromArgb("#2A3568"),
                Content = grid
            });
        }
    }

    private void RenderRecords()
    {
        RecordBox.Children.Clear();

        var records = StoryLoomStore.RecentRecords();
        if (records.Count == 0)
        {
            RecordBox.Children.Add(new Label
            {
                Text = "还没有记录。讲一个故事，这里就会热闹起来。",
                FontSize = 11.5,
                TextColor = Color.FromArgb("#6E79A8")
            });
            return;
        }

        foreach (var r in records)
        {
            var time = new Label
            {
                Text = r.TimeText,
                FontSize = 10,
                TextColor = Color.FromArgb("#6E79A8"),
                WidthRequest = 36,
                VerticalOptions = LayoutOptions.Start
            };

            var body = new Label
            {
                Text = $"{r.Who}：{r.Text}",
                FontSize = 11.5,
                TextColor = Color.FromArgb("#AAB2D8"),
                LineBreakMode = LineBreakMode.WordWrap
            };

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 8
            };
            grid.Add(time, 0, 0);
            grid.Add(body, 1, 0);
            RecordBox.Children.Add(grid);
        }
    }

    // ══════════ 家长区 ══════════

    private async void OnAiSettings(object sender, EventArgs e)
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

        await Navigation.PushModalAsync(new StoryLoomAiPage());
    }

    private async void OnClearGrowth(object sender, EventArgs e)
    {
        var ok = await DisplayAlert("清空成长数据",
            "六个维度的数值与世界记录会回到初始状态，故事库里的收藏不受影响。",
            "清空", "再想想");
        if (!ok) return;

        StoryLoomStore.ClearGrowth();
        RenderGrowth();
        Buzz("成长数据已清空");
    }

    // ══════════ 小工具 ══════════

    private void Buzz(string text)
    {
        try { ServiceProvider.Get<ISoundService>().PlayClick(); } catch { /* 无声设备 */ }
        _ = ShowToastAsync(text);
    }

    /// <summary>页面级轻提示：不打断孩子，也不像 DisplayAlert 那样必须点「好」。</summary>
    private async Task ShowToastAsync(string text)
    {
        try
        {
            var toast = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#EE1C244E"),
                Padding = new Thickness(16, 10),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(20, 0, 20, 90),
                Opacity = 0,
                Content = new Label { Text = text, FontSize = 13, TextColor = Colors.White }
            };

            if (Content is not Grid root) return;
            root.Add(toast);
            Grid.SetRow(toast, 0);
            Grid.SetRowSpan(toast, Math.Max(1, root.RowDefinitions.Count));
            await toast.FadeTo(1, 160);
            await Task.Delay(1700);
            await toast.FadeTo(0, 220);
            root.Remove(toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 提示失败：{ex.Message}");
        }
    }
}
