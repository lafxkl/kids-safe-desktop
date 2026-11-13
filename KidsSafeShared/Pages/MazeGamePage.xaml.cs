using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace KidsSafeDesktop.Pages;

public partial class MazeGamePage : ContentPage
{
    private readonly MazeGameViewModel _vm;
    private Image? _panda;

    /// <summary>上一次排版用的格子边长，用于判断尺寸变化是否值得重建。</summary>
    private double _lastCell;

    /// <summary>重建期间屏蔽 SizeChanged，避免设置尺寸时再次触发回调。</summary>
    private bool _building;

    public MazeGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new MazeGameViewModel();
        BindingContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Start();
        BuildBoard();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(MazeGameViewModel.PlayerRow) ||
             e.PropertyName == nameof(MazeGameViewModel.PlayerCol)) && _panda is not null)
        {
            Grid.SetRow(_panda, _vm.PlayerRow);
            Grid.SetColumn(_panda, _vm.PlayerCol);
        }
    }

    /// <summary>
    /// 按当前可用空间反算格子边长。
    /// 原先是写死的 <c>min(34, 480/dim)</c>，在 1080×2340 这类手机上刚好顶到屏幕两边，
    /// 小屏则会超出右边界。改成按实际布局尺寸反算，并留 8px 呼吸空间。
    /// </summary>
    private double ComputeCell()
    {
        var dim = _vm.Dim;
        double w = MazeHost.Width, h = MazeHost.Height;
        if (w <= 0 || h <= 0)
        {
            (w, h) = BoardMetrics.EstimateByScreen(widthPadding: 36, heightChrome: 230);
        }

        return BoardMetrics.Fit(w - 8, h - 8, dim, dim, minCell: 14, maxCell: 46);
    }

    private void OnMazeHostSizeChanged(object? sender, EventArgs e)
    {
        if (_building || MazeHost.Width <= 0 || MazeHost.Height <= 0) return;

        var cell = ComputeCell();
        if (Math.Abs(cell - _lastCell) < 1.5) return;

        BuildBoard();
    }

    private void BuildBoard()
    {
        var dim = _vm.Dim;
        var cell = ComputeCell();

        _building = true;
        try
        {
            MazeGrid.RowDefinitions.Clear();
            MazeGrid.ColumnDefinitions.Clear();
            for (var i = 0; i < dim; i++)
            {
                // 行高/列宽写死为 cell，不用 Auto —— Auto 在上层容器给足空间时会被拉长，
                // 迷宫会变成「高比宽大 1.47 倍」的长方形（真机上就是这个现象）。
                MazeGrid.RowDefinitions.Add(new RowDefinition(new GridLength(cell)));
                MazeGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(cell)));
            }
            MazeGrid.Children.Clear();
            MazeGrid.WidthRequest = dim * cell;
            MazeGrid.HeightRequest = dim * cell;

            // 通道圆角随格子缩放，格子小时固定 4 会显得过圆
            var radius = Math.Max(2, cell * 0.12);

            for (var r = 0; r < dim; r++)
            {
                for (var c = 0; c < dim; c++)
                {
                    if (_vm.IsPassage(r, c))
                    {
                        var cellBox = new Border
                        {
                            WidthRequest = cell,
                            HeightRequest = cell,
                            BackgroundColor = Color.FromArgb("#FFF8E1"),
                            StrokeShape = new RoundRectangle { CornerRadius = radius },
                            StrokeThickness = 0,
                            Padding = 0
                        };
                        var rr = r; var cc = c;
                        TapGestureRecognizer tap = new() { NumberOfTapsRequired = 1 };
                        tap.Tapped += (s, e) => _vm.TryMove(rr, cc);
                        cellBox.GestureRecognizers.Add(tap);
                        Grid.SetRow(cellBox, r);
                        Grid.SetColumn(cellBox, c);
                        MazeGrid.Children.Add(cellBox);
                    }
                    else
                    {
                        var wall = new BoxView
                        {
                            WidthRequest = cell,
                            HeightRequest = cell,
                            Color = Color.FromArgb("#6D4C41")
                        };
                        Grid.SetRow(wall, r);
                        Grid.SetColumn(wall, c);
                        MazeGrid.Children.Add(wall);
                    }
                }
            }

            // 起点标记
            var start = new Image
            {
                Source = "maze_start.png",
                WidthRequest = cell * 0.78,
                HeightRequest = cell * 0.78,
                InputTransparent = true,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetRow(start, _vm.StartPos.r);
            Grid.SetColumn(start, _vm.StartPos.c);
            MazeGrid.Children.Add(start);

            // 终点标记
            var end = new Image
            {
                Source = "maze_end.png",
                WidthRequest = cell * 0.78,
                HeightRequest = cell * 0.78,
                InputTransparent = true,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetRow(end, _vm.End.r);
            Grid.SetColumn(end, _vm.End.c);
            MazeGrid.Children.Add(end);

            // 熊猫角色
            _panda = new Image
            {
                Source = "maze_panda.png",
                WidthRequest = cell * 0.96,
                HeightRequest = cell * 0.96,
                InputTransparent = true,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetRow(_panda, _vm.PlayerRow);
            Grid.SetColumn(_panda, _vm.PlayerCol);
            MazeGrid.Children.Add(_panda);

            _lastCell = cell;
        }
        finally
        {
            _building = false;
        }
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    private void OnReplay(object sender, EventArgs e)
    {
        _vm.Replay();
        BuildBoard();
    }
}
