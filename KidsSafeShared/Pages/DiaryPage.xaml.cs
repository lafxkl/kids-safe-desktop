using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 我的日记。
///
/// 和「关爱中心 → 心情日记」不是一回事：那边是每天一条心情 + 一句话，不设防，家长面板能看到天数；
/// 这里是**孩子自己设密码的自由书写**，可以长、可以乱、可以不给人看。
///
/// 三个设计决定：
/// 1. **密码是孩子自己设的 4 位数字**，家长不知道——这才叫「我的」日记。我们只存 PBKDF2 派生值。
/// 2. **离开就自动锁**。孩子把手机递给同学的那一刻，日记不该还开着。
/// 3. **忘记密码只能家长重置**，并且重置后家长能看到内容——这句话写在设密码的页面上，
///    不藏在隐私政策里。宁可让孩子知道「这里不是绝对安全的」，也不要给他一个假的承诺。
/// </summary>
public partial class DiaryPage : ContentPage
{
    private enum Screen { Lock, List, Editor }

    private enum LockMode { Setup, SetupConfirm, Unlock }

    private Screen _screen = Screen.Lock;
    private LockMode _mode = LockMode.Setup;

    private string _pin = string.Empty;
    private string _firstPin = string.Empty;
    private bool _busy;
    private bool _opened;

    private DiaryEntry? _editing;
    private string _moodId = string.Empty;

    private readonly List<Button> _keypadButtons = new();
    private readonly Dictionary<string, Frame> _moodCells = new();

    private IDispatcherTimer? _cooldownTimer;
    private int _cooldownLeft;

    public DiaryPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BuildKeypad();
        BuildMoodPicker();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // 从后台回来时 _opened 已在 OnDisappearing 里被清掉，必须重新输密码
        if (_opened)
        {
            ShowList();
        }
        else
        {
            ShowLock();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // 正在写的时候被切走（来电、家长锁屏），先存下来，别让孩子丢字
        if (_screen == Screen.Editor) _ = SaveEditingAsync(silent: true);

        _opened = false;
        _cooldownTimer?.Stop();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_screen == Screen.Editor)
        {
            _ = SaveEditingAsync(silent: true);
            ShowList();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    // ── 锁屏 ────────────────────────────────────────────────

    private void BuildKeypad()
    {
        Keypad.Children.Clear();
        _keypadButtons.Clear();

        var keys = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", "⌫" };

        for (var i = 0; i < keys.Length; i++)
        {
            if (keys[i].Length == 0) continue;

            var text = keys[i];
            var button = new Button
            {
                Text = text,
                FontSize = text == "⌫" ? 19 : 22,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5E35B1"),
                BackgroundColor = Colors.White,
                BorderColor = Color.FromArgb("#E6DCF8"),
                BorderWidth = 1,
                CornerRadius = 16,
                HeightRequest = 56,
                Padding = 0
            };

            if (text == "⌫") button.Clicked += OnBackspace;
            else button.Clicked += (_, _) => AppendDigit(text);

            Grid.SetColumn(button, i % 3);
            Grid.SetRow(button, i / 3);
            Keypad.Children.Add(button);
            _keypadButtons.Add(button);
        }
    }

    private void ShowLock()
    {
        _screen = Screen.Lock;
        ListPanel.IsVisible = false;
        EditorPanel.IsVisible = false;
        LockPanel.IsVisible = true;
        LockButton.IsVisible = false;
        HeaderSub.Text = "只有你的密码能打开";

        _pin = string.Empty;
        _firstPin = string.Empty;
        _cooldownTimer?.Stop();
        SetKeypadEnabled(true);

        if (DiaryLock.IsSet)
        {
            _mode = LockMode.Unlock;
            LockEmoji.Text = "🔐";
            LockTitle.Text = "我的日记";
            LockSub.Text = "输入你的 4 位密码";
            ForgotButton.IsVisible = true;
            LockFoot.Text = "密码只保存在这台设备上，我们也不知道它是什么。";

            var left = DiaryLock.RemainingLockSeconds;
            if (left > 0) StartCooldown(left);
            else HideMsg();
        }
        else
        {
            _mode = LockMode.Setup;
            LockEmoji.Text = "📔";
            LockTitle.Text = "给自己设一个密码";
            LockSub.Text = "4 位数字，只有你知道。设好之后，只有你能打开这本日记。";
            ForgotButton.IsVisible = false;
            LockFoot.Text = "如果忘了密码，只能请家长用家长密码帮你重置——日记内容会保留，但家长也能看到。"
                          + "\n所以不想让人看到的东西，别写在这里。";
            HideMsg();
        }

        UpdateDots();
    }

    private void AppendDigit(string digit)
    {
        if (_busy || _pin.Length >= 4) return;

        _pin += digit;
        UpdateDots();

        if (_pin.Length == 4) _ = CompletePinAsync();
    }

    private void OnBackspace(object? sender, EventArgs e)
    {
        if (_busy || _pin.Length == 0) return;

        _pin = _pin[..^1];
        UpdateDots();
    }

    private void UpdateDots()
    {
        var filled = _pin.Length;
        var parts = new string[4];
        for (var i = 0; i < 4; i++) parts[i] = i < filled ? "●" : "○";
        DotsLabel.Text = string.Join(" ", parts);
    }

    private async Task CompletePinAsync()
    {
        if (_busy) return;
        _busy = true;

        try
        {
            switch (_mode)
            {
                case LockMode.Setup:
                    _firstPin = _pin;
                    _pin = string.Empty;
                    _mode = LockMode.SetupConfirm;
                    LockTitle.Text = "再输一次";
                    LockSub.Text = "确认一下，别记错了";
                    UpdateDots();
                    HideMsg();
                    break;

                case LockMode.SetupConfirm:
                    if (_pin == _firstPin)
                    {
                        DiaryLock.SetPin(_pin);
                        _pin = string.Empty;
                        UpdateDots();

                        // 提示语是给「三个月后的自己」看的，不强制
                        var hint = await DisplayPromptAsync("要不要留个提示？",
                            "忘记密码时，这句话会显示在锁屏上。别把密码写在这里。",
                            "保存", "不用了", "比如：我最喜欢的那首歌", 20);

                        if (!string.IsNullOrWhiteSpace(hint)) DiaryLock.SetHint(hint.Trim());

                        Unlock();
                    }
                    else
                    {
                        _mode = LockMode.Setup;
                        _firstPin = string.Empty;
                        _pin = string.Empty;
                        LockTitle.Text = "给自己设一个密码";
                        LockSub.Text = "4 位数字，只有你知道";
                        UpdateDots();
                        Msg("两次不一样，重新设一次");
                    }
                    break;

                default:
                    VerifyPin();
                    break;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private void VerifyPin()
    {
        var pin = _pin;
        var result = DiaryLock.Verify(pin, out var remaining);
        _pin = string.Empty;
        UpdateDots();

        switch (result)
        {
            case DiaryUnlockResult.Ok:
                Unlock();
                break;

            case DiaryUnlockResult.Locked:
                StartCooldown(remaining);
                break;

            case DiaryUnlockResult.NotSet:
                ShowLock();
                break;

            default:
                if (remaining > 0)
                {
                    StartCooldown(remaining);
                }
                else
                {
                    // 输到第二次就把孩子自己留的提示露出来，别让他一直瞎猜
                    var hint = DiaryLock.Hint;
                    Msg(DiaryLock.Fails >= 2 && !string.IsNullOrWhiteSpace(hint)
                        ? $"不对哦。提示：{hint}"
                        : "不对哦，再想想");
                }
                break;
        }
    }

    private void StartCooldown(int seconds)
    {
        _cooldownLeft = seconds;
        SetKeypadEnabled(false);
        Msg($"试了太多次，等 {_cooldownLeft} 秒再试");

        _cooldownTimer?.Stop();
        _cooldownTimer = Dispatcher.CreateTimer();
        _cooldownTimer.Interval = TimeSpan.FromSeconds(1);
        _cooldownTimer.Tick += (_, _) =>
        {
            _cooldownLeft--;

            if (_cooldownLeft > 0)
            {
                Msg($"试了太多次，等 {_cooldownLeft} 秒再试");
                return;
            }

            _cooldownTimer?.Stop();
            SetKeypadEnabled(true);
            if (_screen == Screen.Lock) Msg("可以再试了", error: false);
        };
        _cooldownTimer.Start();
    }

    private void SetKeypadEnabled(bool enabled)
    {
        foreach (var b in _keypadButtons) b.IsEnabled = enabled;
    }

    private void Msg(string text, bool error = true)
    {
        LockMsg.Text = text;
        LockMsg.TextColor = Color.FromArgb(error ? "#E57373" : "#43A047");
        LockMsg.IsVisible = true;
    }

    private void HideMsg() => LockMsg.IsVisible = false;

    private void Unlock()
    {
        _opened = true;
        _pin = string.Empty;
        _cooldownTimer?.Stop();
        SetKeypadEnabled(true);
        ShowList();
    }

    private async void OnLockNow(object? sender, EventArgs e)
    {
        if (_screen == Screen.Editor) await SaveEditingAsync(silent: true);
        _editing = null;
        _opened = false;
        ShowLock();
    }

    // ── 忘记密码 ────────────────────────────────────────────

    private async void OnForgot(object? sender, EventArgs e)
    {
        var go = await DisplayAlert("忘记密码了？",
            "日记密码只有你自己知道，我们也没法帮你找回。\n\n"
            + "可以让家长用「家长密码」帮你重置。重置后日记内容都还在，但家长也能打开看到。\n\n"
            + "如果家长密码还没设置，请先到「家长中心」里设一个。",
            "让家长重置", "再想想");

        if (!go) return;

        var settings = ServiceProvider.GetOrDefault<ISettingsService>();
        if (settings is null) return;

        if (!settings.HasPin)
        {
            await DisplayAlert("还没设家长密码",
                "请家长打开首页右上角的设置，进入「家长中心」先设置一个家长密码，再回来重置日记密码。",
                "好");
            return;
        }

        var parentPin = await DisplayPromptAsync("家长密码",
            "请家长输入家长密码（不是日记密码）", "确定", "取消",
            keyboard: Keyboard.Numeric, maxLength: 8);

        if (parentPin is null) return;

        if (!settings.VerifyPin(parentPin))
        {
            await DisplayAlert("密码不对", "家长密码不正确，没有做任何改动。", "好");
            return;
        }

        var choice = await DisplayActionSheet("重置方式", "取消", null,
            "只重置日记密码（内容保留）",
            "重置并清空全部日记");

        if (choice is null || choice == "取消") return;

        if (choice.StartsWith("重置并清空", StringComparison.Ordinal))
        {
            var confirm = await DisplayAlert("确定清空？",
                $"这本日记里的 {DiaryStore.Count} 篇内容会被永久删除，删了就找不回来了。",
                "确定删除", "算了");

            if (!confirm) return;

            DiaryStore.ClearAll();
            DiaryLock.Reset();
            _editing = null;
            ShowLock();
            await DisplayAlert("已清空", "日记本已经清空，请重新设一个密码。", "好");
            return;
        }

        DiaryLock.Reset();
        _editing = null;
        ShowLock();
        await DisplayAlert("密码已重置",
            $"日记内容都还在（{DiaryStore.Count} 篇），请重新设一个密码。\n\n下次想不起来，可以再请家长帮忙。",
            "好");
    }

    // ── 列表 ────────────────────────────────────────────────

    private void ShowList()
    {
        _screen = Screen.List;
        LockPanel.IsVisible = false;
        EditorPanel.IsVisible = false;
        ListPanel.IsVisible = true;
        LockButton.IsVisible = true;
        HeaderSub.Text = "已经打开 · 离开会自动锁上";

        PromptLabel.Text = DiaryCatalog.PromptOfDay();
        RefreshStats();
        BuildEntries();
    }

    private void RefreshStats()
    {
        CountLabel.Text = DiaryStore.Count.ToString();
        MonthLabel.Text = DiaryStore.ThisMonthCount().ToString();
        StreakLabel.Text = DiaryStore.Streak().ToString();
    }

    private void BuildEntries()
    {
        EntryHost.Children.Clear();

        var keyword = SearchEntry.Text?.Trim() ?? string.Empty;
        var list = DiaryStore.Search(keyword);

        var searching = keyword.Length > 0;
        var isEmpty = list.Count == 0;

        EmptyState.IsVisible = isEmpty && !searching;

        if (isEmpty && searching)
        {
            EntryHost.Children.Add(new Frame
            {
                Padding = new Thickness(14, 16),
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Colors.White,
                BorderColor = Color.FromArgb("#EDE4FB"),
                Content = new Label
                {
                    Text = $"没有找到包含「{keyword}」的日记",
                    FontSize = 12.5,
                    TextColor = Color.FromArgb("#9C8FB5"),
                    HorizontalOptions = LayoutOptions.Center
                }
            });
            return;
        }

        foreach (var entry in list) EntryHost.Children.Add(BuildEntryCard(entry));
    }

    private View BuildEntryCard(DiaryEntry entry)
    {
        var day = DateTime.TryParse(entry.Date, out var parsed) ? parsed : DateTime.Today;

        var left = new VerticalStackLayout { Spacing = 0, WidthRequest = 50, VerticalOptions = LayoutOptions.Start };
        left.Add(new Label
        {
            Text = day.Day.ToString("00"),
            FontSize = 21,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7E57C2"),
            HorizontalOptions = LayoutOptions.Center
        });
        left.Add(new Label
        {
            Text = $"{day.Month} 月",
            FontSize = 10.5,
            TextColor = Color.FromArgb("#9C8FB5"),
            HorizontalOptions = LayoutOptions.Center
        });

        var right = new VerticalStackLayout { Spacing = 3 };
        right.Add(new Label
        {
            Text = (entry.Mood is null ? string.Empty : entry.Mood.Emoji + " ") + entry.TitleOrFallback,
            FontSize = 14.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4A3B6B"),
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation
        });
        right.Add(new Label
        {
            Text = entry.Preview,
            FontSize = 12,
            TextColor = Color.FromArgb("#8E7FA8"),
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation,
            LineHeight = 1.35
        });
        right.Add(new Label
        {
            Text = $"{entry.Time} · {entry.Words} 字",
            FontSize = 10.5,
            TextColor = Color.FromArgb("#B3A6C9")
        });

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);

        var card = new Frame
        {
            Padding = new Thickness(12, 10),
            CornerRadius = 18,
            HasShadow = false,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#EDE4FB"),
            Content = grid
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenEditorAsync(entry.Id);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => BuildEntries();

    private void OnNextPrompt(object? sender, EventArgs e) => PromptLabel.Text = DiaryCatalog.NextPrompt();

    private async void OnNewEntry(object? sender, EventArgs e) => await OpenEditorAsync(null);

    // ── 写日记 ──────────────────────────────────────────────

    private void BuildMoodPicker()
    {
        MoodHost.Children.Clear();
        _moodCells.Clear();

        var moods = CareCatalog.Moods;
        for (var i = 0; i < moods.Length; i++)
        {
            var mood = moods[i];

            var cell = new Frame
            {
                Padding = 0,
                HeightRequest = 40,
                CornerRadius = 12,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#F8F5FF"),
                BorderColor = Colors.Transparent,
                Content = new Label
                {
                    Text = mood.Emoji,
                    FontSize = 20,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => ToggleMood(mood.Id);
            cell.GestureRecognizers.Add(tap);

            Grid.SetColumn(cell, i);
            Grid.SetRow(cell, 0);
            MoodHost.Children.Add(cell);
            _moodCells[mood.Id] = cell;
        }
    }

    private void ToggleMood(string moodId)
    {
        _moodId = _moodId == moodId ? string.Empty : moodId;
        UpdateMoodSelection();
    }

    private void UpdateMoodSelection()
    {
        foreach (var (id, cell) in _moodCells)
        {
            var selected = id == _moodId;
            cell.BackgroundColor = Color.FromArgb(selected ? "#EDE7F6" : "#F8F5FF");
            cell.BorderColor = selected ? Color.FromArgb("#7E57C2") : Colors.Transparent;
        }
    }

    private async Task OpenEditorAsync(string? id)
    {
        if (id is null)
        {
            _editing = new DiaryEntry();
        }
        else
        {
            _editing = DiaryStore.Get(id);
            if (_editing is null)
            {
                await DisplayAlert("找不到这一篇", "可能已经被删掉了。", "好");
                BuildEntries();
                return;
            }
        }

        _moodId = _editing.MoodId;
        EditorDateLabel.Text = _editing.DateLabel;
        TitleEntry.Text = _editing.Title;
        BodyEditor.Text = _editing.Body;
        DeleteButton.IsVisible = id is not null;

        UpdateMoodSelection();
        UpdateWordCount();

        _screen = Screen.Editor;
        LockPanel.IsVisible = false;
        ListPanel.IsVisible = false;
        EditorPanel.IsVisible = true;
        LockButton.IsVisible = true;
        HeaderSub.Text = "正在写 · 离开会自动保存";
    }

    private void OnBodyChanged(object? sender, TextChangedEventArgs e) => UpdateWordCount();

    private void UpdateWordCount()
        => WordCountLabel.Text = $"{DiaryCatalog.CountWords(BodyEditor.Text)} 字";

    private async Task<bool> SaveEditingAsync(bool silent)
    {
        if (_editing is null) return false;

        var title = (TitleEntry.Text ?? string.Empty).Trim();
        var body = (BodyEditor.Text ?? string.Empty).TrimEnd();

        if (title.Length == 0 && DiaryCatalog.CountWords(body) == 0)
        {
            // 空白：新篇直接丢掉，旧篇保持原样（不清空孩子之前写的）
            if (!silent) await DisplayAlert("还没写呢", "写一句话再保存吧。", "好");
            return false;
        }

        _editing.Title = title;
        _editing.Body = body;
        _editing.MoodId = _moodId;

        var ok = DiaryStore.Save(_editing);

        if (!ok && !silent)
            await DisplayAlert("没存下来", "手机的存储空间可能满了，等一会儿再试一次。", "好");

        return ok;
    }

    private async void OnSaveEntry(object? sender, EventArgs e)
    {
        if (!await SaveEditingAsync(silent: false)) return;

        _editing = null;
        ShowList();
    }

    private async void OnBackToList(object? sender, EventArgs e)
    {
        await SaveEditingAsync(silent: true);
        _editing = null;
        ShowList();
    }

    private async void OnDeleteEntry(object? sender, EventArgs e)
    {
        if (_editing is null) return;

        var yes = await DisplayAlert("删掉这一篇？", "删了就找不回来了。", "删掉", "算了");
        if (!yes) return;

        DiaryStore.Delete(_editing.Id);
        _editing = null;
        ShowList();
    }

    private async void OnSpeakEntry(object? sender, EventArgs e)
    {
        var text = (BodyEditor.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            await DisplayAlert("还没写呢", "写几句再让系统读给你听。", "好");
            return;
        }

        try
        {
            await Tts.SpeakAsync(text, "zh");
        }
        catch
        {
            await DisplayAlert("读不出来", "这台设备可能还没装中文语音包，可以在系统设置里装一个。", "好");
        }
    }

    // ── 导航 ────────────────────────────────────────────────

    private async void OnBack(object? sender, EventArgs e)
    {
        if (_screen == Screen.Editor)
        {
            await SaveEditingAsync(silent: true);
            _editing = null;
            ShowList();
            return;
        }

        await Navigation.PopAsync();
    }
}
