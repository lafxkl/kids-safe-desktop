using System.ComponentModel;

namespace KidsSafeDesktop.ViewModels;

/// <summary>数独棋盘上的一个格子（用于 UI 绑定）。</summary>
public class SudokuCell : BaseViewModel
{
    public int Row { get; }
    public int Col { get; }

    private string _valueText = string.Empty;
    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }

    private bool _isGiven;
    public bool IsGiven
    {
        get => _isGiven;
        set => SetProperty(ref _isGiven, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isWrong;
    public bool IsWrong
    {
        get => _isWrong;
        set => SetProperty(ref _isWrong, value);
    }

    /// <summary>所在 2×2 宫的着色（用于视觉分组）。</summary>
    public string BoxTint { get; }

    public SudokuCell(int row, int col, string boxTint)
    {
        Row = row;
        Col = col;
        BoxTint = boxTint;
    }
}
