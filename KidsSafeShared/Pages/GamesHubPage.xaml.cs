using System.Collections.ObjectModel;
using KidsSafeDesktop.Controls;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

public partial class GamesHubPage : ContentPage
{
    /// <summary>大厅内的分类：合并入口后内置小游戏和网页小游戏都在这一页。</summary>
    private enum GameFilter { All, Builtin, Web }

    /// <summary>
    /// 网页小游戏清单（打包在 Resources/Raw/Playland/pages 下，离线可玩）。
    /// 与 <see cref="PlaylandAssets"/> 的文件清单一一对应，改一处要同步改另一处。
    /// </summary>
    private static readonly (string File, string Emoji, string Title)[] WebGames =
    {
        ("plane_war.html",        "🚀", "雷霆战机"),
        ("gomoku.html",           "🎯", "欢乐五子棋"),
        ("snake_kids.html",       "🐍", "贪吃蛇大冒险"),
        ("gem_match.html",        "💎", "萌趣宝石消消乐"),
        ("animal_chess.html",     "🦁", "森林斗兽棋"),
        ("chinese_chess.html",    "♟️", "中国象棋"),
        ("penguin_hit.html",      "🐧", "冰雪打企鹅"),
        ("make_ten.html",         "🔟", "萌趣凑十消消乐"),
        ("math_pilot.html",       "🛩️", "算术小飞行员"),
        ("poem_puzzle.html",      "📜", "点字成诗大闯关"),
        ("radical_merge.html",    "🧩", "汉字合体消消乐"),
        ("word_balloon.html",     "🎈", "听音识字打气球"),
        ("pictograph_match.html", "🖼️", "象形字翻翻看"),
        ("car_rush.html",         "🚗", "小汽车大冒险"),
        ("light_puzzle.html",     "💡", "光线宝石解谜"),
    };

    private readonly GamesHubViewModel _vm;
    private readonly ObservableCollection<LauncherItem> _tiles = new();
    private GameFilter _filter = GameFilter.All;

    public GamesHubPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new GamesHubViewModel();
        BindingContext = _vm;
        BuildTiles();
        GamesGrid.ItemsSource = _tiles;
        UpdateDifficultyButtons();
        UpdateFilterButtons();
    }

    private void BuildTiles()
    {
        _tiles.Clear();

        // 内置小游戏：家长没开启的不会出现在 _vm.Games 里
        if (_filter != GameFilter.Web)
        {
            foreach (var g in _vm.Games)
            {
                var type = g.Type;
                _tiles.Add(new LauncherItem
                {
                    Name = g.Title,
                    Emoji = g.Emoji,
                    Accent = Color.FromArgb(g.Color),
                    Launch = () =>
                    {
                        var page = PageFor(type);

                        // 统一挂「本局结算奖励页」：监听 ViewModel 的 Finished / GameOver，
                        // 一局结束后自动记星星并弹出奖励页，19 款游戏不需要各写一套。
                        RewardWatcher.Attach(page, g);

                        Nav(page);
                    }
                });
            }
        }

        // 网页小游戏：统一蓝紫底色，和内置小游戏区分开
        if (_filter != GameFilter.Builtin)
        {
            foreach (var w in WebGames)
            {
                var file = "pages/" + w.File;
                var title = w.Emoji + " " + w.Title;
                _tiles.Add(new LauncherItem
                {
                    Name = w.Title,
                    Emoji = w.Emoji,
                    Accent = Color.FromArgb("#5C6BC0"),
                    Launch = () => Nav(new PlaylandPage(file, title))
                });
            }
        }
    }

    private static ContentPage PageFor(GameType type) => type switch
    {
        GameType.PictureWord => new PictureWordGamePage(),
        GameType.Counting => new CountingGamePage(),
        GameType.Matching => new MatchingGamePage(),
        GameType.FindColor => new FindColorGamePage(),
        GameType.Listen => new ListenGamePage(),
        GameType.LinkMatch => new LinkMatchGamePage(),
        GameType.Maze => new MazeGamePage(),
        GameType.SafetyQuiz => new SafetyQuizGamePage(),
        GameType.Doodle => new DoodleGamePage(),
        GameType.FishEat => new FishGamePage(),
        GameType.Arithmetic => new MathGamePage(),
        GameType.Sudoku => new SudokuGamePage(),
        GameType.HappyLiteracy => new HappyLiteracyPage(),
        GameType.PinyinLiteracy => new PinyinLiteracyPage(),
        GameType.EncyclopediaQuiz => new EncyclopediaQuizGamePage(),
        GameType.WhackMole => new WhackMoleGamePage(),
        GameType.Typing => new TypingGamePage(),
        GameType.Balloon => new BalloonGamePage(),
        GameType.Go => new GoGamePage(),
        GameType.MindRead => new MindReadGamePage(),
        GameType.Tetris => new TetrisGamePage(),
        _ => new PictureWordGamePage()
    };

    private void Nav(Page page) => Navigation.Push(page);

    private async void OnGameTapped(object sender, LauncherItem item)
        => item.Launch?.Invoke();

    private void OnBack(object sender, EventArgs e)
        => _ = Navigation.PopAsync();

    private void OnDifficultyClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var diff = btn == BtnEasy ? GameDifficulty.Easy
                  : btn == BtnMedium ? GameDifficulty.Medium
                  : GameDifficulty.Hard;
        _vm.Difficulty = diff;
        UpdateDifficultyButtons();
    }

    private void UpdateDifficultyButtons()
    {
        BtnEasy.BackgroundColor   = _vm.Difficulty == GameDifficulty.Easy   ? Color.FromArgb("#7E57C2") : Colors.White;
        BtnMedium.BackgroundColor = _vm.Difficulty == GameDifficulty.Medium ? Color.FromArgb("#7E57C2") : Colors.White;
        BtnHard.BackgroundColor   = _vm.Difficulty == GameDifficulty.Hard   ? Color.FromArgb("#7E57C2") : Colors.White;
        BtnEasy.TextColor   = _vm.Difficulty == GameDifficulty.Easy   ? Colors.White : Color.FromArgb("#4A148C");
        BtnMedium.TextColor = _vm.Difficulty == GameDifficulty.Medium ? Colors.White : Color.FromArgb("#4A148C");
        BtnHard.TextColor   = _vm.Difficulty == GameDifficulty.Hard   ? Colors.White : Color.FromArgb("#4A148C");
    }

    // ===== 分类筛选（全部 / 内置 / 网页） =====

    private void OnFilterClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        _filter = btn == BtnBuiltin ? GameFilter.Builtin
                : btn == BtnWeb ? GameFilter.Web
                : GameFilter.All;

        BuildTiles();
        UpdateFilterButtons();
    }

    private void UpdateFilterButtons()
    {
        var on = Color.FromArgb("#7E57C2");
        var onText = Colors.White;
        var off = Colors.White;
        var offText = Color.FromArgb("#4A148C");

        BtnAll.BackgroundColor     = _filter == GameFilter.All     ? on : off;
        BtnBuiltin.BackgroundColor = _filter == GameFilter.Builtin ? on : off;
        BtnWeb.BackgroundColor     = _filter == GameFilter.Web     ? on : off;

        BtnAll.TextColor     = _filter == GameFilter.All     ? onText : offText;
        BtnBuiltin.TextColor = _filter == GameFilter.Builtin ? onText : offText;
        BtnWeb.TextColor     = _filter == GameFilter.Web     ? onText : offText;
    }

    private async void OnLeaderboard(object sender, EventArgs e)
        => await Navigation.PushAsync(new LeaderboardPage());

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ServiceProvider.Get<IScreenTimeService>().LimitReached -= OnLimitReached;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ServiceProvider.Get<IScreenTimeService>().LimitReached += OnLimitReached;
        if (ServiceProvider.Get<IScreenTimeService>().IsLockedOut)
            _ = GoLockoutAsync();
    }

    private async void OnLimitReached(object? sender, EventArgs e) => await GoLockoutAsync();

    private async Task GoLockoutAsync()
    {
        if (Navigation.NavigationStack.LastOrDefault() is LockoutPage) return;
        await Navigation.PushAsync(new LockoutPage());
    }
}
