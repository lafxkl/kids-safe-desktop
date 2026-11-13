using System.Text;

namespace KidsSafeDesktop.Models;

/// <summary>一次落子的结果。</summary>
public enum GoMoveResult
{
    /// <summary>落子成功。</summary>
    Ok,
    /// <summary>该点已经有棋子。</summary>
    Occupied,
    /// <summary>坐标越界。</summary>
    OutOfBoard,
    /// <summary>禁入点：落下后自己这块棋没有气。</summary>
    Suicide,
    /// <summary>打劫：不能立刻把棋盘恢复成上一手之前的样子。</summary>
    Ko,
    /// <summary>棋局已经结束。</summary>
    Finished
}

/// <summary>
/// 9 路围棋规则引擎（中国规则数子法）。
///
/// 面向小朋友做的取舍：
/// 1) 只做 9 路小棋盘 —— 81 个点，一局十几分钟能下完，不会劝退；
/// 2) 规则完整但只保留必需的：气、提子、禁自杀点、打劫（禁止立即复现上一手局面）；
/// 3) 不走「数目」的复杂判定，收完官直接按中国规则数子（子 + 单方围住的空点）；
/// 4) 悔棋按「一手」回退，页面调用两次即为撤销一个回合。
/// </summary>
public sealed class GoEngine
{
    public const int Size = 9;
    public const byte Empty = 0;
    public const byte Black = 1;
    public const byte White = 2;

    private byte[,] _cells = new byte[Size, Size];

    /// <summary>逐手记录局面字符串，用于打劫判定（不能立刻复现上一手之前的局面）。</summary>
    private readonly List<string> _positions = new();

    /// <summary>悔棋快照栈。</summary>
    private readonly Stack<GoSnapshot> _history = new();

    public GoEngine()
    {
        _positions.Add(PositionKey(_cells));
    }

    public byte Turn { get; private set; } = Black;

    /// <summary>黑方提掉的白子数。</summary>
    public int CapturedByBlack { get; private set; }

    /// <summary>白方提掉的黑子数。</summary>
    public int CapturedByWhite { get; private set; }

    public (int Row, int Col)? LastMove { get; private set; }

    /// <summary>连续「停一手」的次数，达到 2 次即终局。</summary>
    public int ConsecutivePasses { get; private set; }

    public bool Finished { get; private set; }

    public int MoveCount { get; private set; }

    public bool CanUndo => _history.Count > 0;

    public byte At(int row, int col) => _cells[row, col];

    public static byte Opponent(byte color) => color == Black ? White : Black;

    public static string ColorName(byte color) => color == Black ? "黑棋" : "白棋";

    /// <summary>落子。成功时自动换手。</summary>
    public GoMoveResult Play(int row, int col)
    {
        if (Finished) return GoMoveResult.Finished;
        if (row < 0 || row >= Size || col < 0 || col >= Size) return GoMoveResult.OutOfBoard;
        if (_cells[row, col] != Empty) return GoMoveResult.Occupied;

        var sim = Simulate(row, col, Turn);
        if (sim.Suicide) return GoMoveResult.Suicide;
        if (sim.Ko) return GoMoveResult.Ko;

        PushHistory();

        _cells[row, col] = Turn;
        RemoveDeadGroups(_cells, Opponent(Turn));

        if (Turn == Black) CapturedByBlack += sim.Captured;
        else CapturedByWhite += sim.Captured;

        LastMove = (row, col);
        ConsecutivePasses = 0;
        MoveCount++;
        _positions.Add(PositionKey(_cells));

        Turn = Opponent(Turn);
        return GoMoveResult.Ok;
    }

    /// <summary>停一手。连续两次停一手即终局。</summary>
    public void Pass()
    {
        if (Finished) return;

        PushHistory();
        ConsecutivePasses++;
        MoveCount++;
        LastMove = null;
        _positions.Add(PositionKey(_cells));
        if (ConsecutivePasses >= 2) Finished = true;
        else Turn = Opponent(Turn);
    }

    /// <summary>悔一手。返回是否真的退了一手。</summary>
    public bool Undo()
    {
        if (_history.Count == 0) return false;

        var s = _history.Pop();
        _cells = (byte[,])s.Cells.Clone();
        Turn = s.Turn;
        CapturedByBlack = s.CapturedByBlack;
        CapturedByWhite = s.CapturedByWhite;
        LastMove = s.LastMove;
        ConsecutivePasses = s.ConsecutivePasses;
        MoveCount = s.MoveCount;
        Finished = s.Finished;

        // 局面历史回退到对应长度（第一个元素是空盘，永远保留）
        while (_positions.Count > s.PositionCount) _positions.RemoveAt(_positions.Count - 1);
        return true;
    }

    /// <summary>判断某点是否可以落子（用于给小朋友提示，以及电脑选点）。</summary>
    public bool IsLegalMove(int row, int col, out int captured)
    {
        captured = 0;
        if (row < 0 || row >= Size || col < 0 || col >= Size) return false;
        if (_cells[row, col] != Empty) return false;
        var sim = Simulate(row, col, Turn);
        captured = sim.Captured;
        return !sim.Suicide && !sim.Ko;
    }

    /// <summary>电脑选点用的评估：返回该点是否合法、能提几子、落下后自己这块棋有几口气、能打吃几个对方块。</summary>
    internal SimResult Evaluate(int row, int col, byte color)
    {
        if (row < 0 || row >= Size || col < 0 || col >= Size || _cells[row, col] != Empty)
            return new SimResult { Suicide = true };
        return Simulate(row, col, color);
    }

    /// <summary>
    /// 中国规则数子：棋子数 + 只被单方围住的空点。
    /// 双方都挨着的空点（单官 / 双活）不算给任何一方。
    ///
    /// 注意一个边界情形：如果棋盘上**只有一方**有棋子，整块空域都只挨着这一色，
    /// 会被整体算作它的地盘（空盘下一手黑棋后点「数子」就是 81:0）。
    /// 这在数子规则上说得通——终局时对方没有活棋，空点归唯一有棋的一方；
    /// 但对孩子是误导，所以页面在双方各下过一手之前不允许结算（见 GoGamePage.OnCountScore）。
    /// 只要双方都落过子，大片空域就会同时邻接两色，不会再被判给任何一方。
    /// </summary>
    public (int Black, int White) AreaScore()
    {
        int black = 0, white = 0;
        var seen = new bool[Size, Size];

        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                if (_cells[r, c] == Black) black++;
                else if (_cells[r, c] == White) white++;
            }
        }

        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                if (_cells[r, c] != Empty || seen[r, c]) continue;

                // 洪水填充一块空点，同时看它挨着哪些颜色
                var region = new List<(int R, int C)>();
                bool nearBlack = false, nearWhite = false;
                var queue = new Queue<(int R, int C)>();
                queue.Enqueue((r, c));
                seen[r, c] = true;

                while (queue.Count > 0)
                {
                    var (cr, cc) = queue.Dequeue();
                    region.Add((cr, cc));
                    foreach (var (nr, nc) in Neighbors(cr, cc))
                    {
                        var v = _cells[nr, nc];
                        if (v == Black) nearBlack = true;
                        else if (v == White) nearWhite = true;
                        else if (!seen[nr, nc])
                        {
                            seen[nr, nc] = true;
                            queue.Enqueue((nr, nc));
                        }
                    }
                }

                if (nearBlack && !nearWhite) black += region.Count;
                else if (nearWhite && !nearBlack) white += region.Count;
            }
        }

        return (black, white);
    }

    // ── 内部实现 ──────────────────────────────────────────

    private void PushHistory()
    {
        _history.Push(new GoSnapshot
        {
            Cells = (byte[,])_cells.Clone(),
            Turn = Turn,
            CapturedByBlack = CapturedByBlack,
            CapturedByWhite = CapturedByWhite,
            LastMove = LastMove,
            ConsecutivePasses = ConsecutivePasses,
            MoveCount = MoveCount,
            Finished = Finished,
            PositionCount = _positions.Count
        });
    }

    /// <summary>在一份副本上试算这一手，用于自杀 / 打劫判定和 AI 评估。</summary>
    private SimResult Simulate(int row, int col, byte color)
    {
        var trial = (byte[,])_cells.Clone();
        trial[row, col] = color;

        var captured = RemoveDeadGroups(trial, Opponent(color));
        var liberties = CountLiberties(trial, row, col);
        if (liberties == 0) return new SimResult { Suicide = true };

        var key = PositionKey(trial);
        // 打劫：落子后不能复现「上一手之前」的局面
        var ko = _positions.Count >= 2 && _positions[^2] == key;

        return new SimResult
        {
            Captured = captured,
            OwnLiberties = liberties,
            OpponentAtari = CountGroupsWithOneLiberty(trial, Opponent(color)),
            Ko = ko
        };
    }

    private static int RemoveDeadGroups(byte[,] b, byte color)
    {
        int removed = 0;
        var visited = new bool[Size, Size];

        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                if (b[r, c] != color || visited[r, c]) continue;

                var group = CollectGroup(b, r, c, visited);
                if (CountLiberties(b, group) > 0) continue;

                foreach (var (gr, gc) in group)
                {
                    b[gr, gc] = Empty;
                    removed++;
                }
            }
        }

        return removed;
    }

    private static List<(int R, int C)> CollectGroup(byte[,] b, int row, int col, bool[,] visited)
    {
        var color = b[row, col];
        var group = new List<(int R, int C)>();
        var queue = new Queue<(int R, int C)>();
        queue.Enqueue((row, col));
        visited[row, col] = true;

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            group.Add((r, c));
            foreach (var (nr, nc) in Neighbors(r, c))
            {
                if (b[nr, nc] != color || visited[nr, nc]) continue;
                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }

        return group;
    }

    private static int CountLiberties(byte[,] b, int row, int col)
    {
        var color = b[row, col];
        var stones = CollectGroup(b, row, col, new bool[Size, Size]);
        return CountLiberties(b, stones);
    }

    private static int CountLiberties(byte[,] b, List<(int R, int C)> stones)
    {
        var seen = new bool[Size, Size];
        int libs = 0;
        foreach (var (r, c) in stones)
        {
            foreach (var (nr, nc) in Neighbors(r, c))
            {
                if (b[nr, nc] == Empty && !seen[nr, nc])
                {
                    seen[nr, nc] = true;
                    libs++;
                }
            }
        }
        return libs;
    }

    /// <summary>数出对方有多少块棋只剩一口气（用于 AI 判断「打吃」）。</summary>
    private static int CountGroupsWithOneLiberty(byte[,] b, byte color)
    {
        int count = 0;
        var visited = new bool[Size, Size];
        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                if (b[r, c] != color || visited[r, c]) continue;
                var group = CollectGroup(b, r, c, visited);
                if (CountLiberties(b, group) == 1) count++;
            }
        }
        return count;
    }

    private static IEnumerable<(int R, int C)> Neighbors(int r, int c)
    {
        if (r > 0) yield return (r - 1, c);
        if (r < Size - 1) yield return (r + 1, c);
        if (c > 0) yield return (r, c - 1);
        if (c < Size - 1) yield return (r, c + 1);
    }

    private static string PositionKey(byte[,] b)
    {
        var sb = new StringBuilder(Size * Size + Size);
        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++) sb.Append((char)('0' + b[r, c]));
            sb.Append('/');
        }
        return sb.ToString();
    }

    private readonly struct GoSnapshot
    {
        public byte[,] Cells { get; init; }
        public byte Turn { get; init; }
        public int CapturedByBlack { get; init; }
        public int CapturedByWhite { get; init; }
        public (int Row, int Col)? LastMove { get; init; }
        public int ConsecutivePasses { get; init; }
        public int MoveCount { get; init; }
        public bool Finished { get; init; }
        public int PositionCount { get; init; }
    }
}

/// <summary>试算结果。</summary>
internal struct SimResult
{
    public int Captured;
    public int OwnLiberties;
    public int OpponentAtari;
    public bool Suicide;
    public bool Ko;
}

/// <summary>
/// 简易围棋 AI。
///
/// 不做蒙特卡洛，也不用神经网络——对 9 路儿童对局来说，「会吃子、会躲打吃、会贴着对方下」
/// 已经足够像一个对手。评分项与权重都是显式的，便于按难度微调：
///   吃子（最重要） &gt; 打吃对方 &gt; 贴近对方 &gt; 连自己的棋 &gt; 靠近棋盘中部
/// 难度只影响随机扰动的幅度和是否愿意「冒险」。
/// </summary>
public static class GoAi
{
    public static (int Row, int Col)? ChooseMove(GoEngine engine, GameDifficulty difficulty)
    {
        var color = engine.Turn;

        // 空盘：直接占天元附近，别把第一手下在角上发呆
        if (engine.MoveCount == 0)
            return (GoEngine.Size / 2, GoEngine.Size / 2);

        var rng = new Random(Guid.NewGuid().GetHashCode());
        var best = new List<(int Row, int Col, double Score)>();
        (int Row, int Col)? fallback = null;

        for (var r = 0; r < GoEngine.Size; r++)
        {
            for (var c = 0; c < GoEngine.Size; c++)
            {
                if (engine.At(r, c) != GoEngine.Empty) continue;

                var sim = engine.Evaluate(r, c, color);
                if (sim.Suicide || sim.Ko) continue;
                fallback ??= (r, c);

                double score = 0;

                // 1) 能吃子就吃，吃得多更香
                score += sim.Captured * 60;
                if (sim.Captured > 0) score += 30;

                // 2) 打吃（把对方逼到只剩一口气）
                score += sim.OpponentAtari * 12;

                // 3) 自己落下后只剩一口气：危险，除非同时吃了子
                if (sim.OwnLiberties == 1 && sim.Captured == 0) score -= 55;
                else score += Math.Min(sim.OwnLiberties, 4) * 2;

                // 4) 贴着自己的棋连成片 / 贴着对方施加压力
                int own = 0, opp = 0;
                foreach (var (nr, nc) in Neighbors(r, c))
                {
                    var v = engine.At(nr, nc);
                    if (v == color) own++;
                    else if (v != GoEngine.Empty) opp++;
                }
                score += own * 5 + opp * 6;

                // 5) 略微偏好中部，避免全挤在边线上
                var center = (GoEngine.Size - 1) / 2.0;
                score += 6 - (Math.Abs(r - center) + Math.Abs(c - center));

                // 6) 随机扰动：难度越低越随性，看起来不那么「算无遗策」
                score += difficulty switch
                {
                    GameDifficulty.Easy => rng.NextDouble() * 55,
                    GameDifficulty.Medium => rng.NextDouble() * 16,
                    _ => rng.NextDouble() * 5
                };

                best.Add((r, c, score));
            }
        }

        if (best.Count == 0) return fallback;

        // 前几名里随机挑一个，避免每局都下成一模一样
        best.Sort((a, b) => b.Score.CompareTo(a.Score));
        var poolSize = difficulty == GameDifficulty.Easy ? Math.Min(6, best.Count)
                     : difficulty == GameDifficulty.Medium ? Math.Min(3, best.Count)
                     : 1;
        var chosen = best[rng.Next(poolSize)];
        return (chosen.Row, chosen.Col);
    }

    private static IEnumerable<(int R, int C)> Neighbors(int r, int c)
    {
        if (r > 0) yield return (r - 1, c);
        if (r < GoEngine.Size - 1) yield return (r + 1, c);
        if (c > 0) yield return (r, c - 1);
        if (c < GoEngine.Size - 1) yield return (r, c + 1);
    }
}
