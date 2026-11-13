using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 数独小游戏：4×4 版本（数字 1-4，2×2 宫），适合小朋友。
/// 回溯随机生成完整解 → 按难度挖空 → 选中格子用数字键盘填入 → 自动校验 → 全部填对即胜利。
/// </summary>
public class SudokuGameViewModel : BaseViewModel
{
    private const int Size = 4;
    private readonly System.Random _rnd = new();
    private readonly int[,] _solution = new int[Size, Size];
    private readonly int?[,] _board = new int?[Size, Size];
    private readonly bool[,] _given = new bool[Size, Size];
    private SudokuCell? _selected;
    private GameDifficulty _difficulty = GameDifficulty.Medium;
    private int _filled;
    private int _moves;

    private string _difficultyLabel = "中等";
    private string _progressText = string.Empty;
    private string _statusText = string.Empty;
    private bool _finished;
    private string _resultText = string.Empty;

    public ObservableCollection<SudokuCell> Cells { get; } = new();

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

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
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

    public SudokuGameViewModel()
    {
        // 预创建 16 个单元格（含 2×2 宫着色）
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
            {
                var tint = ((r / 2) + (c / 2)) % 2 == 0 ? "#ECEFF1" : "#E8EAF6";
                Cells.Add(new SudokuCell(r, c, tint));
            }
    }

    public void Start()
    {
        _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        DifficultyLabel = _difficulty.Label();

        GenerateSolved();
        var blanks = _difficulty switch
        {
            GameDifficulty.Easy => 6,
            GameDifficulty.Medium => 9,
            _ => 12
        };
        DigHoles(blanks);

        _filled = 0;
        _moves = 0;
        _selected = null;
        Finished = false;
        SyncCells();
        UpdateProgress();
        StatusText = "点一个格子，再选下面的数字填进去吧！";
    }

    /// <summary>回溯法随机生成一个合法的 4×4 数独完整解。</summary>
    private void GenerateSolved()
    {
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
                _solution[r, c] = 0;
        Solve(0, 0);
    }

    private bool Solve(int r, int c)
    {
        if (r == Size) return true;
        var nr = c == Size - 1 ? r + 1 : r;
        var nc = c == Size - 1 ? 0 : c + 1;
        if (_solution[r, c] != 0)
            return Solve(nr, nc);

        var nums = Shuffle(new[] { 1, 2, 3, 4 });
        foreach (var v in nums)
        {
            if (IsValid(r, c, v))
            {
                _solution[r, c] = v;
                if (Solve(nr, nc)) return true;
                _solution[r, c] = 0;
            }
        }
        return false;
    }

    private bool IsValid(int r, int c, int v)
    {
        for (var i = 0; i < Size; i++)
        {
            if (_solution[r, i] == v) return false;
            if (_solution[i, c] == v) return false;
        }
        var br = (r / 2) * 2;
        var bc = (c / 2) * 2;
        for (var i = 0; i < 2; i++)
            for (var j = 0; j < 2; j++)
                if (_solution[br + i, bc + j] == v) return false;
        return true;
    }

    private void DigHoles(int count)
    {
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
            {
                _board[r, c] = _solution[r, c];
                _given[r, c] = true;
            }
        var positions = new List<(int, int)>();
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
                positions.Add((r, c));
        positions = Shuffle(positions);
        for (var i = 0; i < count && i < positions.Count; i++)
        {
            var (r, c) = positions[i];
            _board[r, c] = null;
            _given[r, c] = false;
        }
    }

    private void SyncCells()
    {
        foreach (var cell in Cells)
        {
            var v = _board[cell.Row, cell.Col];
            cell.ValueText = v.HasValue ? v.Value.ToString() : string.Empty;
            cell.IsGiven = _given[cell.Row, cell.Col];
            cell.IsSelected = _selected == cell;
            cell.IsWrong = v.HasValue && !_given[cell.Row, cell.Col] && v.Value != _solution[cell.Row, cell.Col];
        }
    }

    public void SelectCell(SudokuCell? cell)
    {
        if (cell is null) return;
        if (_given[cell.Row, cell.Col]) // 已给数字不可改
        {
            _selected = cell;
            SyncCells();
            StatusText = "这一格是提示数字，不能改哦～";
            return;
        }
        _selected = cell;
        SyncCells();
        StatusText = "选好了！点下面的数字填进去吧～";
    }

    public void Input(int n)
    {
        if (_selected is null || Finished) return;
        if (_given[_selected.Row, _selected.Col]) return;

        if (_board[_selected.Row, _selected.Col] != n)
            _moves++;

        _board[_selected.Row, _selected.Col] = n;
        if (n == _solution[_selected.Row, _selected.Col])
            _filled = CountFilled();
        else
            _filled = CountFilled();

        SyncCells();
        UpdateProgress();

        if (IsSolved())
        {
            Finished = true;
            var best = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
            {
                Game = GameType.Sudoku,
                Difficulty = _difficulty,
                Score = 100 * _difficulty.Factor(),
                MaxScore = 100 * _difficulty.Factor(),
                Date = System.DateTime.Now,
                Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
            });
            ResultText = best
                ? "🎉 新纪录！你完成了数独，太聪明啦！"
                : "🎉 你完成数独啦！每行每列都不重复，真厉害！";
            ServiceProvider.Get<ISoundService>().PlayWin();
        }
    }

    public void Erase()
    {
        if (_selected is null || Finished) return;
        if (_given[_selected.Row, _selected.Col]) return;
        _board[_selected.Row, _selected.Col] = null;
        _filled = CountFilled();
        SyncCells();
        UpdateProgress();
    }

    private int CountFilled()
    {
        var n = 0;
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
                if (_board[r, c].HasValue) n++;
        return n;
    }

    private bool IsSolved()
    {
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
            {
                var v = _board[r, c];
                if (!v.HasValue || v.Value != _solution[r, c]) return false;
            }
        return true;
    }

    private void UpdateProgress() => ProgressText = $"已填 {_filled} / {Size * Size}";

    private List<T> Shuffle<T>(IEnumerable<T> source)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rnd.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
