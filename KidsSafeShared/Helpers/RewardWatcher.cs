using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Pages;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 给小游戏自动挂「结算奖励页」。
///
/// 为什么用监听而不是改 19 个游戏：
/// 各游戏的结算逻辑、分数文案、重开方式都不一样，逐个改既容易漏也很容易改坏。
/// 这里统一监听 ViewModel 的 <c>Finished</c> / <c>GameOver</c>，一旦为 true 就
/// 从 <c>ScoreText</c> / <c>ProgressText</c> / <c>ResultText</c> 里推算表现，
/// 记账后弹出同一个 <see cref="RewardResultPage"/>。
///
/// 对于没有 ViewModel 结束态的游戏（围棋、涂鸦），由页面直接调用
/// <see cref="ShowForGameAsync"/> 显式结算。
/// </summary>
public static partial class RewardWatcher
{
    [GeneratedRegex(@"第\s*(\d+)\s*[/／]\s*(\d+)")]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex NumberRegex();

    /// <summary>给一个游戏页挂上自动结算。可重复调用，同一页面只挂一次。</summary>
    public static void Attach(ContentPage page, GameInfo game)
    {
        if (page is null || game is null) return;

        var state = new WatchState(page, game);

        void Hook()
        {
            if (page.BindingContext is INotifyPropertyChanged vm)
            {
                vm.PropertyChanged -= state.OnPropertyChanged;
                vm.PropertyChanged += state.OnPropertyChanged;
                state.Vm = vm;
            }
        }

        page.BindingContextChanged += (_, _) => Hook();
        Hook();

        // 页面销毁时摘掉事件，避免长链引用
        page.Disappearing += (_, _) =>
        {
            if (page.Navigation.NavigationStack.Contains(page)) return;   // 只是被盖住，不清
            if (state.Vm is not null) state.Vm.PropertyChanged -= state.OnPropertyChanged;
        };
    }

    /// <summary>显式结算（围棋、涂鸦这类没有统一结束状态的游戏用）。</summary>
    public static async Task ShowForGameAsync(
        Page host,
        GameInfo game,
        int stars,
        string detail,
        string? encourage = null,
        Func<Task>? onReplay = null)
    {
        stars = Math.Clamp(stars, 1, 5);

        var outcome = new RewardOutcome
        {
            Kind = "game",
            Emoji = game.Emoji,
            Title = game.Title,
            Headline = HeadlineFor(stars),
            Detail = detail,
            Stars = stars,
            Award = stars,
            Encourage = encourage ?? EncourageFor(stars, game.Title),
            NextTip = stars >= 4 ? "试试更难的一档，挑战一下自己。" : "再玩一局，熟悉了就会更顺。",
            SourceId = game.Type.ToString()
        };

        var award = StarCenter.Award("game", game.Type.ToString(), game.Title, game.Emoji, stars, detail, stars >= 5);

        if (host.Navigation.NavigationStack.LastOrDefault() != host)
            return;   // 孩子已经离开这一页，不再打扰

        await host.Navigation.PushAsync(new RewardResultPage(outcome, award, onReplay), false);
    }

    // ── 内部状态 ────────────────────────────────────────────

    private sealed class WatchState
    {
        private readonly ContentPage _page;
        private readonly GameInfo _game;
        private bool _shown;

        public INotifyPropertyChanged? Vm { get; set; }

        public WatchState(ContentPage page, GameInfo game)
        {
            _page = page;
            _game = game;
        }

        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_shown) return;
            if (e.PropertyName is not ("Finished" or "GameOver")) return;

            var finished = ReadBool(sender, e.PropertyName);
            if (!finished) return;

            _shown = true;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // 让游戏自己的「最后一题」反馈先展示完，再弹奖励页
                await Task.Delay(650);
                await ShowAsync(sender);
            });
        }

        private async Task ShowAsync(object? vm)
        {
            var (stars, detail, ratio) = Evaluate(vm);

            var outcome = new RewardOutcome
            {
                Kind = "game",
                Emoji = _game.Emoji,
                Title = _game.Title,
                Headline = HeadlineFor(stars),
                Detail = detail,
                Stars = stars,
                Award = stars,
                Encourage = EncourageFor(stars, _game.Title),
                NextTip = stars >= 4 ? "表现很稳。换更难的难度再试试？" : "多玩两局，你会越来越熟练。",
                SourceId = _game.Type.ToString()
            };

            var award = StarCenter.Award("game", _game.Type.ToString(), _game.Title,
                _game.Emoji, stars, detail, ratio >= 0.95);

            if (_page.Navigation.NavigationStack.LastOrDefault() != _page)
                return;   // 已经离开，记了账但不打扰

            var replay = new Func<Task>(() =>
            {
                try
                {
                    // 大部分游戏的重开叫 Replay；数独是 Start，这里做一次兜底
                    var type = vm?.GetType();
                    var m = type?.GetMethod("Replay", BindingFlags.Public | BindingFlags.Instance)
                         ?? type?.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
                    m?.Invoke(vm, null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Reward] 重开失败：{ex.Message}");
                }
                _shown = false;   // 允许下一局再次结算
                return Task.CompletedTask;
            });

            await _page.Navigation.PushAsync(new RewardResultPage(outcome, award, replay), false);
        }

        /// <summary>从 ViewModel 里尽量读出「得分 / 总题数」，读不到就给 3 星鼓励。</summary>
        private (int Stars, string Detail, double Ratio) Evaluate(object? vm)
        {
            var scoreText = ReadString(vm, "ScoreText");
            var progressText = ReadString(vm, "ProgressText");
            var resultText = ReadString(vm, "ResultText");

            var score = FirstNumber(scoreText);
            var total = 0;
            var m = ProgressRegex().Match(progressText ?? string.Empty);
            if (m.Success && int.TryParse(m.Groups[2].Value, out var t)) total = t;

            double ratio = 0;
            if (score > 0 && total > 0 && score <= total)
                ratio = score / (double)total;

            var stars = ratio > 0 ? StarCenter.StarsFromRatio(ratio) : (score > 0 ? 3 : 2);

            // 成绩说明优先用短的结果文案，太长就用得分
            var detail = !string.IsNullOrWhiteSpace(resultText) && resultText!.Length <= 40
                ? resultText
                : !string.IsNullOrWhiteSpace(scoreText) && total > 0
                    ? $"答对 {score} / {total}，拿到 {score} 分"
                    : !string.IsNullOrWhiteSpace(scoreText)
                        ? scoreText!
                        : "完成了一局";

            return (stars, detail, ratio);
        }
    }

    // ── 文案与工具 ──────────────────────────────────────────

    private static string HeadlineFor(int stars) => stars switch
    {
        5 => "满分！太厉害了",
        4 => "干得漂亮！",
        3 => "不错哦！",
        2 => "有进步！",
        _ => "继续加油！"
    };

    private static string EncourageFor(int stars, string game) => stars switch
    {
        5 => $"「{game}」这一局几乎没错，说明你已经掌握了。要不要挑战更难的？",
        4 => "只差一点点就满分，说明方法是对的，再稳一点就好。",
        3 => "完成一局就是一次练习。玩得开心，比赢更重要。",
        2 => "有点难对不对？没关系，多试几次脑子就记住了。",
        _ => "愿意坐下来试一次，就已经很勇敢了。"
    };

    private static bool ReadBool(object? vm, string prop)
    {
        try
        {
            var v = vm?.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)?.GetValue(vm);
            return v is bool b && b;
        }
        catch { return false; }
    }

    private static string? ReadString(object? vm, string prop)
    {
        try
        {
            return vm?.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)?.GetValue(vm) as string;
        }
        catch { return null; }
    }

    private static int FirstNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var m = NumberRegex().Match(text!);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : 0;
    }
}
