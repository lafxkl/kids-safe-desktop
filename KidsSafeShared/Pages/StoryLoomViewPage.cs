using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls.Shapes;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 故事库里的「看一个收藏的故事」。
///
/// 用纯代码搭页面（和家务页的编辑弹窗同一套做法）：内容只有标题 + 四段正文 + 两个按钮，
/// 为它单开一个 XAML 反而增加维护成本。
/// </summary>
public sealed class StoryLoomViewPage : ContentPage
{
    private readonly LoomSavedStory _story;
    private readonly List<Label> _paras = new();
    private readonly Button _readBtn;

    private CancellationTokenSource? _cts;
    private bool _speaking;

    public StoryLoomViewPage(LoomSavedStory story)
    {
        _story = story;
        Title = story.Title;
        Padding = 0;

        // 页面自己画了返回按钮和标题，系统导航栏再出现就多一条白条。
        NavigationPage.SetHasNavigationBar(this, false);

        Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb("#2A2F6E"), 0f),
                new(Color.FromArgb("#141A3F"), 0.45f),
                new(Color.FromArgb("#0A0E2A"), 1f)
            },
            new Point(0.75, 0), new Point(0.25, 1));

        var back = new Button
        {
            Text = "← 返回",
            FontSize = 15,
            TextColor = Color.FromArgb("#EEF1FF"),
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0
        };
        back.Clicked += async (_, _) => await Navigation.PopAsync();

        var head = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            Padding = new Thickness(10, 10, 16, 0)
        };
        head.Add(back, 0, 0);
        head.Add(new Label
        {
            Text = "📚 故事库",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFE9A8"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var body = new VerticalStackLayout { Padding = new Thickness(18, 6, 18, 30), Spacing = 12 };

        body.Children.Add(new Label
        {
            Text = $"🌛 {story.Title}",
            FontSize = 21,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFE9A8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        body.Children.Add(new Label
        {
            Text = $"{story.Mood} · 收藏于 {story.TimeText}",
            FontSize = 11.5,
            TextColor = Color.FromArgb("#8AB4FF")
        });

        foreach (var p in story.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var label = new Label
            {
                Text = p.Trim(),
                FontSize = 15,
                LineHeight = 1.6,
                TextColor = Color.FromArgb("#DCE3FF")
            };
            _paras.Add(label);
            body.Children.Add(label);
        }

        _readBtn = new Button
        {
            Text = "🔊 读一读",
            FontSize = 13.5,
            CornerRadius = 14,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#2A3568"),
            TextColor = Color.FromArgb("#FFE9A8")
        };
        _readBtn.Clicked += OnRead;

        var del = new Button
        {
            Text = "🗑 删除",
            FontSize = 13.5,
            CornerRadius = 14,
            HeightRequest = 42,
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#3A4680"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#8C95C4")
        };
        del.Clicked += OnDelete;

        var acts = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0)
        };
        acts.Add(_readBtn, 0, 0);
        acts.Add(del, 1, 0);
        body.Children.Add(acts);

        body.Children.Add(new Label
        {
            Text = "🌙 晚安，明天见。",
            FontSize = 13,
            TextColor = Color.FromArgb("#FFD60A"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 6, 0, 0)
        });

        var scroll = new ScrollView { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Never };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(head, 0, 0);
        root.Add(scroll, 0, 1);

        Content = root;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTts();
    }

    private async void OnRead(object? sender, EventArgs e)
    {
        if (_speaking)
        {
            StopTts();
            return;
        }

        _speaking = true;
        _readBtn.Text = "⏹ 停下";
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var paras = _paras.Select(p => p.Text ?? "").Where(t => t.Length > 0).ToList();
            for (var i = 0; i < paras.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                Highlight(i);
                await Tts.SpeakAsync(paras[i], "zh", ct, VoiceStyles.Current.Pitch);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loom] 收藏故事朗读失败：{ex.Message}");
        }
        finally
        {
            Highlight(-1);
            StopTts();
        }
    }

    private void Highlight(int index)
    {
        for (var i = 0; i < _paras.Count; i++)
        {
            var on = i == index;
            _paras[i].TextColor = on ? Color.FromArgb("#FFE9A8") : Color.FromArgb("#DCE3FF");
            _paras[i].FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void StopTts()
    {
        try { _cts?.Cancel(); } catch { /* 忽略 */ }
        _cts?.Dispose();
        _cts = null;
        _speaking = false;
        if (_readBtn is not null) _readBtn.Text = "🔊 读一读";
        Highlight(-1);
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        var ok = await DisplayAlert("删除这个故事", "删掉之后就找不回来了，确定吗？", "删除", "再想想");
        if (!ok) return;

        StopTts();
        StoryLoomStore.RemoveStory(_story);
        await Navigation.PopAsync();
    }
}

/// <summary>
/// 大模型引擎设置（家长区，需 PIN）。
///
/// 页面上刻意把「会发到哪台服务器」写在按钮旁边，而不是只写「高级设置」：
/// 家长点开这一页就该明白自己在授权什么。
/// </summary>
public sealed class StoryLoomAiPage : ContentPage
{
    private readonly Switch _enabled;
    private readonly Entry _base;
    private readonly Entry _model;
    private readonly Entry _key;
    private readonly Label _tip;
    private readonly Button _test;

    public StoryLoomAiPage()
    {
        Title = "大模型故事引擎";
        Padding = 0;
        Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb("#2A2F6E"), 0f),
                new(Color.FromArgb("#141A3F"), 0.45f),
                new(Color.FromArgb("#0A0E2A"), 1f)
            },
            new Point(0.75, 0), new Point(0.25, 1));

        _enabled = new Switch { OnColor = Color.FromArgb("#FFD60A"), ThumbColor = Color.FromArgb("#FFE9A8") };
        _base = Input("https://api.modelarts-maas.com/v2");
        _model = Input("deepseek-v4.1-flash");
        _key = Input("在这里粘贴 API Key");
        _key.IsPassword = true;

        _tip = new Label
        {
            Text = "",
            FontSize = 11.5,
            TextColor = Color.FromArgb("#8C95C4"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _test = new Button
        {
            Text = "🔌 测试连接",
            FontSize = 13,
            CornerRadius = 14,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#2A3568"),
            TextColor = Color.FromArgb("#FFE9A8")
        };
        _test.Clicked += OnTest;

        var save = new Button
        {
            Text = "💾 保存",
            FontSize = 13,
            CornerRadius = 14,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#FFD60A"),
            TextColor = Color.FromArgb("#2A2200")
        };
        save.Clicked += OnSave;

        var close = new Button
        {
            Text = "关闭",
            FontSize = 13,
            CornerRadius = 14,
            HeightRequest = 42,
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#3A4680"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#8C95C4")
        };
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var acts = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 6, 0, 0)
        };
        acts.Add(_test, 0, 0);
        acts.Add(save, 1, 0);

        var body = new VerticalStackLayout { Padding = new Thickness(18, 8, 18, 28), Spacing = 12 };
        body.Children.Add(new Label
        {
            Text = "🤖 大模型故事引擎",
            FontSize = 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFE9A8")
        });
        body.Children.Add(new Label
        {
            Text = "默认关闭。关闭时故事由内置模板完成，完全离线；开启后孩子说的一句话会发送到下面这台服务器。",
            FontSize = 12,
            TextColor = Color.FromArgb("#AAB2D8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        body.Children.Add(RowLabel("启用 AI 生成"));
        var swRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(12, 4)
        };
        swRow.Add(new Label
        {
            Text = "打开后优先用大模型写，失败自动回退模板",
            FontSize = 11.5,
            TextColor = Color.FromArgb("#8C95C4"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        swRow.Add(_enabled, 1, 0);
        body.Children.Add(swRow);

        body.Children.Add(RowLabel("API 地址"));
        body.Children.Add(_base);
        body.Children.Add(RowLabel("模型名称"));
        body.Children.Add(_model);
        body.Children.Add(RowLabel("API Key"));
        body.Children.Add(_key);

        body.Children.Add(new Label
        {
            Text = "Key 存在本机的安全存储里（Android Keystore / Windows 数据保护），不会同步到任何地方。",
            FontSize = 10.5,
            TextColor = Color.FromArgb("#6E79A8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        body.Children.Add(acts);
        body.Children.Add(_tip);
        body.Children.Add(close);

        Content = new ScrollView { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _enabled.IsToggled = StoryLoomAi.Enabled;
        _base.Text = StoryLoomAi.BaseUrl;
        _model.Text = StoryLoomAi.Model;
        try { _key.Text = await StoryLoomAi.GetKeyAsync(); } catch { /* 忽略 */ }
    }

    private static Label RowLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#C9D2F5"),
        Margin = new Thickness(0, 4, 0, 0)
    };

    private static Entry Input(string placeholder) => new()
    {
        FontSize = 13,
        TextColor = Color.FromArgb("#EEF1FF"),
        PlaceholderColor = Color.FromArgb("#6E79A8"),
        BackgroundColor = Color.FromArgb("#1C244E"),
        Placeholder = placeholder
    };

    private async void OnTest(object? sender, EventArgs e)
    {
        _tip.TextColor = Color.FromArgb("#8C95C4");
        _tip.Text = "正在连接…";
        _test.IsEnabled = false;

        try
        {
            await StoryLoomAi.SaveAsync(_enabled.IsToggled,
                _base.Text ?? "", _model.Text ?? "", _key.Text ?? "");

            var (ok, message) = await StoryLoomAi.TestAsync();
            _tip.TextColor = ok ? Color.FromArgb("#34C759") : Color.FromArgb("#FF6B6B");
            _tip.Text = message;
        }
        catch (Exception ex)
        {
            _tip.TextColor = Color.FromArgb("#FF6B6B");
            _tip.Text = "连接失败 ❌ " + ex.Message;
        }
        finally
        {
            _test.IsEnabled = true;
        }
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        await StoryLoomAi.SaveAsync(_enabled.IsToggled,
            _base.Text ?? "", _model.Text ?? "", _key.Text ?? "");

        _tip.TextColor = Color.FromArgb("#34C759");
        _tip.Text = _enabled.IsToggled ? "已保存 ✅ 下次生成会优先用大模型" : "已保存 ✅ 继续使用内置模板";
    }
}
