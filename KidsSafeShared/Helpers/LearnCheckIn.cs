using System.Runtime.CompilerServices;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Pages;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「学完了 · 领星星」入口。
///
/// 学习页和游戏不一样，没有天然的「结束」时刻：读了几首古诗、看了几页身体百科，
/// 什么时候算学完只有孩子自己知道。所以这里不猜，给一个轻轻的浮动按钮，
/// 孩子点一下就算今天学过——然后进入激励页，看到今天累计、勋章与下一步建议。
///
/// 实现上用「包一层 Grid」的方式注入，不需要改动各学习页的 XAML；
/// 每个页面每次进入只能打卡一次，避免连点刷星星。
/// </summary>
public static class LearnCheckIn
{
    private static readonly ConditionalWeakTable<Page, StrongBox<bool>> Attached = new();

    /// <summary>
    /// 给学习页挂上入口。
    /// </summary>
    /// <param name="page">目标页面（通常在构造或 OnAppearing 里调用一次）。</param>
    /// <param name="subject">学科名，例如「古诗学习」。</param>
    /// <param name="emoji">学科图标。</param>
    /// <param name="subjectId">稳定的标识，用于统计次数，例如 "poem"。</param>
    /// <param name="stars">本次打卡给的星星（默认 2）。</param>
    /// <param name="detail">成绩/内容说明，例如「读了 3 首古诗」。</param>
    public static void Attach(
        ContentPage page,
        string subject,
        string emoji,
        string subjectId,
        int stars = 2,
        string? detail = null)
    {
        if (page is null) return;

        if (Attached.TryGetValue(page, out var box) && box.Value)
            return;
        Attached.AddOrUpdate(page, new StrongBox<bool>(true));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                InjectButton(page, subject, emoji, subjectId, stars, detail);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LearnCheckIn] 注入失败（{subject}）：{ex.Message}");
            }
        });
    }

    private static void InjectButton(
        ContentPage page,
        string subject,
        string emoji,
        string subjectId,
        int stars,
        string? detail)
    {
        // 只处理「单一内容根」的页面；结构过复杂的页面宁可不注入，也不要把布局弄坏
        if (page.Content is not View original) return;

        var label = new Label
        {
            Text = "🌟 学完了",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4E3B12"),
            VerticalOptions = LayoutOptions.Center
        };

        var button = new Frame
        {
            Padding = new Thickness(14, 8),
            CornerRadius = 22,
            HasShadow = true,
            BackgroundColor = Color.FromArgb("#FFD54F"),
            BorderColor = Color.FromArgb("#F5C518"),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 14, 18),
            Content = label,
            Opacity = 0.94
        };

        var tapped = false;
        button.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                if (tapped) return;
                tapped = true;

                label.Text = "⏳ 记录中…";
                await Task.Delay(60);

                await ShowEncourageAsync(page, subject, emoji, subjectId, stars, detail);

                label.Text = "✅ 已打卡";
                button.BackgroundColor = Color.FromArgb("#C8E6C9");
                button.BorderColor = Color.FromArgb("#A5D6A7");
                await button.FadeTo(0.55, 250);
            })
        });

        var host = new Grid();
        page.Content = null;
        host.Add(original);
        host.Add(button);
        page.Content = host;
    }

    /// <summary>
    /// 直接给某个学习行为记一次打卡并进入激励页。
    /// 供「心理小课堂」「故事屋」这类有自己的完成动作的页面调用。
    /// </summary>
    public static async Task ShowEncourageAsync(
        ContentPage page,
        string subject,
        string emoji,
        string subjectId,
        int stars,
        string? detail)
    {
        var times = StarCenter.CountOf(subjectId) + 1;

        var outcome = new RewardOutcome
        {
            Kind = "learn",
            Emoji = emoji,
            Title = subject,
            Headline = HeadlineFor(times),
            Detail = string.IsNullOrWhiteSpace(detail)
                ? $"今天第 {StarCenter.Recent(80).Count(r => r.Kind == "learn" && r.Time.Date == DateTime.Today) + 1} 次学习"
                : detail!,
            Stars = Math.Clamp(stars + 1, 1, 5),
            Award = Math.Clamp(stars, 1, 5),
            Encourage = EncourageFor(times, subject),
            NextTip = NextTipFor(subject),
            SourceId = subjectId
        };

        var award = StarCenter.Award("learn", subjectId, subject, emoji, outcome.Award, detail ?? "");

        if (page.Navigation.NavigationStack.LastOrDefault() != page)
            return;   // 已经离开，账照记，不打扰

        await page.Navigation.PushAsync(new EncouragePage(outcome, award), false);
    }

    private static string HeadlineFor(int times) => times switch
    {
        1 => "第一次打卡，很棒！",
        2 => "又来了，真不错！",
        < 5 => "坚持的样子最好看",
        < 10 => "你已经很熟练了",
        _ => "学习小达人！"
    };

    private static string EncourageFor(int times, string subject) => times switch
    {
        1 => $"第一次在「{subject}」打卡成功。刚开始不用求多，能坐下来就是胜利。",
        2 => "第二次来，说明这件事开始变成习惯了。",
        < 5 => $"第 {times} 次了。习惯就是这样，一次一次攒出来的。",
        < 10 => $"已经第 {times} 次了，你比很多小朋友都更有耐心。",
        _ => "你在这件事上花的时间，都会变成你自己的本事。"
    };

    private static string NextTipFor(string subject) => subject switch
    {
        "古诗学习" => "试着不看屏幕，把刚才那首诗背给爸爸妈妈听。",
        "英语学习" => "把今天学的单词读三遍，再指着身边的物品说一遍。",
        "口诀乐园" => "拍着手念一遍，节奏能帮记忆。",
        "身体百科" => "考考自己：刚才看到的器官，能说出它的作用吗？",
        "安全知识" => "把今天学到的一条安全知识，讲给家里人听。",
        "科学实验室" => "想一想：如果换一个条件，结果会不一样吗？",
        "眼保健操" => "做完记得看看窗外远处，让眼睛也休息一下。",
        "语音训练" => "对着镜子再说一遍，注意嘴型。",
        "故事屋" => "试着用一句话讲讲这个故事讲了什么。",
        _ => "合上屏幕，用自己的话把刚学的说一遍。"
    };
}
