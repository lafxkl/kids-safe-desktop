using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 表情配对（社交情绪 · 情绪识别与归因）。
/// 训练逻辑：看表情 + 读情境句，从四个情绪词里选出最匹配的一个。
/// 依据：表情是口语之外最主要的情绪线索；听障与孤独症谱系儿童常在「读脸」上遇到困难。
/// 把表情与具体情境配对，练的是心理理论（Theory of Mind）的前置链条：识别 → 命名 → 归因。
/// 无障碍考量：正误不用红绿单一编码（照顾色觉障碍），而是同时用「✅ / ❌」符号 + 振动 + 文字说明。
/// </summary>
public partial class EmotionMatchGamePage : ContentPage
{
    private const int QuizCount = 10;

    private readonly IReadOnlyList<EmotionCard> _all = SpecialTrainingData.Emotions;
    private readonly Random _rnd = new();
    private readonly List<EmotionCard> _queue = new();

    private int _index;
    private int _correct;
    private string _answer = "";
    private bool _locked;
    private bool _started;

    public EmotionMatchGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        BuildQueue();
        NextQuestion();
    }

    private void NextQuestion()
    {
        if (_index >= QuizCount || _queue.Count == 0)
        {
            _ = FinishAsync();
            return;
        }

        _index++;
        ProgressLabel.Text = $"第 {_index} / {QuizCount} 题";
        FeedbackLabel.Text = "";

        var target = _queue[0];
        _queue.RemoveAt(0);
        _answer = target.Name;

        FaceLabel.Text = target.Face;
        SceneLabel.Text = target.Scene;

        // 三个干扰项：名称不同于正确答案
        var names = new List<string> { target.Name };
        int guard = 0;
        while (names.Count < 4 && guard++ < 120)
        {
            var w = _all[_rnd.Next(_all.Count)];
            if (names.Any(n => n == w.Name)) continue;
            names.Add(w.Name);
        }

        for (int i = names.Count - 1; i > 0; i--)
        {
            int j = _rnd.Next(i + 1);
            (names[i], names[j]) = (names[j], names[i]);
        }

        BuildOptions(names);
    }

    private void BuildOptions(List<string> names)
    {
        OptionsGrid.Children.Clear();
        for (int i = 0; i < names.Count && i < 4; i++)
        {
            var btn = new Button
            {
                Text = names[i],
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#EDE7F6"),
                TextColor = Color.FromArgb("#4527A0"),
                CornerRadius = 20,
                HeightRequest = 82,
                CommandParameter = names[i]
            };
            SemanticProperties.SetDescription(btn, names[i]);
            btn.Clicked += OnOptionClicked;

            Grid.SetRow(btn, i / 2);
            Grid.SetColumn(btn, i % 2);
            OptionsGrid.Children.Add(btn);
        }
    }

    private async void OnOptionClicked(object sender, EventArgs e)
    {
        if (_locked || sender is not Button btn) return;
        var picked = btn.CommandParameter?.ToString() ?? "";

        _locked = true;

        if (picked == _answer)
        {
            _correct++;
            A11y.Success();
            btn.Text = $"✅ {picked}";
            btn.BackgroundColor = Color.FromArgb("#C8E6C9");
            btn.TextColor = Color.FromArgb("#1B5E20");
            FeedbackLabel.Text = $"✅ 对，这是「{_answer}」";
            await A11y.SayAsync(_answer);
        }
        else
        {
            A11y.Fail();
            btn.Text = $"❌ {picked}";
            btn.BackgroundColor = Color.FromArgb("#FFE0B2");
            btn.TextColor = Color.FromArgb("#E65100");

            foreach (var c in OptionsGrid.Children)
            {
                if (c is Button b && b.CommandParameter?.ToString() == _answer)
                {
                    b.Text = $"✅ {_answer}";
                    b.BackgroundColor = Color.FromArgb("#C8E6C9");
                    b.TextColor = Color.FromArgb("#1B5E20");
                }
            }
            FeedbackLabel.Text = $"再想想，正确的是「{_answer}」";
        }

        await Task.Delay(1100);
        _locked = false;
        if (IsLoaded) NextQuestion();
    }

    private async Task FinishAsync()
    {
        ResultLayer.IsVisible = true;
        ResultTitle.Text = _correct >= 9 ? "很会看表情！"
                        : _correct >= 6 ? "不错，继续练"
                        : "慢慢来，多说说感受";
        ResultDetail.Text = $"共 {QuizCount} 题，答对 {_correct} 题";

        await A11y.SayAsync($"练完啦，答对{_correct}题，一共{QuizCount}题");
    }

    private void OnReplayAll(object sender, EventArgs e)
    {
        ResultLayer.IsVisible = false;
        _index = 0;
        _correct = 0;
        BuildQueue();
        NextQuestion();
    }

    /// <summary>打乱题序，保证同一轮里尽量不重复出同一张表情卡。</summary>
    private void BuildQueue()
    {
        _queue.Clear();
        foreach (var e in _all) _queue.Add(e);

        for (int i = _queue.Count - 1; i > 0; i--)
        {
            int j = _rnd.Next(i + 1);
            (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
        }

        // 题库不足 10 张时循环补足
        int k = 0;
        while (_queue.Count < QuizCount && _all.Count > 0)
            _queue.Add(_all[k++ % _all.Count]);
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
