using System.Linq;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 围棋小课堂：9 路棋盘，小朋友执黑先手，对战一个「会吃子、会躲打吃」的简易电脑。
///
/// 交互约定：
/// - 只在轮到玩家（黑棋）且电脑没在思考时接受点击，避免连点造成两手棋；
/// - 非法点不弹窗（弹窗会打断下棋节奏），直接用状态条一句话说明原因；
/// - 「悔棋」一次退回到玩家该下的时候（通常是两手），孩子下错了不用整局重来。
/// </summary>
public partial class GoGamePage : ContentPage
{
    private readonly GoEngine _engine = new();
    private readonly GoBoardDrawable _drawable = new();
    private readonly GameDifficulty _difficulty;

    private bool _thinking;
    private bool _alive = true;
    private bool _rewarded;   // 本局是否已经发过结算奖励（防止重复弹页面）

    public GoGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        try { _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty; }
        catch { _difficulty = GameDifficulty.Medium; }

        _drawable.Engine = _engine;
        Board.Drawable = _drawable;
        UpdateUi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _alive = true;
        _ = ShowRulesOnceAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _alive = false;
    }

    /// <summary>第一次进入时讲一遍规则，之后不再打扰。</summary>
    private async Task ShowRulesOnceAsync()
    {
        try
        {
            if (Preferences.Get("go_rules_seen", false)) return;
            Preferences.Set("go_rules_seen", true);

            await DisplayAlert("围棋怎么玩",
                "你执黑棋，先下。点棋盘上的交叉点就能落子。\n\n" +
                "• 棋子上下左右连在一起，周围空着的点就是它的「气」\n" +
                "• 把对方一块棋的气全堵住，就能把它提掉\n" +
                "• 自己下下去就没气的地方不能下（禁入点）\n" +
                "• 下完点「🏁 数子」，谁占的地方多谁赢\n\n" +
                "小提示：先占角，再围边，最后才去中间。",
                "开始下棋");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[围棋] 规则说明弹窗失败: {ex.Message}");
        }
    }

    // ── 落子 ─────────────────────────────────────────────

    /// <summary>转屏 / 分屏 / 折叠屏切换时画布尺寸会变，重绘一次按新尺寸排版。</summary>
    private void OnBoardHostSizeChanged(object? sender, EventArgs e)
    {
        if (Board.Width > 0 && Board.Height > 0) Board.Invalidate();
    }

    private async void OnBoardTap(object? sender, TouchEventArgs e)
    {
        if (!_alive || _thinking || _engine.Finished) return;
        if (_engine.Turn != GoEngine.Black) return;
        if (e.Touches.Length == 0) return;

        var p = e.Touches[0];
        var (cell, ox, oy, _) = GoBoardDrawable.Measure((float)Board.Width, (float)Board.Height);
        if (cell <= 0) return;

        var col = (int)Math.Round((p.X - ox) / cell);
        var row = (int)Math.Round((p.Y - oy) / cell);

        if (row < 0 || row >= GoEngine.Size || col < 0 || col >= GoEngine.Size) return;

        // 离交叉点太远就不算落子，避免误触
        if (Math.Abs(p.X - (ox + col * cell)) > cell * 0.62 ||
            Math.Abs(p.Y - (oy + row * cell)) > cell * 0.62) return;

        var result = _engine.Play(row, col);
        switch (result)
        {
            case GoMoveResult.Occupied:
                StatusLabel.Text = "这里已经有棋子啦，换一个点试试～";
                return;
            case GoMoveResult.Suicide:
                StatusLabel.Text = "下这里会被吃掉，换个地方吧～";
                return;
            case GoMoveResult.Ko:
                StatusLabel.Text = "打劫！不能马上提回来，先下别处～";
                return;
            case GoMoveResult.Finished:
                return;
        }

        PlaySound(sim: false);
        UpdateUi();

        await ComputerTurnAsync();
    }

    private async Task ComputerTurnAsync()
    {
        if (!_alive || _engine.Finished) return;

        _thinking = true;
        UpdateUi();

        // 稍等一下再下，让小朋友看清楚自己刚才那手棋
        await Task.Delay(_difficulty == GameDifficulty.Easy ? 560 : 430);
        if (!_alive) return;

        var move = GoAi.ChooseMove(_engine, _difficulty);
        if (move is { } m)
        {
            var res = _engine.Play(m.Row, m.Col);
            if (res != GoMoveResult.Ok) _engine.Pass();
        }
        else
        {
            _engine.Pass();
        }

        _thinking = false;

        if (_engine.Finished) ShowResult();
        else UpdateUi();
    }

    // ── 操作 ─────────────────────────────────────────────

    private void OnUndo(object sender, EventArgs e)
    {
        if (_thinking) return;

        // 退到「该玩家下」为止：电脑刚下完就退两手，玩家刚下完只退一手
        if (!_engine.Undo()) return;
        if (_engine.Turn != GoEngine.Black && _engine.CanUndo) _engine.Undo();

        ResultLayer.IsVisible = false;
        UpdateUi();
        StatusLabel.Text = _engine.MoveCount == 0 ? "轮到你啦，你执黑先行" : "已经退回来啦，重新想一步～";
    }

    private void OnRestart(object sender, EventArgs e)
    {
        if (_thinking) return;

        ResultLayer.IsVisible = false;
        _rewarded = false;
        while (_engine.CanUndo) _engine.Undo();   // 用悔棋回到空盘，保持引擎只有一处状态
        UpdateUi();
        StatusLabel.Text = "新的一局开始，你执黑先行";
    }

    private void OnCountScore(object sender, EventArgs e)
    {
        if (_thinking) return;

        if (!_engine.Finished)
        {
            // 只有一方落过子时，整盘空点都会判给这一方（81:0），对孩子完全是误导。
            // 双方各下过一手后大片空域会同时挨着两色，结果才合理。
            if (_engine.MoveCount < 2)
            {
                StatusLabel.Text = "先下几手棋，再来数子吧～";
                return;
            }

            // 连续两次停一手即终局（围棋的正规结束方式）
            _engine.Pass();
            _engine.Pass();
        }
        ShowResult();
    }

    // ── 界面刷新 ─────────────────────────────────────────

    private void UpdateUi()
    {
        MoveLabel.Text = $"第 {_engine.MoveCount} 手";
        CaptureLabel.Text = $"提子 黑 {_engine.CapturedByBlack} : 白 {_engine.CapturedByWhite}";
        UndoBtn.IsEnabled = _engine.CanUndo && !_thinking;

        if (_engine.Finished) StatusLabel.Text = "对局结束";
        else if (_thinking) StatusLabel.Text = "电脑正在思考…";
        else StatusLabel.Text = _engine.Turn == GoEngine.Black
            ? "轮到你啦（你执黑）"
            : "轮到白棋";

        Board.Invalidate();
    }

    private void ShowResult()
    {
        var (black, white) = _engine.AreaScore();
        var diff = black - white;

        ResultTitle.Text = diff > 0 ? "🎉 你赢啦！" : diff < 0 ? "电脑赢了这一局" : "平局！";
        ResultScore.Text = $"黑棋 {black} 子　·　白棋 {white} 子\n相差 {Math.Abs(diff)} 子";        ResultTip.Text = diff switch
        {
            > 0 => "你围的地盘比电脑多。想再试试别的开局吗？",
            < 0 => "电脑多围了几个点。试着先占角、把角上的空地围起来，下次就能赢回来。",
            _ => "一人一半，正好平手。再来一局分个胜负吧！"
        };

        ResultLayer.IsVisible = true;
        PlaySound(sim: true);

        // 结算后把状态条也同步，避免背后还写着「轮到白棋」
        UpdateUi();

        // 围棋没有统一的 ViewModel 结束态，所以显式调用结算奖励页
        _ = RewardForGoAsync(diff, black, white);
    }

    /// <summary>
    /// 围棋结算是「赢 / 平 / 输」，没有正确率，因此按目数差换算星级；
    /// 输棋也给 2 星，重点是不让孩子因为输而不敢再下。
    /// </summary>
    private async Task RewardForGoAsync(int diff, int black, int white)
    {
        if (_rewarded) return;
        _rewarded = true;

        await Task.Delay(900);   // 先让孩子看清棋盘结果，再弹出奖励页

        var stars = diff >= 10 ? 5 : diff > 0 ? 4 : diff == 0 ? 3 : diff >= -10 ? 3 : 2;

        var game = GameCatalog.All.First(g => g.Type == GameType.Go);
        var encourage = diff > 0
            ? "你守住了自己的地盘，还吃掉了对方的子。围棋赢在耐心，你做到了。"
            : diff < 0
                ? "电脑多围了几个点。先占角、再守边，下次就能赢回来。"
                : "平局说明你和电脑实力相当，再来一局就有胜负了。";

        await RewardWatcher.ShowForGameAsync(
            this, game, stars,
            $"黑棋 {black} 子 · 白棋 {white} 子",
            encourage,
            () =>
            {
                OnRestart(this, EventArgs.Empty);
                _rewarded = false;
                return Task.CompletedTask;
            });
    }

    private static void PlaySound(bool sim)
    {
        try
        {
            var sound = ServiceProvider.Get<ISoundService>();
            if (sim) sound.PlayWin();
            else sound.PlayClick();
        }
        catch
        {
            // 音效不是必需能力，失败不影响下棋
        }
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
