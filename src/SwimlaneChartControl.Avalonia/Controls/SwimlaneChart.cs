using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using SwimlaneChartControl.Avalonia.Internal;

namespace SwimlaneChartControl.Avalonia;

/// <summary>
/// A timeline control that groups tasks into horizontal lanes and renders them as bars against a
/// date axis, with mouse/touch panning, wheel/pinch zooming and scrollbars. Overlapping tasks
/// within the same lane automatically stack into sub-rows.
/// </summary>
public partial class SwimlaneChart : TemplatedControl
{
    private const string PartHorizontalScrollBar = "PART_HorizontalScrollBar";
    private const string PartVerticalScrollBar = "PART_VerticalScrollBar";

    private const double BaseDayWidth = 30.0;
    private const double TitleRowHeight = 32.0;
    private const double AxisRowHeight = 28.0;
    private const double ScrollBarThickness = 14.0;

    private static readonly ConcurrentPropertyCache PropertyCache = new();

    private readonly List<SwimlaneTaskItem> _items = new();
    private readonly List<LaneInfo> _lanes = new();
    private readonly HashSet<INotifyPropertyChanged> _subscribedItems = new();

    private ScrollBar? _horizontalScrollBar;

    private bool _isPanning;
    private DateTime _maxDate = DateTime.Today.AddDays(7);
    private DateTime _minDate = DateTime.Today;
    private double _panStartHorizontalOffset;
    private Point _panStartPointer;
    private double _panStartVerticalOffset;
    private bool _updatingScrollBars;
    private ScrollBar? _verticalScrollBar;
    private DateTime _viewportEndDate;
    private DateTime _viewportStartDate;

    /// <inheritdoc/>
    public SwimlaneChart()
    {
        RebuildItems();
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachTemplateParts();

        _horizontalScrollBar = e.NameScope.Find<ScrollBar>(PartHorizontalScrollBar);
        _verticalScrollBar = e.NameScope.Find<ScrollBar>(PartVerticalScrollBar);

        AttachTemplateParts();
        UpdateScrollBars();
    }

    private void AttachTemplateParts()
    {
        if (_horizontalScrollBar is not null) _horizontalScrollBar.Scroll += OnHorizontalScrollBarScroll;
        if (_verticalScrollBar is not null) _verticalScrollBar.Scroll += OnVerticalScrollBarScroll;
    }

    private void DetachTemplateParts()
    {
        if (_horizontalScrollBar is not null) _horizontalScrollBar.Scroll -= OnHorizontalScrollBarScroll;
        if (_verticalScrollBar is not null) _verticalScrollBar.Scroll -= OnVerticalScrollBarScroll;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        // Bounds isn't assigned until after this method returns, so it still reflects the
        // previous arrange pass (Rect.Empty on the very first one). Use finalSize directly
        // rather than reading the (stale) Bounds property via ComputeLayout()/UpdateScrollBars().
        UpdateScrollBars(finalSize);
        return result;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldIncc)
                oldIncc.CollectionChanged -= OnItemsCollectionChanged;
            if (change.NewValue is INotifyCollectionChanged newIncc)
                newIncc.CollectionChanged += OnItemsCollectionChanged;

            RebuildItems();
        }
        else if (change.Property == StartPathProperty ||
                 change.Property == EndPathProperty ||
                 change.Property == LanePathProperty ||
                 change.Property == TaskNamePathProperty ||
                 change.Property == BrushPathProperty)
        {
            RebuildItems();
        }
        else if (change.Property == LaneHeightProperty ||
                 change.Property == TaskHeightProperty ||
                 change.Property == TaskSpacingProperty)
        {
            RecomputeLaneLayout();
            CoerceValue(HorizontalOffsetProperty);
            CoerceValue(VerticalOffsetProperty);
            UpdateScrollBars();
            UpdateViewportDates();
        }
        else if (change.Property == ZoomProperty)
        {
            CoerceValue(HorizontalOffsetProperty);
            CoerceValue(VerticalOffsetProperty);
            UpdateScrollBars();
            UpdateViewportDates();
        }
        else if (change.Property == MinZoomProperty || change.Property == MaxZoomProperty)
        {
            CoerceValue(ZoomProperty);
        }
        else if (change.Property == HorizontalOffsetProperty || change.Property == VerticalOffsetProperty)
        {
            SyncScrollBars();
            UpdateViewportDates();
        }
        else if (change.Property == BoundsProperty)
        {
            CoerceValue(HorizontalOffsetProperty);
            CoerceValue(VerticalOffsetProperty);
            UpdateScrollBars();
            UpdateViewportDates();
        }
        else if (change.Property == SelectedItemProperty)
        {
            var removed = change.OldValue is { } oldItem ? new[] { oldItem } : Array.Empty<object>();
            var added = change.NewValue is { } newItem ? new[] { newItem } : Array.Empty<object>();
            RaiseEvent(new SelectionChangedEventArgs(SelectionChangedEvent, removed, added));
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void RebuildItems()
    {
        _items.Clear();

        var itemsSource = ItemsSource;
        var startPath = StartPath;
        var endPath = EndPath;
        var currentItems = new HashSet<INotifyPropertyChanged>();

        if (itemsSource is not null && !string.IsNullOrEmpty(startPath) && !string.IsNullOrEmpty(endPath))
        {
            var lanePath = LanePath;
            var taskNamePath = TaskNamePath;
            var brushPath = BrushPath;

            foreach (var source in itemsSource)
            {
                if (source is null) continue;

                if (source is INotifyPropertyChanged notifying)
                    currentItems.Add(notifying);

                var type = source.GetType();

                if (!TryGetDateTime(PropertyCache.GetValue(type, startPath, source), out var start)) continue;
                if (!TryGetDateTime(PropertyCache.GetValue(type, endPath, source), out var end)) continue;

                var lane = !string.IsNullOrEmpty(lanePath)
                    ? PropertyCache.GetValue(type, lanePath, source)?.ToString() ?? string.Empty
                    : string.Empty;

                var name = !string.IsNullOrEmpty(taskNamePath)
                    ? PropertyCache.GetValue(type, taskNamePath, source)?.ToString() ?? string.Empty
                    : string.Empty;

                IBrush? brush = null;
                if (!string.IsNullOrEmpty(brushPath))
                    brush = TryGetBrush(PropertyCache.GetValue(type, brushPath, source));

                _items.Add(new SwimlaneTaskItem(source, lane, name, start, end, brush));
            }
        }

        SyncItemSubscriptions(currentItems);

        RecomputeLaneLayout();
        RecomputeDateRange();
        CoerceValue(HorizontalOffsetProperty);
        CoerceValue(VerticalOffsetProperty);
        UpdateScrollBars();
        UpdateViewportDates();
        InvalidateVisual();
    }

    /// <summary>
    /// Subscribes to <see cref="INotifyPropertyChanged.PropertyChanged"/> on every source item so
    /// that in-place edits to <see cref="LanePath"/>/<see cref="StartPath"/>/<see cref="EndPath"/>/
    /// <see cref="TaskNamePath"/>/<see cref="BrushPath"/> values trigger an automatic rebuild/
    /// redraw, without requiring the caller to replace the item or reassign
    /// <see cref="ItemsSource"/>. Items no longer present are unsubscribed to avoid leaking
    /// handlers.
    /// </summary>
    private void SyncItemSubscriptions(HashSet<INotifyPropertyChanged> current)
    {
        foreach (var item in _subscribedItems)
        {
            if (!current.Contains(item))
                item.PropertyChanged -= OnSourceItemPropertyChanged;
        }

        foreach (var item in current)
        {
            if (!_subscribedItems.Contains(item))
                item.PropertyChanged += OnSourceItemPropertyChanged;
        }

        _subscribedItems.Clear();
        foreach (var item in current)
            _subscribedItems.Add(item);
    }

    private void OnSourceItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A null/empty PropertyName means "any/all properties may have changed" per the
        // INotifyPropertyChanged convention, so it always triggers a rebuild.
        if (!string.IsNullOrEmpty(e.PropertyName) &&
            e.PropertyName != StartPath &&
            e.PropertyName != EndPath &&
            e.PropertyName != LanePath &&
            e.PropertyName != TaskNamePath &&
            e.PropertyName != BrushPath)
            return;

        RebuildItems();
    }

    private static bool TryGetDateTime(object? value, out DateTime result)
    {
        switch (value)
        {
            case DateTime dateTime:
                result = dateTime;
                return true;
            case DateTimeOffset dateTimeOffset:
                result = dateTimeOffset.DateTime;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static IBrush? TryGetBrush(object? value)
    {
        switch (value)
        {
            case IBrush brush:
                return brush;
            case string text when !string.IsNullOrWhiteSpace(text):
                try
                {
                    return Brush.Parse(text);
                }
                catch (FormatException)
                {
                    return null;
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Groups <see cref="_items"/> into lanes (preserving first-appearance order) and greedily
    /// assigns each task to the first sub-row within its lane whose previous task has already
    /// ended, so overlapping tasks stack instead of overdrawing each other. Also computes each
    /// lane's cumulative vertical offset and effective height (the larger of <see cref="LaneHeight"/>
    /// and the space actually required by the stacked sub-rows).
    /// </summary>
    private void RecomputeLaneLayout()
    {
        _lanes.Clear();

        var laneOrder = new List<string>();
        var laneItems = new Dictionary<string, List<SwimlaneTaskItem>>();

        foreach (var item in _items)
        {
            if (!laneItems.TryGetValue(item.Lane, out var list))
            {
                list = new List<SwimlaneTaskItem>();
                laneItems.Add(item.Lane, list);
                laneOrder.Add(item.Lane);
            }

            list.Add(item);
        }

        var taskHeight = TaskHeight;
        var taskSpacing = TaskSpacing;
        var laneHeight = LaneHeight;
        var top = 0.0;

        foreach (var laneName in laneOrder)
        {
            var tasks = laneItems[laneName].OrderBy(t => t.Start).ThenBy(t => t.End).ToList();
            var rowEnds = new List<DateTime>();
            var laneIndex = _lanes.Count;

            foreach (var task in tasks)
            {
                var rowIndex = -1;
                for (var r = 0; r < rowEnds.Count; r++)
                {
                    if (task.Start < rowEnds[r]) continue;
                    rowIndex = r;
                    break;
                }

                if (rowIndex == -1)
                {
                    rowIndex = rowEnds.Count;
                    rowEnds.Add(task.End);
                }
                else
                {
                    rowEnds[rowIndex] = task.End;
                }

                task.LaneIndex = laneIndex;
                task.SubRow = rowIndex;
            }

            var rowCount = Math.Max(1, rowEnds.Count);
            var requiredHeight = rowCount * taskHeight + (rowCount + 1) * taskSpacing;
            var height = Math.Max(laneHeight, requiredHeight);

            _lanes.Add(new LaneInfo(laneName, top, height, rowCount));
            top += height;
        }
    }

    private void RecomputeDateRange()
    {
        if (_items.Count == 0)
        {
            _minDate = DateTime.Today;
            _maxDate = DateTime.Today.AddDays(7);
            return;
        }

        _minDate = _items.Min(i => i.Start);
        _maxDate = _items.Max(i => i.End);

        var span = (_maxDate - _minDate).TotalDays;
        var padding = TimeSpan.FromDays(Math.Max(1, span * 0.05));
        _minDate -= padding;
        _maxDate += padding;
    }

    private void UpdateViewportDates()
    {
        var layout = ComputeLayout();
        if (layout.DayWidth <= 0) return;

        var start = _minDate.AddDays(HorizontalOffset / layout.DayWidth);
        var end = _minDate.AddDays((HorizontalOffset + layout.PlotWidth) / layout.DayWidth);

        SetAndRaise(ViewportStartDateProperty, ref _viewportStartDate, start);
        SetAndRaise(ViewportEndDateProperty, ref _viewportEndDate, end);
    }

    private SwimlaneLayout ComputeLayout() => ComputeLayout(Bounds.Size);

    private SwimlaneLayout ComputeLayout(Size size)
    {
        var titleHeight = string.IsNullOrEmpty(Title) ? 0 : TitleRowHeight;
        var plotTop = titleHeight + AxisRowHeight;
        var plotLeft = LaneLabelWidth;
        var plotWidth = Math.Max(0, size.Width - plotLeft - ScrollBarThickness);
        var plotHeight = Math.Max(0, size.Height - plotTop - ScrollBarThickness);
        var dayWidth = BaseDayWidth * Zoom;
        var contentWidth = Math.Max(plotWidth, (_maxDate - _minDate).TotalDays * dayWidth);
        var lanesHeight = _lanes.Count == 0 ? 0 : _lanes[_lanes.Count - 1].TopOffset + _lanes[_lanes.Count - 1].Height;
        var contentHeight = Math.Max(plotHeight, lanesHeight);

        return new SwimlaneLayout(titleHeight, AxisRowHeight, plotLeft, plotTop, plotWidth, plotHeight, dayWidth,
            contentWidth, contentHeight);
    }

    private void UpdateScrollBars() => UpdateScrollBars(Bounds.Size);

    private void UpdateScrollBars(Size size)
    {
        if (_horizontalScrollBar is null && _verticalScrollBar is null) return;

        _updatingScrollBars = true;
        try
        {
            var layout = ComputeLayout(size);

            if (_horizontalScrollBar is not null)
            {
                var max = Math.Max(0, layout.ContentWidth - layout.PlotWidth);
                _horizontalScrollBar.Minimum = 0;
                _horizontalScrollBar.Maximum = max;
                _horizontalScrollBar.ViewportSize = layout.PlotWidth;
                _horizontalScrollBar.LargeChange = Math.Max(1, layout.PlotWidth);
                _horizontalScrollBar.SmallChange = Math.Max(1, layout.DayWidth);
                _horizontalScrollBar.Value = MathUtil.Clamp(HorizontalOffset, 0, max);
                _horizontalScrollBar.IsVisible = max > 0;
            }

            if (_verticalScrollBar is not null)
            {
                var max = Math.Max(0, layout.ContentHeight - layout.PlotHeight);
                _verticalScrollBar.Minimum = 0;
                _verticalScrollBar.Maximum = max;
                _verticalScrollBar.ViewportSize = layout.PlotHeight;
                _verticalScrollBar.LargeChange = Math.Max(1, layout.PlotHeight);
                _verticalScrollBar.SmallChange = Math.Max(1, LaneHeight);
                _verticalScrollBar.Value = MathUtil.Clamp(VerticalOffset, 0, max);
                _verticalScrollBar.IsVisible = max > 0;
            }
        }
        finally
        {
            _updatingScrollBars = false;
        }
    }

    private void SyncScrollBars()
    {
        if (_updatingScrollBars) return;

        _updatingScrollBars = true;
        try
        {
            if (_horizontalScrollBar is not null) _horizontalScrollBar.Value = HorizontalOffset;
            if (_verticalScrollBar is not null) _verticalScrollBar.Value = VerticalOffset;
        }
        finally
        {
            _updatingScrollBars = false;
        }

        InvalidateVisual();
    }

    private void OnHorizontalScrollBarScroll(object? sender, ScrollEventArgs e)
    {
        if (_updatingScrollBars) return;
        SetCurrentValue(HorizontalOffsetProperty, e.NewValue);
    }

    private void OnVerticalScrollBarScroll(object? sender, ScrollEventArgs e)
    {
        if (_updatingScrollBars) return;
        SetCurrentValue(VerticalOffsetProperty, e.NewValue);
    }

    private void ZoomAt(Point pointer, double factor, SwimlaneLayout layout)
    {
        var oldDayWidth = layout.DayWidth;
        if (oldDayWidth <= 0) return;

        var dateAtPointer = _minDate.AddDays((HorizontalOffset + pointer.X - layout.PlotLeft) / oldDayWidth);

        SetCurrentValue(ZoomProperty, Zoom * factor);

        var newLayout = ComputeLayout();
        var newOffset = (dateAtPointer - _minDate).TotalDays * newLayout.DayWidth - (pointer.X - layout.PlotLeft);
        SetCurrentValue(HorizontalOffsetProperty, newOffset);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (Background is { } background)
            context.DrawRectangle(background, null, new RoundedRect(new Rect(bounds.Size), CornerRadius));

        var layout = ComputeLayout();
        var (unit, step) = GetTickUnit(layout.DayWidth);
        var ticks = GetTicks(unit, step);

        if (layout.TitleHeight > 0)
            DrawTitle(context, layout);

        using (context.PushClip(new Rect(0, layout.PlotTop, bounds.Width, layout.PlotHeight)))
        {
            DrawLaneBackgrounds(context, layout, bounds.Width);
        }

        using (context.PushClip(new Rect(layout.PlotLeft, layout.TitleHeight, layout.PlotWidth, layout.AxisHeight)))
        {
            DrawAxis(context, layout, ticks, unit);
        }

        using (context.PushClip(new Rect(layout.PlotLeft, layout.PlotTop, layout.PlotWidth, layout.PlotHeight)))
        {
            DrawGrid(context, layout, ticks);
            DrawTasks(context, layout);
            DrawTodayLine(context, layout);
        }

        using (context.PushClip(new Rect(0, layout.PlotTop, layout.PlotLeft, layout.PlotHeight)))
        {
            DrawLaneLabels(context, layout);
        }

        if (BorderBrush is { } borderBrush && BorderThickness.Left > 0)
        {
            var half = BorderThickness.Left / 2;
            context.DrawRectangle(null, new Pen(borderBrush, BorderThickness.Left),
                new RoundedRect(new Rect(bounds.Size).Deflate(half), CornerRadius));
        }
    }

    private void DrawTitle(DrawingContext context, in SwimlaneLayout layout)
    {
        var title = Title;
        if (title is null || title.Length == 0) return;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight.Bold);
        var formattedText = new FormattedText(title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
            FontSize + 4, Foreground ?? Brushes.Black);
        var origin = new Point((Bounds.Width - formattedText.Width) / 2,
            (layout.TitleHeight - formattedText.Height) / 2);
        context.DrawText(formattedText, origin);
    }

    private void DrawLaneBackgrounds(DrawingContext context, in SwimlaneLayout layout, double width)
    {
        if (LaneBackgroundBrush is not { } brush) return;

        foreach (var lane in _lanes)
        {
            var y = layout.PlotTop + lane.TopOffset - VerticalOffset;
            if (y + lane.Height < layout.PlotTop || y > layout.PlotTop + layout.PlotHeight) continue;

            context.DrawRectangle(brush, null, new Rect(0, y, width, lane.Height));
        }
    }

    private void DrawAxis(DrawingContext context, in SwimlaneLayout layout, IReadOnlyList<DateTime> ticks,
        TickUnit unit)
    {
        var typeface = new Typeface(FontFamily);
        var pen = new Pen(GridLineBrush ?? Brushes.Transparent);
        var foreground = Foreground ?? Brushes.Black;

        var nextLabelX = double.NegativeInfinity;

        foreach (var tick in ticks)
        {
            var x = layout.PlotLeft + (tick - _minDate).TotalDays * layout.DayWidth - HorizontalOffset;
            if (x < layout.PlotLeft - 80 || x > layout.PlotLeft + layout.PlotWidth + 80) continue;
            if (x < nextLabelX) continue;

            var label = FormatTick(tick, unit);
            var formattedText = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, FontSize, foreground);
            context.DrawText(formattedText,
                new Point(x + 4, layout.TitleHeight + (layout.AxisHeight - formattedText.Height) / 2));
            nextLabelX = x + formattedText.Width + 12;
        }

        context.DrawLine(pen,
            new Point(layout.PlotLeft, layout.TitleHeight + layout.AxisHeight),
            new Point(layout.PlotLeft + layout.PlotWidth, layout.TitleHeight + layout.AxisHeight));
    }

    private void DrawGrid(DrawingContext context, in SwimlaneLayout layout, IReadOnlyList<DateTime> ticks)
    {
        var visibility = GridLinesVisibility;
        if (visibility == GridLinesVisibility.None) return;

        var pen = new Pen(GridLineBrush ?? Brushes.Transparent);

        if (visibility is GridLinesVisibility.Vertical or GridLinesVisibility.All)
        {
            foreach (var tick in ticks)
            {
                var x = layout.PlotLeft + (tick - _minDate).TotalDays * layout.DayWidth - HorizontalOffset;
                context.DrawLine(pen, new Point(x, layout.PlotTop), new Point(x, layout.PlotTop + layout.PlotHeight));
            }
        }

        if (visibility is GridLinesVisibility.Horizontal or GridLinesVisibility.All)
        {
            for (var i = 0; i <= _lanes.Count; i++)
            {
                var laneOffset = i < _lanes.Count
                    ? _lanes[i].TopOffset
                    : _lanes[_lanes.Count - 1].TopOffset + _lanes[_lanes.Count - 1].Height;
                var y = layout.PlotTop + laneOffset - VerticalOffset;
                if (y < layout.PlotTop - LaneHeight || y > layout.PlotTop + layout.PlotHeight + LaneHeight) continue;
                context.DrawLine(pen, new Point(layout.PlotLeft, y), new Point(layout.PlotLeft + layout.PlotWidth, y));
            }
        }
    }

    private Rect ComputeBarRect(SwimlaneTaskItem item, in SwimlaneLayout layout)
    {
        var lane = _lanes[item.LaneIndex];
        var taskHeight = TaskHeight;
        var taskSpacing = TaskSpacing;
        var totalContentHeight = lane.RowCount * taskHeight + (lane.RowCount - 1) * taskSpacing;
        var startY = (lane.Height - totalContentHeight) / 2;
        var rowY = startY + item.SubRow * (taskHeight + taskSpacing);
        var laneTop = layout.PlotTop + lane.TopOffset - VerticalOffset;
        var barY = laneTop + rowY;

        var barX = layout.PlotLeft + (item.Start - _minDate).TotalDays * layout.DayWidth - HorizontalOffset;
        var barWidth = Math.Max(2, (item.End - item.Start).TotalDays * layout.DayWidth);

        return new Rect(barX, barY, barWidth, taskHeight);
    }

    private void DrawTasks(DrawingContext context, in SwimlaneLayout layout)
    {
        var taskBrush = TaskBrush;
        var taskForeground = TaskForeground ?? Brushes.White;
        var selectionBrush = SelectionBrush;
        var cornerRadius = TaskCornerRadius;
        var showLabels = ShowTaskLabels;
        var selectedItem = SelectedItem;
        var typeface = showLabels ? new Typeface(FontFamily) : default;

        foreach (var item in _items)
        {
            var barRect = ComputeBarRect(item, layout);
            if (barRect.Bottom < layout.PlotTop || barRect.Top > layout.PlotTop + layout.PlotHeight) continue;
            if (barRect.Right < layout.PlotLeft || barRect.Left > layout.PlotLeft + layout.PlotWidth) continue;

            var roundedBarRect = new RoundedRect(barRect, cornerRadius);
            context.DrawRectangle(item.Brush ?? taskBrush, null, roundedBarRect);

            if (showLabels && item.Name.Length > 0)
            {
                var formattedText = new FormattedText(item.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, FontSize, taskForeground)
                {
                    MaxTextWidth = Math.Max(0, barRect.Width - 8),
                    Trimming = TextTrimming.CharacterEllipsis
                };

                if (formattedText.Height <= barRect.Height)
                {
                    var textOrigin = new Point(barRect.X + (barRect.Width - formattedText.Width) / 2,
                        barRect.Y + (barRect.Height - formattedText.Height) / 2);
                    context.DrawText(formattedText, textOrigin);
                }
            }

            if (selectedItem is not null && Equals(item.Source, selectedItem))
                context.DrawRectangle(null, new Pen(selectionBrush ?? Brushes.Transparent, 2), roundedBarRect);
        }
    }

    private void DrawTodayLine(DrawingContext context, in SwimlaneLayout layout)
    {
        if (!ShowTodayLine) return;

        var today = DateTime.Today;
        if (today < _minDate || today > _maxDate) return;

        var x = layout.PlotLeft + (today - _minDate).TotalDays * layout.DayWidth - HorizontalOffset;
        var pen = new Pen(TodayLineBrush ?? Brushes.Transparent, 1.5, new DashStyle(new double[] { 4, 2 }, 0));
        context.DrawLine(pen, new Point(x, layout.PlotTop), new Point(x, layout.PlotTop + layout.PlotHeight));
    }

    private void DrawLaneLabels(DrawingContext context, in SwimlaneLayout layout)
    {
        var typeface = new Typeface(FontFamily);
        var foreground = Foreground ?? Brushes.Black;
        var pen = new Pen(GridLineBrush ?? Brushes.Transparent);
        var showSeparators = GridLinesVisibility is GridLinesVisibility.Horizontal or GridLinesVisibility.All;

        foreach (var lane in _lanes)
        {
            var y = layout.PlotTop + lane.TopOffset - VerticalOffset;
            if (y + lane.Height < layout.PlotTop || y > layout.PlotTop + layout.PlotHeight) continue;

            var formattedText = new FormattedText(lane.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, FontSize, foreground)
            {
                MaxTextWidth = Math.Max(0, layout.PlotLeft - 16),
                Trimming = TextTrimming.CharacterEllipsis
            };
            context.DrawText(formattedText, new Point(8, y + (lane.Height - formattedText.Height) / 2));

            if (showSeparators)
            {
                var bottom = y + lane.Height;
                context.DrawLine(pen, new Point(0, bottom), new Point(layout.PlotLeft, bottom));
            }
        }
    }

    private static (TickUnit Unit, int Step) GetTickUnit(double dayWidth)
    {
        if (dayWidth >= 28) return (TickUnit.Day, 1);
        if (dayWidth >= 14) return (TickUnit.Day, 2);
        if (dayWidth >= 4) return (TickUnit.Week, 1);
        return (TickUnit.Month, 1);
    }

    private List<DateTime> GetTicks(TickUnit unit, int step)
    {
        var ticks = new List<DateTime>();
        var tick = AlignDown(_minDate, unit);

        while (tick <= _maxDate)
        {
            ticks.Add(tick);
            tick = Advance(tick, unit, step);
        }

        return ticks;
    }

    private static DateTime AlignDown(DateTime date, TickUnit unit) => unit switch
    {
        TickUnit.Day => date.Date,
        TickUnit.Week => date.Date.AddDays(-(int)date.DayOfWeek),
        TickUnit.Month => new DateTime(date.Year, date.Month, 1),
        _ => date.Date
    };

    private static DateTime Advance(DateTime date, TickUnit unit, int step) => unit switch
    {
        TickUnit.Day => date.AddDays(step),
        TickUnit.Week => date.AddDays(7 * step),
        TickUnit.Month => date.AddMonths(step),
        _ => date.AddDays(1)
    };

    private string FormatTick(DateTime date, TickUnit unit)
    {
        var format = DateFormat;
        if (!string.IsNullOrEmpty(format))
            return date.ToString(format, CultureInfo.InvariantCulture);

        return unit switch
        {
            TickUnit.Month => date.ToString("MMM yyyy", CultureInfo.InvariantCulture),
            _ => date.ToString("MMM d", CultureInfo.InvariantCulture)
        };
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);
        var layout = ComputeLayout();

        SwimlaneTaskItem? hit = null;
        if (point.X >= layout.PlotLeft && point.Y >= layout.PlotTop)
            hit = HitTestBar(point, layout);

        if (hit is not null)
        {
            SetCurrentValue(SelectedItemProperty, hit.Source);
            e.Handled = true;
            return;
        }

        // Clicked empty space (no task bar under the pointer) - clear the current selection.
        if (SelectedItem is not null)
            SetCurrentValue(SelectedItemProperty, null);

        if (IsPanEnabled && point.Y >= layout.TitleHeight)
        {
            _isPanning = true;
            _panStartPointer = point;
            _panStartHorizontalOffset = HorizontalOffset;
            _panStartVerticalOffset = VerticalOffset;
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isPanning) return;

        var point = e.GetPosition(this);
        var delta = point - _panStartPointer;

        SetCurrentValue(HorizontalOffsetProperty, _panStartHorizontalOffset - delta.X);
        SetCurrentValue(VerticalOffsetProperty, _panStartVerticalOffset - delta.Y);
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndPan(e.Pointer);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPan(null);
    }

    private void EndPan(IPointer? pointer)
    {
        if (!_isPanning) return;
        _isPanning = false;
        pointer?.Capture(null);
        Cursor = Cursor.Default;
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var layout = ComputeLayout();

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (!IsPanEnabled) return;
            SetCurrentValue(VerticalOffsetProperty, VerticalOffset - e.Delta.Y * LaneHeight);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (!IsPanEnabled) return;
            SetCurrentValue(HorizontalOffsetProperty, HorizontalOffset - e.Delta.Y * layout.DayWidth);
            e.Handled = true;
            return;
        }

        if (!IsZoomEnabled) return;
        var point = e.GetPosition(this);
        var factor = Math.Pow(1.15, e.Delta.Y);
        ZoomAt(point, factor, layout);
        e.Handled = true;
    }

    private SwimlaneTaskItem? HitTestBar(Point point, in SwimlaneLayout layout)
    {
        foreach (var item in _items)
        {
            if (ComputeBarRect(item, layout).Contains(point))
                return item;
        }

        return null;
    }

    private enum TickUnit
    {
        Day,
        Week,
        Month
    }

    private sealed class ConcurrentPropertyCache
    {
        private readonly Dictionary<(Type Type, string Path), PropertyInfo?> _cache = new();

        public object? GetValue(Type type, string? path, object source)
        {
            if (path is null) return null;

            var key = (type, path);
            if (!_cache.TryGetValue(key, out var property))
            {
                property = type.GetProperty(path);
                _cache[key] = property;
            }

            return property?.GetValue(source);
        }
    }

    private readonly record struct SwimlaneLayout(
        double TitleHeight,
        double AxisHeight,
        double PlotLeft,
        double PlotTop,
        double PlotWidth,
        double PlotHeight,
        double DayWidth,
        double ContentWidth,
        double ContentHeight);

    private readonly record struct LaneInfo(string Name, double TopOffset, double Height, int RowCount);

    private sealed class SwimlaneTaskItem
    {
        public SwimlaneTaskItem(object source, string lane, string name, DateTime start, DateTime end, IBrush? brush)
        {
            Source = source;
            Lane = lane;
            Name = name;
            Start = start;
            End = end;
            Brush = brush;
        }

        public object Source { get; }
        public string Lane { get; }
        public string Name { get; }
        public DateTime Start { get; }
        public DateTime End { get; }
        public IBrush? Brush { get; }
        public int LaneIndex { get; set; }
        public int SubRow { get; set; }
    }
}
