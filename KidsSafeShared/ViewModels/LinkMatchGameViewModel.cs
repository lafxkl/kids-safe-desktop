using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 连连看（Lianliankan）：在网格中找出两张相同图案的方块，
/// 若能用不超过两个拐弯的连线（可绕到棋盘外围）把它们连起来，则消除。
/// 全部消除即获胜。
/// </summary>
public class LinkTile : BaseViewModel
{
    public int Row { get; init; }   // 内网格行
    public int Col { get; init; }   // 内网格列
    public int ERow { get; init; }  // 扩展网格行（含外圈边框）
    public int ECol { get; init; }  // 扩展网格列

    private int _typeId;
    public int TypeId
    {
        get => _typeId;
        set => SetProperty(ref _typeId, value);
    }

    private string _image = string.Empty;
    public string Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    private bool _removed;
    public bool Removed
    {
        get => _removed;
        set => SetProperty(ref _removed, value);
    }

    private bool _hint;
    public bool Hint
    {
        get => _hint;
        set => SetProperty(ref _hint, value);
    }
}

public class LinkMatchGameViewModel : BaseViewModel
{
    private static readonly string[] TileImages =
    {
        "ll_butterfly.png", "ll_bee.png", "ll_ladybug.png", "ll_snail.png",
        "ll_caterpillar.png", "ll_spider.png", "ll_dragonfly.png", "ll_beetle.png"
    };

    private readonly System.Random _rnd = new();
    private GameDifficulty _difficulty = GameDifficulty.Medium;
    private int _rows, _cols, _cell;
    private int[][] _grid = System.Array.Empty<int[]>();
    private LinkTile? _first;
    private int _removedPairs, _totalPairs;
    private bool _finished, _busy;
    private string _progressText = string.Empty, _difficultyLabel = "中等", _resultText = string.Empty;

    public ObservableCollection<LinkTile> Tiles { get; } = new();

    public int Rows => _rows;
    public int Cols => _cols;
    public int CellSize => _cell;

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

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

    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    /// <summary>当前需要画出的连接线（扩展坐标，像素由页面换算），无则 null。</summary>
    private List<(int r, int c)>? _pathPoints;
    public List<(int r, int c)>? PathPoints
    {
        get => _pathPoints;
        set => SetProperty(ref _pathPoints, value);
    }

    public void Start()
    {
        _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        DifficultyLabel = _difficulty.Label();

        (_rows, _cols, _cell) = _difficulty switch
        {
            GameDifficulty.Easy => (4, 4, 74),
            GameDifficulty.Medium => (5, 6, 62),
            GameDifficulty.Hard => (6, 8, 52),
            _ => (5, 6, 62)
        };

        BuildBoard();
        _first = null;
        _busy = false;
        _removedPairs = 0;
        Finished = false;
        UpdateProgress();
    }

    private void BuildBoard()
    {
        var er = _rows + 2;
        var ec = _cols + 2;
        _grid = new int[er][];
        for (var r = 0; r < er; r++)
        {
            _grid[r] = new int[ec];
            for (var c = 0; c < ec; c++)
                _grid[r][c] = -1; // -1 表示空（含外圈边框）
        }

        // 生成成对图案
        _totalPairs = (_rows * _cols) / 2;
        var types = new List<int>();
        for (var i = 0; i < _totalPairs; i++)
        {
            var t = i % TileImages.Length;
            types.Add(t);
            types.Add(t);
        }
        // 洗牌
        for (var i = types.Count - 1; i > 0; i--)
        {
            var j = _rnd.Next(i + 1);
            (types[i], types[j]) = (types[j], types[i]);
        }

        Tiles.Clear();
        var k = 0;
        for (var r = 1; r <= _rows; r++)
        {
            for (var c = 1; c <= _cols; c++)
            {
                var t = types[k++];
                _grid[r][c] = t;
                Tiles.Add(new LinkTile
                {
                    Row = r - 1,
                    Col = c - 1,
                    ERow = r,
                    ECol = c,
                    TypeId = t,
                    Image = TileImages[t]
                });
            }
        }
    }

    public async Task OnTileTapped(LinkTile tile)
    {
        if (_busy || Finished || tile.Removed) return;
        var sound = ServiceProvider.Get<ISoundService>();

        if (_first is null)
        {
            _first = tile;
            tile.Selected = true;
            sound.PlayClick();
            return;
        }

        if (_first == tile)
        {
            tile.Selected = false;
            _first = null;
            return;
        }

        if (_first.TypeId != tile.TypeId)
        {
            // 不同图案：切换选中
            _first.Selected = false;
            _first = tile;
            tile.Selected = true;
            sound.PlayClick();
            return;
        }

        // 同图案：尝试连线
        var path = FindPath(_first, tile);
        if (path is null)
        {
            _first.Selected = false;
            _first = tile;
            tile.Selected = true;
            sound.PlayClick();
            return;
        }

        // 连接成功
        _busy = true;
        sound.PlayCorrect();
        PathPoints = path;
        _grid[_first.ERow][_first.ECol] = -1;
        _grid[tile.ERow][tile.ECol] = -1;
        _first.Selected = false;
        _first.Removed = true;
        tile.Removed = true;
        _first = null;
        _removedPairs++;
        UpdateProgress();

        await Task.Delay(260);
        PathPoints = null;
        _busy = false;

        if (_removedPairs >= _totalPairs)
        {
            Finished = true;
            var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.LinkMatch,
                Difficulty = _difficulty,
                Score = _totalPairs,
                MaxScore = _totalPairs,
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = isBest
                ? "🎉 新纪录！全部连光啦！"
                : "全部连光啦，你真棒！🎉";
            sound.PlayWin();
        }
        else if (!AnyMoveExists())
        {
            Shuffle();
        }
    }

    public void ShowHint()
    {
        if (Finished) return;
        ClearHint();
        foreach (var a in Tiles.Where(t => !t.Removed))
        {
            foreach (var b in Tiles.Where(t => !t.Removed && t != a && t.TypeId == a.TypeId))
            {
                if (FindPath(a, b) is not null)
                {
                    a.Hint = true;
                    b.Hint = true;
                    return;
                }
            }
        }
    }

    private void ClearHint()
    {
        foreach (var t in Tiles)
            t.Hint = false;
    }

    public void Shuffle()
    {
        if (Finished) return;
        var remaining = Tiles.Where(t => !t.Removed).ToList();
        var types = remaining.Select(t => t.TypeId).OrderBy(_ => _rnd.Next()).ToList();
        for (var i = 0; i < remaining.Count; i++)
        {
            var t = remaining[i];
            t.TypeId = types[i];
            t.Image = TileImages[types[i]];
            _grid[t.ERow][t.ECol] = types[i];
        }
        _first = null;
        ClearHint();
        // 若洗牌后仍无解，再洗一次（最多若干次）
        var tries = 0;
        while (!AnyMoveExists() && tries++ < 10)
        {
            types = types.OrderBy(_ => _rnd.Next()).ToList();
            for (var i = 0; i < remaining.Count; i++)
            {
                var t = remaining[i];
                t.TypeId = types[i];
                t.Image = TileImages[types[i]];
                _grid[t.ERow][t.ECol] = types[i];
            }
        }
    }

    private bool AnyMoveExists()
    {
        var live = Tiles.Where(t => !t.Removed).ToList();
        for (var i = 0; i < live.Count; i++)
            for (var j = i + 1; j < live.Count; j++)
                if (live[i].TypeId == live[j].TypeId && FindPath(live[i], live[j]) is not null)
                    return true;
        return false;
    }

    /// <summary>
    /// 广度优先搜索：在扩展网格中找一条从 a 到 b、拐弯不超过 2 次的路径，
    /// 路径只能经过空格（含外圈边框）或终点 b。返回扩展坐标路径点或 null。
    /// </summary>
    private List<(int r, int c)>? FindPath(LinkTile a, LinkTile b)
    {
        var R = _rows + 2;
        var C = _cols + 2;
        var dr = new[] { -1, 1, 0, 0 };
        var dc = new[] { 0, 0, -1, 1 };

        var startKey = (a.ERow, a.ECol);
        // visited[state] -> parent (r,c,dir,turns)
        var visited = new System.Collections.Generic.HashSet<(int r, int c, int d, int t)>();
        var parent = new System.Collections.Generic.Dictionary<(int r, int c, int d, int t), (int r, int c, int d, int t)?>();
        var queue = new System.Collections.Generic.Queue<(int r, int c, int d, int t)>();
        var startStates = new List<(int r, int c, int d, int t)>();

        for (var d = 0; d < 4; d++)
        {
            var st = (a.ERow, a.ECol, d, 0);
            visited.Add(st);
            parent[st] = null;
            queue.Enqueue(st);
            startStates.Add(st);
        }

        (int r, int c, int d, int t)? goal = null;
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur.r == b.ERow && cur.c == b.ECol)
            {
                goal = cur;
                break;
            }
            for (var nd = 0; nd < 4; nd++)
            {
                var nr = cur.r + dr[nd];
                var nc = cur.c + dc[nd];
                if (nr < 0 || nc < 0 || nr >= R || nc >= C) continue;
                var nt = cur.t + (nd == cur.d ? 0 : 1);
                if (nt > 2) continue;

                var isTarget = (nr == b.ERow && nc == b.ECol);
                if (!isTarget && _grid[nr][nc] != -1) continue; // 被其他方块挡住

                var nst = (nr, nc, nd, nt);
                if (visited.Contains(nst)) continue;
                visited.Add(nst);
                parent[nst] = cur;
                queue.Enqueue(nst);
            }
        }

        if (goal is null) return null;

        // 回溯路径
        var cells = new System.Collections.Generic.List<(int r, int c)>();
        var step = goal;
        while (step is not null)
        {
            cells.Add((step.Value.r, step.Value.c));
            step = parent[step.Value];
        }
        cells.Reverse();

        // 压缩为拐点（去掉共线的中间点）
        var way = new System.Collections.Generic.List<(int r, int c)> { cells[0] };
        for (var i = 1; i < cells.Count - 1; i++)
        {
            var prev = way[^1];
            var cur = cells[i];
            var nxt = cells[i + 1];
            var d1r = System.Math.Sign(cur.r - prev.r);
            var d1c = System.Math.Sign(cur.c - prev.c);
            var d2r = System.Math.Sign(nxt.r - cur.r);
            var d2c = System.Math.Sign(nxt.c - cur.c);
            if (d1r != d2r || d1c != d2c)
                way.Add(cur);
        }
        way.Add(cells[^1]);
        return way;
    }

    public void Replay() => Start();

    private void UpdateProgress() => ProgressText = $"已消除 {_removedPairs} / {_totalPairs} 对";
}
