using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GodjiVpn.Controls;

/// <summary>
/// Список без прокрутки: показывает столько строк, сколько помещается в отведённую высоту,
/// остальное — на следующих страницах ("‹ 1 / 3 ›" под списком, колесо мыши листает).
/// Сколько строк на странице, считается заново при каждом изменении размера окна, шрифта
/// или содержимого. Высоту списку должен задать контейнер (строка Grid со звёздочкой):
/// при бесконечной высоте всё помещается на одну страницу.
/// </summary>
public sealed class PagedItems : ItemsControl
{
    public static readonly DependencyProperty PageIndexProperty = DependencyProperty.Register(
        nameof(PageIndex), typeof(int), typeof(PagedItems),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((PagedItems)d).OnPageIndexChanged()));

    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(
        nameof(PageCount), typeof(int), typeof(PagedItems),
        new FrameworkPropertyMetadata(1, (d, _) => ((PagedItems)d).UpdatePagerText()));

    private static readonly DependencyPropertyKey PagerTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PagerText), typeof(string), typeof(PagedItems), new PropertyMetadata(""));
    public static readonly DependencyProperty PagerTextProperty = PagerTextPropertyKey.DependencyProperty;

    /// <summary>Число колонок сетки (макет 9a «Серверы»: 2 колонки × строки); 1 — обычный список.</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(PagedItems), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public int PageIndex { get => (int)GetValue(PageIndexProperty); set => SetValue(PageIndexProperty, value); }
    public int PageCount { get => (int)GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }
    public string PagerText => (string)GetValue(PagerTextProperty);

    private static readonly DependencyPropertyKey HasPagesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasPages), typeof(bool), typeof(PagedItems), new PropertyMetadata(false));
    public static readonly DependencyProperty HasPagesProperty = HasPagesPropertyKey.DependencyProperty;
    /// <summary>Страниц больше одной — показывать переключатель.</summary>
    public bool HasPages => (bool)GetValue(HasPagesProperty);

    public RoutedUICommand PrevCommand { get; } = new();
    public RoutedUICommand NextCommand { get; } = new();

    public PagedItems()
    {
        ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(PagedPanel)));
        CommandBindings.Add(new CommandBinding(PrevCommand, (_, _) => PageIndex--, (_, e) => e.CanExecute = PageIndex > 0));
        CommandBindings.Add(new CommandBinding(NextCommand, (_, _) => PageIndex++, (_, e) => e.CanExecute = PageIndex < PageCount - 1));
        UpdatePagerText();
    }

    /// <summary>Для экранных дикторов и UIAutomation видны и строки, и кнопки страниц
    /// (стандартный пир ItemsControl показывает только строки).</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);

    /// <summary>Колесо мыши листает страницы, раз прокрутки нет.</summary>
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (PageCount <= 1) return;
        var next = PageIndex + (e.Delta < 0 ? 1 : -1);
        if (next >= 0 && next < PageCount) { PageIndex = next; e.Handled = true; }
    }

    /// <summary>Новый набор строк (поиск, сортировка, удаление) — снова с первой страницы.</summary>
    protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        PageIndex = 0;
    }

    /// <summary>Число страниц сообщает панель во время измерения. Применяем его отложенно:
    /// переключатель страниц появляется/исчезает в разметке самого PagedItems, а запрос на
    /// перекомпоновку предка, который как раз измеряется, WPF теряет.</summary>
    internal void ReportPages(int count)
    {
        if (count == PageCount && PageIndex <= count - 1 && HasPages == count > 1) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (PageCount != count) PageCount = count;
            SetValue(HasPagesPropertyKey, count > 1);
            if (PageIndex > count - 1) PageIndex = Math.Max(0, count - 1);
            CommandManager.InvalidateRequerySuggested();
        });
    }

    /// <summary>Панель строк (регистрируется сама при первом измерении).</summary>
    internal PagedPanel? Panel { get; set; }

    /// <summary>Другая страница: размер панели не меняется, поэтому WPF сам её не перемерит.</summary>
    private void OnPageIndexChanged()
    {
        UpdatePagerText();
        Panel?.InvalidateMeasure();
        Panel?.InvalidateArrange();
    }

    private void UpdatePagerText() =>
        SetValue(PagerTextPropertyKey, $"СТРАНИЦА {Math.Min(PageIndex + 1, PageCount)} ИЗ {PageCount}");
}

/// <summary>
/// Панель PagedItems: раскладывает строки текущей страницы стопкой, остальные уводит за
/// границу (панель обрезает содержимое). Первой строке каждой страницы ставит IsPageStart —
/// шаблон строки по нему прячет разделитель сверху.
/// </summary>
public sealed class PagedPanel : Panel
{
    public static readonly DependencyProperty IsPageStartProperty = DependencyProperty.RegisterAttached(
        "IsPageStart", typeof(bool), typeof(PagedPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsPageStart(DependencyObject d) => (bool)d.GetValue(IsPageStartProperty);
    public static void SetIsPageStart(DependencyObject d, bool value) => d.SetValue(IsPageStartProperty, value);

    private readonly List<int> _pageStarts = new() { 0 };

    public PagedPanel() => ClipToBounds = true;

    private PagedItems? _owner;

    /// <summary>Свой PagedItems — по визуальному дереву (GetItemsOwner у панели из шаблона
    /// ItemsPanel срабатывает не всегда).</summary>
    private PagedItems? Owner
    {
        get
        {
            if (_owner != null) return _owner;
            DependencyObject? d = this;
            while (d != null && d is not PagedItems) d = VisualTreeHelper.GetParent(d);
            _owner = d as PagedItems ?? ItemsControl.GetItemsOwner(this) as PagedItems;
            if (_owner != null) _owner.Panel = this;
            return _owner;
        }
    }

    private readonly List<double> _rowHeights = new();

    protected override Size MeasureOverride(Size available)
    {
        var width = double.IsInfinity(available.Width) ? double.PositiveInfinity : available.Width;
        var cols = Math.Max(1, Owner?.Columns ?? 1);
        var cellW = double.IsInfinity(width) ? width : width / cols;
        _pageStarts.Clear();
        _pageStarts.Add(0);
        _rowHeights.Clear();
        double used = 0;
        var n = InternalChildren.Count;
        for (var r = 0; r * cols < n; r++)
        {
            double rowH = 0;
            for (var c = 0; c < cols && r * cols + c < n; c++)
            {
                var child = InternalChildren[r * cols + c];
                child.Measure(new Size(cellW, double.PositiveInfinity));
                rowH = Math.Max(rowH, child.DesiredSize.Height);
            }
            _rowHeights.Add(rowH);
            if (used > 0 && used + rowH > available.Height)
            {
                _pageStarts.Add(r * cols);
                used = 0;
            }
            used += rowH;
        }
        var owner = Owner;
        var page = Math.Clamp(owner?.PageIndex ?? 0, 0, _pageStarts.Count - 1);
        // Предварительные проходы с бесконечной высотой не считаются: иначе 1 и N страниц чередовались бы.
        if (!double.IsInfinity(available.Height)) owner?.ReportPages(_pageStarts.Count);

        for (var i = 0; i < n; i++)
            SetIsPageStart(InternalChildren[i], _pageStarts.Contains(i - i % cols));

        var (from, to) = PageRange(page);
        double height = 0;
        for (var r = from / cols; r * cols < to; r++) height += _rowHeights[r];
        return new Size(double.IsInfinity(width) ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cols = Math.Max(1, Owner?.Columns ?? 1);
        var cellW = finalSize.Width / cols;
        var page = Math.Clamp(Owner?.PageIndex ?? 0, 0, _pageStarts.Count - 1);
        var (from, to) = PageRange(page);
        double y = 0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            var r = i / cols;
            if (i >= from && i < to)
            {
                var rowTop = y;
                for (var q = from / cols; q < r; q++) rowTop = 0;
                child.Arrange(new Rect((i % cols) * cellW, RowTop(from, r, cols), cellW, _rowHeights[r]));
            }
            else
            {
                child.Arrange(new Rect(0, finalSize.Height + 10000, cellW, child.DesiredSize.Height));
            }
        }
        return finalSize;
    }

    private double RowTop(int from, int row, int cols)
    {
        double top = 0;
        for (var q = from / cols; q < row; q++) top += _rowHeights[q];
        return top;
    }

    private (int From, int To) PageRange(int page)
    {
        if (_pageStarts.Count == 0) return (0, InternalChildren.Count);
        var from = _pageStarts[page];
        var to = page + 1 < _pageStarts.Count ? _pageStarts[page + 1] : InternalChildren.Count;
        return (from, to);
    }
}
