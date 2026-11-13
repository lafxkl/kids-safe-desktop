using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 俄罗斯方块：10×20 棋盘，7 种方块，消行得分、每 10 行升一级。
/// 逻辑全在这里，页面只负责每帧调 <see cref="Update"/> 和把按钮接到四个操作上。
///
/// 儿童向的三处取舍：
/// ① 不做「旋转失败要扣分/卡住」，撞墙时自动左右挪一下（wall kick），孩子乱按也不会卡死；
/// ② 消行给固定分、不看连击，避免为了刷分反复堆高危局面；
/// ③ 结束条件是「新方块放不下」，比计时更直观——屏幕堆满了就结束了。
/// </summary>
public class TetrisGameViewModel : BaseViewModel
{
    public const int Cols = 10;
    public const int Rows = 20;

    /// <summary>棋盘：0 为空，1..7 为方块类型（决定颜色）。</summary>
    public int[,] Board { get; } = new int[Rows, Cols];

    /// <summary>当前方块（4×4 掩码，1 表示实心）。</summary>
    public int[,] Piece { get; private set; } = new int[4, 4];

    public int PieceX { get; private set; }
    public int PieceY { get; private set; }

    /// <summary>当前方块的类型（0..6），绘制时用来取颜色。</summary>
    public int PieceType => _pieceType;

    /// <summary>下一个方块的类型（0..6），供右侧预览。</summary>
    public int NextType { get; private set; }

    /// <summary>幽灵落点（当前方块一直下落会停在哪一行），画半透明轮廓用。</summary>
    public int GhostY { get; private set; }

    private readonly Random _rnd = new();
    private readonly Queue<int> _bag = new();

    private int _pieceType;
    private int _score;
    private int _lines;
    private int _level = 1;
    private bool _gameOver;
    private string _resultText = string.Empty;
    private float _gravityTimer;
    private float _baseInterval = 0.9f;

    public int Score => _score;
    public int Level => _level;
    public int Lines => _lines;

    public string ScoreText => $"🏆 {_score}";
    public string LevelText => $"Lv.{_level}";
    public string ProgressText => $"等级 {_level}";

    public bool GameOver
    {
        get => _gameOver;
        private set => SetProperty(ref _gameOver, value);
    }

    public string ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    // ── 方块形状 ────────────────────────────────────────────
    // 统一用 4×4，旋转就是纯矩阵操作，不用为每种形状维护 4 个朝向。
    public static readonly int[][,] Shapes =
    {
        // I
        new int[4, 4] { { 0, 0, 0, 0 }, { 1, 1, 1, 1 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
        // O
        new int[4, 4] { { 0, 1, 1, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
        // T
        new int[4, 4] { { 0, 1, 0, 0 }, { 1, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
        // S
        new int[4, 4] { { 0, 1, 1, 0 }, { 1, 1, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
        // Z
        new int[4, 4] { { 1, 1, 0, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
        // J
        new int[4, 4] { { 1, 0, 0, 0 }, { 1, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
        // L
        new int[4, 4] { { 0, 0, 1, 0 }, { 1, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } }
    };

    /// <summary>方块配色（马卡龙系，和其余小游戏一致）。索引 1..7 对应 I O T S Z J L。</summary>
    public static readonly string[] PieceColors =
    {
        "#4DD0E1", "#FFD54F", "#BA68C8", "#81C784", "#E57373", "#64B5F6", "#FFB74D"
    };

    public void Start() => Replay();

    public void Replay()
    {
        var diff = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        _baseInterval = diff switch
        {
            GameDifficulty.Easy => 1.05f,
            GameDifficulty.Hard => 0.62f,
            _ => 0.85f
        };

        Array.Clear(Board, 0, Board.Length);
        _bag.Clear();
        _score = 0;
        _lines = 0;
        _level = 1;
        _gravityTimer = 0;
        _resultText = string.Empty;
        GameOver = false;

        NextType = NextFromBag();
        Spawn();
        RaiseStats();
    }

    // ── 每帧推进 ────────────────────────────────────────────

    public void Update(float dt)
    {
        if (GameOver) return;

        _gravityTimer += dt;
        var interval = GravityInterval();
        if (_gravityTimer >= interval)
        {
            _gravityTimer = 0;
            if (!TryMove(0, 1)) LockAndSpawn();
        }
    }

    private float GravityInterval()
    {
        // 每升一级快 0.07s，最快 0.18s（再快孩子就跟不上了）
        var t = _baseInterval - (_level - 1) * 0.07f;
        return t < 0.18f ? 0.18f : t;
    }

    // ── 四个操作 ────────────────────────────────────────────

    public void MoveLeft()
    {
        if (!GameOver) TryMove(-1, 0);
    }

    public void MoveRight()
    {
        if (!GameOver) TryMove(1, 0);
    }

    public void SoftDrop()
    {
        if (GameOver) return;
        if (TryMove(0, 1))
        {
            _score += 1;
            RaiseStats();
        }
        else
        {
            LockAndSpawn();
        }
    }

    public void Rotate()
    {
        if (GameOver) return;

        var rotated = new int[4, 4];
        for (var r = 0; r < 4; r++)
            for (var c = 0; c < 4; c++)
                rotated[r, c] = Piece[3 - c, r];

        // 撞墙就试着一格一格挪回来（wall kick）：左、右各试 1 格，再试 2 格
        foreach (var dx in new[] { 0, -1, 1, -2, 2 })
        {
            if (!CanPlace(rotated, PieceX + dx, PieceY)) continue;

            Piece = rotated;
            PieceX += dx;
            UpdateGhost();
            OnPropertyChanged(nameof(Piece));
            return;
        }
    }

    public void HardDrop()
    {
        if (GameOver) return;

        var dropped = 0;
        while (TryMove(0, 1)) dropped++;

        _score += dropped * 2;
        RaiseStats();
        LockAndSpawn();
    }

    // ── 内部：移动 / 锁定 / 消行 ──────────────────────────

    private bool TryMove(int dx, int dy)
    {
        if (!CanPlace(Piece, PieceX + dx, PieceY + dy)) return false;

        PieceX += dx;
        PieceY += dy;
        UpdateGhost();
        OnPropertyChanged(nameof(Piece));
        return true;
    }

    private bool CanPlace(int[,] mask, int x, int y)
    {
        for (var r = 0; r < 4; r++)
        {
            for (var c = 0; c < 4; c++)
            {
                if (mask[r, c] == 0) continue;

                var bx = x + c;
                var by = y + r;
                if (bx < 0 || bx >= Cols || by >= Rows) return false;
                if (by >= 0 && Board[by, bx] != 0) return false;
            }
        }
        return true;
    }

    private void UpdateGhost()
    {
        var y = PieceY;
        while (CanPlace(Piece, PieceX, y + 1)) y++;
        GhostY = y;
    }

    private void LockAndSpawn()
    {
        for (var r = 0; r < 4; r++)
        {
            for (var c = 0; c < 4; c++)
            {
                if (Piece[r, c] == 0) continue;

                var by = PieceY + r;
                var bx = PieceX + c;
                if (by < 0 || by >= Rows || bx < 0 || bx >= Cols) continue;   // 顶部溢出：忽略（已经结束）
                Board[by, bx] = _pieceType + 1;
            }
        }

        OnPropertyChanged(nameof(Board));
        ClearLines();
        Spawn();

        // 没消行时 ClearLines 不会走 RaiseStats，硬降的 +2/格 就会停在 UI 上不动
        RaiseStats();
    }

    private void ClearLines()
    {
        var cleared = 0;

        for (var r = Rows - 1; r >= 0; r--)
        {
            var full = true;
            for (var c = 0; c < Cols; c++)
            {
                if (Board[r, c] != 0) continue;
                full = false;
                break;
            }
            if (!full) continue;

            // 整行下移一格（把上面所有行往下一行搬）
            for (var r2 = r; r2 > 0; r2--)
                for (var c = 0; c < Cols; c++)
                    Board[r2, c] = Board[r2 - 1, c];

            for (var c = 0; c < Cols; c++) Board[0, c] = 0;

            cleared++;
            r++;   // 同一行位置要重新检查（新搬下来的那行）
        }

        if (cleared == 0) return;

        var table = new[] { 0, 100, 300, 500, 800 };
        _score += table[cleared] * _level;
        _lines += cleared;

        var newLevel = _lines / 10 + 1;
        if (newLevel != _level)
        {
            _level = newLevel;
            OnPropertyChanged(nameof(LevelText));
        }

        OnPropertyChanged(nameof(Board));
        RaiseStats();
    }

    private void Spawn()
    {
        _pieceType = NextType;
        NextType = NextFromBag();
        OnPropertyChanged(nameof(NextType));
        OnPropertyChanged(nameof(PieceType));

        Piece = (int[,])Shapes[_pieceType].Clone();
        PieceX = 3;
        PieceY = 0;

        // 越界（棋盘堆到顶）或刚出生就撞上，直接结束
        if (!CanPlace(Piece, PieceX, PieceY))
        {
            for (var dy = -1; dy >= -2; dy--)
            {
                if (!CanPlace(Piece, PieceX, dy)) continue;
                PieceY = dy;
                break;
            }
        }

        UpdateGhost();
        OnPropertyChanged(nameof(Piece));

        if (CanPlace(Piece, PieceX, PieceY)) return;

        GameOver = true;
        ResultText = _lines > 0
            ? $"消掉 {_lines} 行，得了 {_score} 分"
            : "方块堆到顶啦";
    }

    /// <summary>7-bag 随机：一轮 7 种各来一次，避免连续掉同一种方块。</summary>
    private int NextFromBag()
    {
        if (_bag.Count == 0)
        {
            var pool = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            while (pool.Count > 0)
            {
                var i = _rnd.Next(pool.Count);
                _bag.Enqueue(pool[i]);
                pool.RemoveAt(i);
            }
        }
        return _bag.Dequeue();
    }

    private void RaiseStats()
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(Lines));
    }
}
