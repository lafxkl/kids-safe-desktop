using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 走迷宫：用回溯算法生成迷宫，熊猫从起点走到终点即获胜。
/// 点击与熊猫相邻的通道格子即可移动一步。
/// </summary>
public class MazeGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private GameDifficulty _difficulty = GameDifficulty.Medium;
    private bool[][] _maze = System.Array.Empty<bool[]>();
    private int _dim;
    private int _pr = 1, _pc = 1;
    private int _moves;
    private bool _finished;
    private string _difficultyLabel = "中等", _resultText = string.Empty;

    public int Dim => _dim;
    public bool Finished
    {
        get => _finished;
        set
        {
            if (SetProperty(ref _finished, value))
                OnPropertyChanged(nameof(NotFinished));
        }
    }
    public bool NotFinished => !_finished;

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    public int PlayerRow
    {
        get => _pr;
        private set => SetProperty(ref _pr, value);
    }

    public int PlayerCol
    {
        get => _pc;
        private set => SetProperty(ref _pc, value);
    }

    public (int r, int c) StartPos => (1, 1);
    public (int r, int c) End => (_dim - 2, _dim - 2);

    public bool IsPassage(int r, int c)
        => r >= 0 && c >= 0 && r < _dim && c < _dim && _maze[r][c];

    public void Start()
    {
        _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        DifficultyLabel = _difficulty.Label();

        var n = _difficulty switch
        {
            GameDifficulty.Easy => 6,
            GameDifficulty.Medium => 8,
            GameDifficulty.Hard => 10,
            _ => 8
        };
        _dim = 2 * n + 1;
        Generate();
        _pr = 1; _pc = 1;
        _moves = 0;
        Finished = false;
    }

    private void Generate()
    {
        var dim = _dim;
        var visited = new bool[dim][];
        _maze = new bool[dim][];
        for (var r = 0; r < dim; r++)
        {
            visited[r] = new bool[dim];
            _maze[r] = new bool[dim]; // 全部为墙
        }

        // 回溯法（迭代）开通道
        var stack = new System.Collections.Generic.Stack<(int r, int c)>();
        _maze[1][1] = true;
        visited[1][1] = true;
        stack.Push((1, 1));

        var dirs = new[] { (-2, 0), (2, 0), (0, -2), (0, 2) };

        while (stack.Count > 0)
        {
            var (cr, cc) = stack.Peek();
            var neighbors = new System.Collections.Generic.List<(int r, int c)>();
            foreach (var (dr, dc) in dirs)
            {
                var nr = cr + dr;
                var nc = cc + dc;
                if (nr > 0 && nc > 0 && nr < dim - 1 && nc < dim - 1 && !visited[nr][nc])
                    neighbors.Add((nr, nc));
            }

            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var pick = neighbors[_rnd.Next(neighbors.Count)];
            // 打通中间的墙
            _maze[(cr + pick.r) / 2][(cc + pick.c) / 2] = true;
            _maze[pick.r][pick.c] = true;
            visited[pick.r][pick.c] = true;
            stack.Push(pick);
        }
    }

    public void TryMove(int r, int c)
    {
        if (Finished) return;
        if (!IsPassage(r, c)) return;
        if (System.Math.Abs(r - _pr) + System.Math.Abs(c - _pc) != 1) return;

        var sound = ServiceProvider.Get<ISoundService>();
        sound.PlayClick();
        PlayerRow = r;
        PlayerCol = c;
        _moves++;

        if (r == End.r && c == End.c)
            Win();
    }

    private void Win()
    {
        Finished = true;
        var sound = ServiceProvider.Get<ISoundService>();
        var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
        {
            Game = GameType.Maze,
            Difficulty = _difficulty,
            Score = 1,
            MaxScore = 1,
            Date = System.DateTime.Now,
            Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
        });
        ResultText = isBest
            ? $"🎉 新纪录！用了 {_moves} 步走出迷宫！"
            : $"你带熊猫走出迷宫啦，用了 {_moves} 步！🐼";
        sound.PlayWin();
    }

    public void Replay() => Start();
}
