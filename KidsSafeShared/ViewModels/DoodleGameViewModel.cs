using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>调色板里的一种颜色。</summary>
public class DoodleColorItem : BaseViewModel
{
    public string Hex { get; }
    public string Name { get; }
    public DoodleColorItem(string hex, string name)
    {
        Hex = hex;
        Name = name;
    }
}

/// <summary>
/// 快乐涂鸦：管理画笔颜色、笔刷粗细、橡皮开关等工具栏状态。
/// 实际笔画数据保存在 DoodleDrawable 中，由页面负责绘制与撤销/清空。
/// </summary>
public class DoodleGameViewModel : BaseViewModel
{
    public ObservableCollection<DoodleColorItem> Colors { get; } = new();
    public ObservableCollection<int> BrushSizes { get; } = new();

    private string _selectedColor = "#E53935";
    private int _selectedBrush = 10;
    private bool _isEraser;

    public string SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    public int SelectedBrush
    {
        get => _selectedBrush;
        set => SetProperty(ref _selectedBrush, value);
    }

    public bool IsEraser
    {
        get => _isEraser;
        set
        {
            if (SetProperty(ref _isEraser, value))
                OnPropertyChanged(nameof(NotEraser));
        }
    }

    public bool NotEraser => !_isEraser;

    public DoodleGameViewModel()
    {
        Colors.Add(new DoodleColorItem("#E53935", "红"));
        Colors.Add(new DoodleColorItem("#FB8C00", "橙"));
        Colors.Add(new DoodleColorItem("#FDD835", "黄"));
        Colors.Add(new DoodleColorItem("#43A047", "绿"));
        Colors.Add(new DoodleColorItem("#1E88E5", "蓝"));
        Colors.Add(new DoodleColorItem("#8E24AA", "紫"));
        Colors.Add(new DoodleColorItem("#EC407A", "粉"));
        Colors.Add(new DoodleColorItem("#6D4C41", "棕"));
        Colors.Add(new DoodleColorItem("#212121", "黑"));
        Colors.Add(new DoodleColorItem("#FFFFFF", "白"));

        BrushSizes.Add(6);
        BrushSizes.Add(12);
        BrushSizes.Add(22);
        BrushSizes.Add(36);

        SelectedBrush = 12;
    }

    public void SelectColor(string hex) => SelectedColor = hex;
    public void SelectBrush(int size) => SelectedBrush = size;
    public void ToggleEraser() => IsEraser = !IsEraser;
}
