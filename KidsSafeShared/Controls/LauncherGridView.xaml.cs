using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;

namespace KidsSafeDesktop.Controls;

/// <summary>
/// 统一的「手机桌面式」图标网格，两种布局模式自动切换：
///
/// ① 换行模式（Windows 桌面）：FlexLayout + BindableLayout，按宽度决定列数（4~8 列），
///    超出屏幕纵向滚动 —— 与改造前完全一致。
/// ② 分页模式（手机 Android/iOS）：每页放固定的「列 × 行」个图标，横向滑动翻页，
///    页面吸附 + 底部指示点，与系统桌面的操作习惯一致。
///
/// 模式判定只发生在构造函数里，WinUI 永远走 ①，因此不会重蹈
/// 「CollectionView + 动态 Span 在 WinUI 上 LayoutCycleException」的覆辙（详见 XAML 顶部注释）。
///
/// 尺寸算法统一在 <see cref="LauncherMetrics"/>，首页与各子页面共用同一份：
/// 「同宽度 ⇒ 同列数 ⇒ 同磁贴尺寸」，PC 上不会再出现两页图标不一样大。
/// </summary>
public partial class LauncherGridView : Grid
{
    /// <summary>名称胶囊 + 磁贴上下 Margin 的估算高度，用于推算「一屏能放几行」。</summary>
    private const double NameRowExtra = 46;

    /// <summary>底部指示点占用的高度（含上下 Margin），换算可用高度时要扣掉。</summary>
    private const double DotsHeight = 22;

    private readonly List<LauncherItem> _items = new();
    private readonly ObservableCollection<View> _pages = new();
    private readonly DataTemplate _tileTemplate;

    /// <summary>是否启用横向分页（仅移动端）。</summary>
    private readonly bool _pagedMode;

    private double _tileWidth;
    private double _pageWidth;
    private double _pageHeight;
    private int _capacity;
    private bool _rebuilding;
    private bool _rebuildQueued;
    private INotifyCollectionChanged? _observed;

    public LauncherGridView()
    {
        InitializeComponent();

        _tileTemplate = (DataTemplate)Resources["TileTemplate"];

        // 只认移动端：Windows 保持原有的换行 + 纵向滚动观感
        _pagedMode = DeviceInfo.Platform == DevicePlatform.Android
                     || DeviceInfo.Platform == DevicePlatform.iOS;

        WrapScroll.IsVisible = !_pagedMode;
        Pager.IsVisible = _pagedMode;

        // 换行模式下 BindableLayout 动态创建子项时，立刻套用当前目标尺寸
        WrapFlex.ChildAdded += OnTileChildAdded;

        if (_pagedMode)
        {
            Pager.ItemsSource = _pages;
            Pager.PositionChanged += OnPagePositionChanged;
        }
    }

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(LauncherGridView),
            propertyChanged: OnItemsSourceChanged);

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LauncherGridView g)
            g.SetItems((IEnumerable?)newValue);
    }

    /// <summary>图标被点击时触发，携带对应的 LauncherItem。</summary>
    public event EventHandler<LauncherItem>? ItemTapped;

    /// <summary>上一次点击时间，用于手势去重（见 OnTileTapped）。</summary>
    private DateTime _lastTapAt = DateTime.MinValue;

    private async void OnTileTapped(object sender, EventArgs e)
    {
        // 磁贴上的手势同时挂在了 Grid / Frame / Label 三处：
        // 安卓上子元素会消费触摸、只触发一次；但 Windows 上 Tapped 会从子元素冒泡到父级，
        // 一次点击可能触发两次（导致连推两个页面）。这里用 400ms 去重，两端都安全。
        var now = DateTime.UtcNow;
        if ((now - _lastTapAt).TotalMilliseconds < 400) return;
        _lastTapAt = now;

        if (sender is not View view || view.BindingContext is not LauncherItem item) return;

        // 等按下去的缩放画完再抛事件：接事件的一方通常会 new 一个页面（XAML 展开占满 UI 线程），
        // 若同时进行，缩放动画会卡在半途，看起来就是「点一下先顿住」。
        await view.PopAsync(60, 90);

        try
        {
            ItemTapped?.Invoke(this, item);
        }
        catch (Exception ex)
        {
            // 页面构造/导航失败时必须留下痕迹（此前静默吞掉，表现为「点了没反应」）
            _ = NavFx.ReportAsync("打不开", ex);
        }
    }

    // ── 数据 ────────────────────────────────────────────────────────────────

    private void SetItems(IEnumerable? source)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnItemsCollectionChanged;
            _observed = null;
        }

        _items.Clear();
        if (source is not null)
            foreach (var o in source)
                if (o is LauncherItem item) _items.Add(item);

        // 换行模式：直接把集合交给 BindableLayout，增删由它自己响应
        if (!_pagedMode)
        {
            BindableLayout.SetItemsSource(WrapFlex, source);
            return;
        }

        // 分页模式：页面按「每页容量」切块，集合变了要重新切
        if (source is INotifyCollectionChanged observable)
        {
            _observed = observable;
            observable.CollectionChanged += OnItemsCollectionChanged;
        }

        RebuildPages();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _items.Clear();
        if (ItemsSource is IEnumerable src)
            foreach (var o in src)
                if (o is LauncherItem item) _items.Add(item);

        // 首页的 BuildLauncher 是「先 Clear 再逐个 Add」，一次会触发 20 次事件。
        // 这里合并成一次重建，否则会连续建 20 遍页面（每个页面都要展开一遍磁贴模板）。
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        Dispatcher.Dispatch(() =>
        {
            _rebuildQueued = false;
            RebuildPages();
        });
    }

    // ── 分页 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 按当前尺寸重新切页。只在「换页容量或页宽变化」时调用，避免滑动过程中反复重建。
    /// </summary>
    private void RebuildPages()
    {
        if (_rebuilding) return;
        if (_pageWidth <= 0 || _capacity <= 0) return;   // 尚未完成首次布局

        _rebuilding = true;
        try
        {
            _pages.Clear();

            for (int start = 0; start < _items.Count; start += _capacity)
            {
                var chunk = _items.GetRange(start, Math.Min(_capacity, _items.Count - start));

                var page = new FlexLayout
                {
                    Wrap = FlexWrap.Wrap,
                    Direction = FlexDirection.Row,
                    JustifyContent = FlexJustify.Center,
                    AlignItems = FlexAlignItems.Start,
                    AlignContent = FlexAlignContent.Start,
                    WidthRequest = _pageWidth,
                    HeightRequest = _pageHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                };

                page.ChildAdded += OnTileChildAdded;
                BindableLayout.SetItemTemplate(page, _tileTemplate);
                BindableLayout.SetItemsSource(page, chunk);

                _pages.Add(page);
            }
        }
        finally
        {
            _rebuilding = false;
        }

        Dots.Count = _pages.Count;
        Dots.Position = 0;
        Dots.IsVisible = _pages.Count > 1;

        // 页面重建后回到第一页（孩子不会因为图标增减被甩到空页上）
        if (_pages.Count > 0) Pager.Position = 0;
    }

    private void OnPagePositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dots.Position = Math.Clamp(e.CurrentPosition, 0, Math.Max(0, _pages.Count - 1));
    }

    private void OnTileChildAdded(object? sender, ElementEventArgs e)
    {
        if (e.Element is View v) ApplyTileSize(v, _tileWidth);
    }

    /// <summary>
    /// 计算「每页几列几行、磁贴多大」。
    /// 列数沿用 LauncherMetrics（手机 4 列），行数由可用高度推出来（3~6 行），
    /// 若行高放不下，就把磁贴整体缩小 —— 保证一页正好一屏，孩子不会滑到一半卡住。
    /// </summary>
    private static (int Cols, int Rows, double Tile) ComputeGrid(double width, double height)
    {
        int cols = LauncherMetrics.SpanFor(width);
        double tile = LauncherMetrics.TileFor(width);

        int rows = (int)Math.Floor(height / (tile + NameRowExtra));
        rows = Math.Clamp(rows, 3, 6);

        double fit = Math.Floor(height / rows) - NameRowExtra;
        if (fit < tile) tile = Math.Max(LauncherMetrics.MinTile, fit);

        return (cols, rows, tile);
    }

    // ── 布局 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 按可用尺寸重算磁贴大小。注意：这里只改「子项尺寸」和分页结构，不会改变本控件
    /// 自身的分配尺寸（它由父级 Grid 的 * 行决定），因此不会再触发 OnSizeAllocated，
    /// 属于一次性收敛的布局，不存在反馈死循环。
    /// </summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (double.IsNaN(width) || width <= 0 || double.IsNaN(height) || height <= 0) return;

        double contentWidth = width - Padding.HorizontalThickness;
        double contentHeight = height - Padding.VerticalThickness;
        if (contentWidth <= 0 || contentHeight <= 0) return;

        // ① 换行模式：只调磁贴尺寸
        if (!_pagedMode)
        {
            double tile = LauncherMetrics.TileFor(contentWidth);
            if (Math.Abs(tile - _tileWidth) < 1) return;
            _tileWidth = tile;

            foreach (var child in WrapFlex.Children)
                if (child is View v) ApplyTileSize(v, _tileWidth);
            return;
        }

        // ② 分页模式：一页一屏，先算容量再决定要不要重建页面
        double pageHeight = Math.Max(0, contentHeight - DotsHeight);
        var (cols, rows, pageTile) = ComputeGrid(contentWidth, pageHeight);

        bool changed = Math.Abs(pageTile - _tileWidth) >= 1
                       || Math.Abs(contentWidth - _pageWidth) >= 1
                       || Math.Abs(pageHeight - _pageHeight) >= 1;

        _tileWidth = pageTile;
        _pageWidth = contentWidth;
        _pageHeight = pageHeight;
        _capacity = cols * rows;

        int expected = _items.Count == 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(_items.Count / (double)_capacity));

        if (changed || _pages.Count != expected)
            RebuildPages();
    }

    /// <summary>
    /// 把一个磁贴（根 Grid + 内部 Frame/图标/名称）整体缩放到目标宽度。
    /// 关键：内部 Frame 与图标必须一起缩放，否则 Frame 的固定宽度会把磁贴撑大，
    /// 导致「算出来 4 列、实际只排得下 3 列」（安卓手机上就是这样挤成 3 列的）。
    /// </summary>
    private static void ApplyTileSize(View tile, double size)
    {
        if (size <= 0) return;

        if (tile is not Grid root) return;
        root.WidthRequest = size;
        // 行高锁定成磁贴尺寸：单元格被拉高时图标也不会被拉成竖长条
        if (root.RowDefinitions.Count > 0)
            root.RowDefinitions[0].Height = new GridLength(size);

        if (root.Children.Count > 0 && root.Children[0] is Frame frame)
        {
            frame.WidthRequest = size;
            frame.HeightRequest = size;
            // 33% 圆角 = 现代「squircle」观感（与首页 HomePage 保持一致）
            frame.CornerRadius = (float)Math.Round(size * 0.33);

            if (frame.Content is Grid inner)
            {
                double iconSize = Math.Round(size * 0.62);
                double emojiSize = Math.Round(size * 0.52);
                foreach (var e in inner.Children)
                {
                    switch (e)
                    {
                        case Image img:
                            img.WidthRequest = iconSize;
                            img.HeightRequest = iconSize;
                            break;
                        case Label lb:
                            lb.FontSize = emojiSize;
                            break;
                    }
                }
            }
        }

        // 名称现在包在圆角 Border（胶囊底）里，需要取到里面的 Label 再调字号
        if (root.Children.Count > 1)
        {
            var nameHost = root.Children[1] as Border;
            Label? name = root.Children[1] as Label ?? nameHost?.Content as Label;
            if (name is not null)
                name.FontSize = Math.Max(10.5, Math.Round(size * 0.145));
        }
    }
}
