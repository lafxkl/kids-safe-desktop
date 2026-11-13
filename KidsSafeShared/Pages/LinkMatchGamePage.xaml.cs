using KidsSafeDesktop.Converters;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Pages;

public partial class LinkMatchGamePage : ContentPage
{
    private readonly LinkMatchGameViewModel _vm;
    private readonly LinkPathDrawable _drawable = new();

    /// <summary>上一次排版用的格子边长，用于判断尺寸变化是否值得重建（避免抖动）。</summary>
    private double _lastCell;

    /// <summary>重建期间屏蔽 SizeChanged，否则设置 Board 尺寸会再次触发回调。</summary>
    private bool _building;

    public LinkMatchGamePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _vm = new LinkMatchGameViewModel();
        BindingContext = _vm;
        LinkCanvas.Drawable = _drawable;
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
        if (e.PropertyName == nameof(LinkMatchGameViewModel.PathPoints))
        {
            _drawable.Points = _vm.PathPoints;
            // 格子边长由本页按屏幕反算（见 ComputeCell），不取 ViewModel 里的固定值
            LinkCanvas.Invalidate();
        }
    }

    private void BuildBoard()
    {
        var cell = ComputeCell();
        var cols = _vm.Cols;
        var rows = _vm.Rows;

        _building = true;
        try
        {
            Board.WidthRequest = (cols + 2) * cell;
            Board.HeightRequest = (rows + 2) * cell;

            TileGrid.RowDefinitions.Clear();
            TileGrid.ColumnDefinitions.Clear();
            for (var i = 0; i < rows; i++)
            {
                // 写死行高列宽，避免 Auto 在容器有富余空间时把格子拉成非正方形
                TileGrid.RowDefinitions.Add(new RowDefinition(new GridLength(cell)));
                TileGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(cell)));
            }
            TileGrid.Margin = cell; // 内缩一个 cell，给外圈连线留空间
            TileGrid.Children.Clear();

            // 圆角与内边距随格子缩放，否则小屏上圆角会把图案吃掉
            var radius = Math.Max(8, cell * 0.22);
            var inner = Math.Max(3, cell * 0.07);

            foreach (var tile in _vm.Tiles)
            {
                var border = new Border
                {
                    Style = (Style)Resources["LinkTileStyle"],
                    WidthRequest = cell,
                    HeightRequest = cell,
                    StrokeShape = new RoundRectangle { CornerRadius = radius },
                    Padding = inner
                };
                border.SetBinding(Border.IsVisibleProperty, new Binding("Removed", converter: new InverseBoolConverter()));
                border.BindingContext = tile;

                var img = new Image
                {
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
                img.SetBinding(Image.SourceProperty, "Image");
                border.Content = img;

                TapGestureRecognizer tap = new() { NumberOfTapsRequired = 1 };
                tap.Tapped += async (s, e) => await _vm.OnTileTapped(tile);
                border.GestureRecognizers.Add(tap);

                Grid.SetRow(border, tile.Row);
                Grid.SetColumn(border, tile.Col);
                TileGrid.Children.Add(border);
            }

            _drawable.Points = null;
            _drawable.Cell = cell;
            _lastCell = cell;
            LinkCanvas.Invalidate();
        }
        finally
        {
            _building = false;
        }
    }

    /// <summary>
    /// 按当前可用空间反算格子边长。
    /// 棋盘含外圈留白，共 cols+2 列、rows+2 行，所以用扩展后的格数参与计算。
    /// </summary>
    private double ComputeCell()
    {
        double w = BoardHost.Width, h = BoardHost.Height;
        if (w <= 0 || h <= 0)
        {
            (w, h) = BoardMetrics.EstimateByScreen(widthPadding: 24, heightChrome: 220);
        }

        return BoardMetrics.Fit(w, h, _vm.Cols + 2, _vm.Rows + 2, minCell: 30, maxCell: 86);
    }

    /// <summary>
    /// 旋转屏幕、分屏、窗口缩放都会走到这里。变化小于 2px 不重建，避免抖动。
    /// </summary>
    private void OnBoardHostSizeChanged(object? sender, EventArgs e)
    {
        if (_building || BoardHost.Width <= 0 || BoardHost.Height <= 0) return;

        var cell = ComputeCell();
        if (Math.Abs(cell - _lastCell) < 2) return;

        BuildBoard();
    }

    private async void OnBack(object sender, EventArgs e)
        => await Navigation.PopAsync();

    private void OnHint(object sender, EventArgs e) => _vm.ShowHint();

    private void OnShuffle(object sender, EventArgs e) => _vm.Shuffle();

    private void OnReplay(object sender, EventArgs e)
    {
        _vm.Replay();
        BuildBoard();
    }
}

/// <summary>在扩展网格坐标中绘制连连看连接线。</summary>
public class LinkPathDrawable : IDrawable
{
    public List<(int r, int c)>? Points { get; set; }

    /// <summary>格子边长（与页面实际排版一致，随屏幕变化）。</summary>
    public double Cell { get; set; } = 62;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Points is null || Points.Count < 2) return;
        var cell = (float)Cell;
        canvas.StrokeColor = Colors.OrangeRed;
        canvas.StrokeSize = Math.Max(6f, cell * 0.16f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        for (var i = 0; i < Points.Count - 1; i++)
        {
            var a = Points[i];
            var b = Points[i + 1];
            var ax = a.c * cell + cell / 2f;
            var ay = a.r * cell + cell / 2f;
            var bx = b.c * cell + cell / 2f;
            var by = b.r * cell + cell / 2f;
            canvas.DrawLine(ax, ay, bx, by);
        }
    }
}
