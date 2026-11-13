using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 🔮 心灵感应魔法 —— 二进制猜数。
///
/// <para>
/// 玩法（原版街头魔术）：孩子在心里想一个数字 → 依次看 6 张卡片 →
/// 每张回答「在 / 不在」→ 6 次回答后报出答案。
/// 数学上这是二进制位权求和，<see cref="MindReadEngine"/> 里有完整推导。
/// </para>
///
/// <para>
/// 产品上要守住两条：
/// 一是<b>绝不要让应用显得真的在「读心」</b>，揭晓后必须给出可验证的解释，
/// 否则孩子会把魔术当迷信；
/// 二是<b>答错时不能让孩子难堪</b>，记混一张的概率不低，这里给的是
/// 「我们再来一次」而不是「你答错了」。
/// </para>
/// </summary>
public partial class MindReadGamePage : ContentPage
{
    private static readonly GameInfo Game =
        GameCatalog.All.First(g => g.Type == GameType.MindRead);

    private readonly StarfieldDrawable _starfield = new();
    private readonly List<bool> _answers = new();

    private MindReadLevel _level = MindReadLevel.Hard;
    private int[][] _cards = Array.Empty<int[]>();

    private int _index;      // 当前第几张卡
    private int _answer;     // 还原出来的数字
    private bool _settled;   // 本局是否已经记过星星
    private bool _learned;   // 是否看过揭秘
    private bool _busy;      // 防止连点两次回答

    public MindReadGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        StarView.Drawable = _starfield;

        BuildLevelBar();
        SelectLevel(MindReadLevel.Hard);
    }

    // ── 开场：选等级 ────────────────────────────────────────

    private void BuildLevelBar()
    {
        foreach (var level in new[] { MindReadLevel.Easy, MindReadLevel.Medium, MindReadLevel.Hard })
            LevelBar.Children.Add(BuildLevelChip(level));
    }

    private View BuildLevelChip(MindReadLevel level)
    {
        var name = new Label
        {
            Text = MindReadEngine.LevelName(level),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var range = new Label
        {
            Text = $"1 ~ {(int)level}",
            FontSize = 10.5,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var border = new Border
        {
            ClassId = ((int)level).ToString(),
            Padding = new Thickness(15, 9),
            StrokeThickness = 1.5,
            BackgroundColor = Color.FromArgb("#12000000"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Content = new VerticalStackLayout { Spacing = 2, Children = { name, range } }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            SfxClick();
            SelectLevel(level);
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private void SelectLevel(MindReadLevel level)
    {
        _level = level;

        IntroText.Text =
            $"在心里想一个 1 ~ {(int)level} 的数字，别说出来。\n" +
            $"我出 {MindReadEngine.CardCountFor(level)} 张卡片，你只要回答「在」或「不在」。\n" +
            "六次之后，我就知道你想的是几。";

        foreach (var child in LevelBar.Children.OfType<Border>())
        {
            var on = child.ClassId == ((int)level).ToString();
            child.BackgroundColor = on ? Color.FromArgb("#33254F") : Color.FromArgb("#12000000");
            child.Stroke = on ? Color.FromArgb("#FFD54F") : Color.FromArgb("#3E3480");

            if (child.Content is not VerticalStackLayout stack || stack.Children.Count < 2) continue;
            if (stack.Children[0] is Label name)
                name.TextColor = on ? Color.FromArgb("#FFD54F") : Color.FromArgb("#9C92CC");
            if (stack.Children[1] is Label range)
                range.TextColor = on ? Color.FromArgb("#E8DFFF") : Color.FromArgb("#6F66A0");
        }
    }

    // ── 阶段切换 ────────────────────────────────────────────

    private void OnStart(object sender, EventArgs e)
    {
        if (_busy) return;
        SfxClick();

        _cards = MindReadEngine.CardsFor(_level);
        _answers.Clear();
        _index = 0;

        StageIntro.IsVisible = false;
        StageReveal.IsVisible = false;
        StageSecret.IsVisible = false;
        StageCard.IsVisible = true;

        CardFrame.Opacity = 1;
        CardFrame.Scale = 1;

        ShowCard();
    }

    private void ShowCard()
    {
        StepLabel.Text = $"第 {_index + 1} / {_cards.Length} 张";

        NumberGrid.Children.Clear();
        NumberGrid.RowDefinitions.Clear();
        NumberGrid.ColumnDefinitions.Clear();

        var numbers = _cards[_index];

        // 4 列时格子大一些（小范围只有 8 个数字，太小的格子反而难认）
        var cols = numbers.Length <= 16 ? 4 : 5;
        var chipWidth = cols == 4 ? 60 : 48;
        var chipHeight = cols == 4 ? 48 : 42;
        var fontSize = cols == 4 ? 22 : 18;

        for (var c = 0; c < cols; c++)
            NumberGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < (numbers.Length + cols - 1) / cols; r++)
            NumberGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var i = 0; i < numbers.Length; i++)
            NumberGrid.Add(NumberChip(numbers[i], chipWidth, chipHeight, fontSize), i % cols, i / cols);

        HintLabel.Text = _index == 0
            ? "慢慢看，看到就告诉我"
            : $"第 {_index + 1} 张，慢慢找";
    }

    private static View NumberChip(int number, double width, double height, double fontSize)
        => new Border
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = Color.FromArgb("#2A2058"),
            Stroke = Color.FromArgb("#4A3E8C"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Content = new Label
            {
                Text = number.ToString(),
                FontSize = fontSize,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F3EEFF"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

    // ── 回答 ────────────────────────────────────────────────

    private async void OnYes(object sender, EventArgs e) => await AnswerAsync(true);

    private async void OnNo(object sender, EventArgs e) => await AnswerAsync(false);

    private async Task AnswerAsync(bool onCard)
    {
        if (_busy || !StageCard.IsVisible) return;
        _busy = true;

        PlayTone(onCard);
        Haptic();

        // 「在」往上弹一点，「不在」往下压一点——手感上的差别比文字更直接
        await CardFrame.ScaleTo(onCard ? 1.02 : 0.98, 70);
        await CardFrame.FadeTo(0.12, 90);

        _answers.Add(onCard);
        _index++;

        if (_index < _cards.Length)
        {
            ShowCard();
            await CardFrame.FadeTo(1, 150);
            await CardFrame.ScaleTo(1, 80);
            _busy = false;
            return;
        }

        await CardFrame.FadeTo(1, 80);
        await CardFrame.ScaleTo(1, 60);
        _busy = false;

        await RevealAsync();
    }

    private async void OnForgot(object sender, EventArgs e)
    {
        SfxClick();

        var restart = await DisplayAlert("没关系", "是不是记混了？我们重新开始，这次把它记牢一点。", "重新开始", "再想想");
        if (!restart) return;

        _answers.Clear();
        _index = 0;
        StepLabel.Text = "";
        StageCard.IsVisible = false;
        StageIntro.IsVisible = true;
    }

    // ── 揭晓 ────────────────────────────────────────────────

    private async Task RevealAsync()
    {
        _answer = MindReadEngine.Reveal(_answers);

        StageCard.IsVisible = false;
        StageReveal.IsVisible = true;
        StepLabel.Text = "";

        AnswerLabel.FontSize = 84;
        AnswerLabel.Text = "?";
        AnswerLabel.Scale = 1;
        RevealLine.Text = "";

        BallLabel.Scale = 0.72;
        await BallLabel.ScaleTo(1.08, 260, Easing.CubicOut);
        await BallLabel.ScaleTo(1.0, 150);

        if (!MindReadEngine.IsPlausible(_answer, _level))
        {
            ShowMismatch();
            return;
        }

        SecretBtn.IsVisible = true;
        DoneBtn.IsVisible = true;
        RetryBtn.IsVisible = false;

        // 数字乱跳几轮再落定，比直接显示更有「读心」的感觉
        for (var i = 0; i < 12; i++)
        {
            AnswerLabel.Text = Random.Shared.Next(1, (int)_level + 1).ToString();
            await Task.Delay(55);
        }

        AnswerLabel.Text = _answer.ToString();
        SfxWin();
        Haptic();

        await AnswerLabel.ScaleTo(1.18, 150, Easing.CubicOut);
        await AnswerLabel.ScaleTo(1.0, 210);

        RevealLine.Text = $"你回答了 {_answers.Count} 次，\n可卡片从头到尾都是背对我的。";
    }

    /// <summary>
    /// 六张答「不在」、或加出来超过上限，只有两种原因：
    /// 某一张记混了，或一开始想的数就不在范围内。两种都不该被说成「你错了」。
    /// </summary>
    private void ShowMismatch()
    {
        AnswerLabel.FontSize = 58;
        AnswerLabel.Text = "🤔";

        RevealLine.Text = _answer == 0
            ? $"六张卡片你都说「不在」——那它就不在 1 ~ {(int)_level} 里面了。\n我猜，可能有一张记混了。"
            : $"加起来是 {_answer}，比 1 ~ {(int)_level} 里最大的数字还大。\n我猜，可能有一张记混了。";

        SecretBtn.IsVisible = false;
        DoneBtn.IsVisible = false;
        RetryBtn.IsVisible = true;
    }

    private void OnRetry(object sender, EventArgs e)
    {
        SfxClick();
        ResetRound();
    }

    // ── 揭秘 ────────────────────────────────────────────────

    private void OnShowSecret(object sender, EventArgs e)
    {
        SfxClick();
        _learned = true;

        BuildSecret();

        StageReveal.IsVisible = false;
        StageSecret.IsVisible = true;
    }

    private void BuildSecret()
    {
        SecretIntroLabel.Text = MindReadEngine.SecretIntro(_level);

        SecretBox.Children.Clear();

        SecretBox.Children.Add(new Label
        {
            Text = $"{MindReadEngine.SumText(_answer)} = {_answer}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFD54F"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });

        // 六张卡当前的状态：金色＝你说了「在」，暗色＝你说了「不在」
        var grid = new Grid
        {
            ColumnSpacing = 6,
            RowSpacing = 6,
            HorizontalOptions = LayoutOptions.Center
        };

        for (var i = 0; i < _cards.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.Add(CardStateChip(i, _answers[i]), i, 0);
        }

        SecretBox.Children.Add(grid);

        SecretTipLabel.Text =
            "你可以在纸上试一个数字：先看它在哪几张卡片上，\n" +
            "再把那几张的代号加起来——每次都会对上。\n\n" +
            "下次让爸爸妈妈想一个数字，你来当魔法师。";
    }

    private static View CardStateChip(int index, bool onCard)
    {
        var weight = MindReadEngine.Weights[index];

        return new Border
        {
            WidthRequest = 40,
            HeightRequest = 60,
            BackgroundColor = onCard ? Color.FromArgb("#FFD54F") : Color.FromArgb("#1A1440"),
            Stroke = onCard ? Color.FromArgb("#FFE082") : Color.FromArgb("#3E3480"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(11) },
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = weight.ToString(),
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = onCard ? Color.FromArgb("#2A2200") : Color.FromArgb("#6F66A0"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = onCard ? "在" : "不在",
                        FontSize = 9.5,
                        TextColor = onCard ? Color.FromArgb("#4A3A00") : Color.FromArgb("#4E4580"),
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    // ── 结算 ────────────────────────────────────────────────

    private async void OnLearnDone(object sender, EventArgs e)
    {
        SfxClick();
        await SettleAsync(4);
    }

    private async void OnFinish(object sender, EventArgs e)
    {
        SfxClick();
        await SettleAsync(_learned ? 4 : 3);
    }

    /// <summary>
    /// 这个游戏里孩子没做对错判断，唯一的「表现」是诚实回答和好奇心，
    /// 所以星级只分两档：完成 3 星，看完揭秘 4 星，不给 5 星。
    /// </summary>
    private async Task SettleAsync(int stars)
    {
        if (_settled) return;
        _settled = true;

        var detail = stars >= 4
            ? $"读到了 {_answer}，还看懂了「代号相加」"
            : $"读到了 {_answer}";

        var encourage = stars >= 4
            ? "你已经知道秘密了：不是读心，是 1、2、4、8、16、32 这几个代号。下次可以给爸爸妈妈表演。"
            : "你一次都没说出口，我还是找到了它。想知道为什么的话，点「这是怎么做到的」。";

        await RewardWatcher.ShowForGameAsync(
            this, Game, stars, detail, encourage,
            () =>
            {
                ResetRound();
                return Task.CompletedTask;
            });
    }

    private void ResetRound()
    {
        _settled = false;
        _learned = false;
        _answers.Clear();
        _index = 0;
        _answer = 0;
        StepLabel.Text = "";

        StageCard.IsVisible = false;
        StageReveal.IsVisible = false;
        StageSecret.IsVisible = false;
        StageIntro.IsVisible = true;

        AnswerLabel.FontSize = 84;
        AnswerLabel.Text = "?";
        AnswerLabel.Scale = 1;
        CardFrame.Opacity = 1;
        CardFrame.Scale = 1;
    }

    private async void OnBack(object sender, EventArgs e)
    {
        // 已经揭晓但还没记账，先把星星收下再走，避免「玩完就退」白玩一局
        if (!_settled && (StageReveal.IsVisible || StageSecret.IsVisible))
        {
            await SettleAsync(StageSecret.IsVisible ? 4 : 3);
            return;
        }

        await Navigation.PopAsync();
    }

    /// <summary>安卓物理返回键走的是这条路径，同样要补一次结算，否则会绕开左上角的按钮。</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_settled && (StageReveal.IsVisible || StageSecret.IsVisible))
        {
            _ = SettleAsync(StageSecret.IsVisible ? 4 : 3);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    // ── 音效与触感 ──────────────────────────────────────────

    private static void PlayTone(bool onCard)
    {
        try
        {
            var sound = ServiceProvider.Get<ISoundService>();
            if (onCard) sound.PlayNotes(new[] { (740.0, 80), (988.0, 90) });
            else sound.PlayNotes(new[] { (392.0, 110) });
        }
        catch
        {
            // 音效不可用不影响游戏
        }
    }

    private static void SfxClick()
    {
        try { ServiceProvider.Get<ISoundService>().PlayClick(); }
        catch { }
    }

    private static void SfxWin()
    {
        try { ServiceProvider.Get<ISoundService>().PlayWin(); }
        catch { }
    }

    private static void Haptic()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); }
        catch { }
    }

    // ── 屏幕时间 ────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var st = ServiceProvider.Get<IScreenTimeService>();
            st.LimitReached += OnLimitReached;
            if (st.IsLockedOut) _ = GoLockoutAsync();
        }
        catch { }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        try { ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached; }
        catch { }
    }

    private async void OnLimitReached(object? sender, EventArgs e) => await GoLockoutAsync();

    private async Task GoLockoutAsync()
    {
        if (Navigation.NavigationStack.LastOrDefault() is LockoutPage) return;
        await Navigation.PushAsync(new LockoutPage());
    }
}
