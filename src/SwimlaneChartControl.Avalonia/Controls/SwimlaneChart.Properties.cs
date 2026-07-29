using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using SwimlaneChartControl.Avalonia.Internal;

namespace SwimlaneChartControl.Avalonia;

public partial class SwimlaneChart
{
    /// <summary>The collection of tasks to display.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SwimlaneChart, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Name of the item property that provides the lane a task belongs to.</summary>
    public static readonly StyledProperty<string?> LanePathProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(LanePath));

    /// <summary>Name of the item property that provides the task label.</summary>
    public static readonly StyledProperty<string?> TaskNamePathProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(TaskNamePath));

    /// <summary>Name of the item property that provides the task start date.</summary>
    public static readonly StyledProperty<string?> StartPathProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(StartPath));

    /// <summary>Name of the item property that provides the task end date.</summary>
    public static readonly StyledProperty<string?> EndPathProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(EndPath));

    /// <summary>Name of the item property that provides a per-task <see cref="IBrush"/>, if any.</summary>
    public static readonly StyledProperty<string?> BrushPathProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(BrushPath));

    /// <summary>Title displayed above the chart.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(Title));

    /// <summary>Minimum vertical space allotted to each lane, in pixels. Grows automatically when tasks stack.</summary>
    public static readonly StyledProperty<double> LaneHeightProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(LaneHeight), 80.0);

    /// <summary>Height of an individual task bar, in pixels.</summary>
    public static readonly StyledProperty<double> TaskHeightProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(TaskHeight), 30.0);

    /// <summary>Gap between stacked task rows within a lane, in pixels.</summary>
    public static readonly StyledProperty<double> TaskSpacingProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(TaskSpacing), 6.0);

    /// <summary>Width of the lane-label column, in pixels.</summary>
    public static readonly StyledProperty<double> LaneLabelWidthProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(LaneLabelWidth), 140.0);

    /// <summary>Corner radius applied to task bars.</summary>
    public static readonly StyledProperty<CornerRadius> TaskCornerRadiusProperty =
        AvaloniaProperty.Register<SwimlaneChart, CornerRadius>(nameof(TaskCornerRadius), new CornerRadius(4));

    /// <summary>Brush used to paint a task bar when the item has no resolvable <see cref="BrushPath"/> value.</summary>
    public static readonly StyledProperty<IBrush?> TaskBrushProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(TaskBrush));

    /// <summary>Brush used to paint task labels drawn on top of task bars.</summary>
    public static readonly StyledProperty<IBrush?> TaskForegroundProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(TaskForeground));

    /// <summary>Brush used to paint the background band of even-indexed lanes (0, 2, 4, ...).</summary>
    public static readonly StyledProperty<IBrush?> LaneBackgroundBrushProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(LaneBackgroundBrush));

    /// <summary>Brush used for lane-separator and timeline gridlines.</summary>
    public static readonly StyledProperty<IBrush?> GridLineBrushProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(GridLineBrush));

    /// <summary>
    /// Brush used for lane separator lines: painted as the background band of odd-indexed lanes
    /// (1, 3, 5, ...), interleaved with <see cref="LaneBackgroundBrush"/> to produce alternating
    /// lane bands.
    /// </summary>
    public static readonly StyledProperty<IBrush?> LaneSeparatorBrushProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(LaneSeparatorBrush));

    /// <summary>Brush used to paint the "today" marker line.</summary>
    public static readonly StyledProperty<IBrush?> TodayLineBrushProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(TodayLineBrush));

    /// <summary>Brush used to outline the selected task bar.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(SelectionBrush));

    /// <summary>Which gridlines (horizontal, vertical, both or none) are drawn.</summary>
    public static readonly StyledProperty<GridLinesVisibility> GridLinesVisibilityProperty =
        AvaloniaProperty.Register<SwimlaneChart, GridLinesVisibility>(nameof(GridLinesVisibility),
            GridLinesVisibility.All);

    /// <summary>Whether the "today" marker line is drawn.</summary>
    public static readonly StyledProperty<bool> ShowTodayLineProperty =
        AvaloniaProperty.Register<SwimlaneChart, bool>(nameof(ShowTodayLine), true);

    /// <summary>Whether each task's label is drawn on top of its bar.</summary>
    public static readonly StyledProperty<bool> ShowTaskLabelsProperty =
        AvaloniaProperty.Register<SwimlaneChart, bool>(nameof(ShowTaskLabels), true);

    /// <summary>
    /// Custom .NET date/time format string (e.g. <c>"dd.MM.yyyy"</c>) used for timeline axis
    /// labels. When <see langword="null"/> or empty, a granularity-aware default is used instead
    /// ("MMM d" for day/week ticks, "MMM yyyy" for month ticks).
    /// </summary>
    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<SwimlaneChart, string?>(nameof(DateFormat));

    /// <summary>The currently selected task's source item.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SwimlaneChart, object?>(nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Current horizontal zoom factor. 1.0 is the base scale.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(Zoom), 1.0, coerce: CoerceZoom);

    /// <summary>Minimum allowed value of <see cref="Zoom"/>.</summary>
    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(MinZoom), 0.2);

    /// <summary>Maximum allowed value of <see cref="Zoom"/>.</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(MaxZoom), 8.0);

    /// <summary>Whether the chart can be panned (drag, Ctrl/Shift+wheel, scrollbars).</summary>
    public static readonly StyledProperty<bool> IsPanEnabledProperty =
        AvaloniaProperty.Register<SwimlaneChart, bool>(nameof(IsPanEnabled), true);

    /// <summary>Whether the chart can be zoomed (plain mouse wheel).</summary>
    public static readonly StyledProperty<bool> IsZoomEnabledProperty =
        AvaloniaProperty.Register<SwimlaneChart, bool>(nameof(IsZoomEnabled), true);

    /// <summary>Current horizontal scroll (pan) offset, in pixels.</summary>
    public static readonly StyledProperty<double> HorizontalOffsetProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(HorizontalOffset), coerce: CoerceHorizontalOffset);

    /// <summary>Current vertical scroll (pan) offset, in pixels.</summary>
    public static readonly StyledProperty<double> VerticalOffsetProperty =
        AvaloniaProperty.Register<SwimlaneChart, double>(nameof(VerticalOffset), coerce: CoerceVerticalOffset);

    /// <summary>Date currently shown at the left edge of the plot area (read-only, follows pan/zoom).</summary>
    public static readonly DirectProperty<SwimlaneChart, DateTime> ViewportStartDateProperty =
        AvaloniaProperty.RegisterDirect<SwimlaneChart, DateTime>(nameof(ViewportStartDate),
            o => o._viewportStartDate);

    /// <summary>Date currently shown at the right edge of the plot area (read-only, follows pan/zoom).</summary>
    public static readonly DirectProperty<SwimlaneChart, DateTime> ViewportEndDateProperty =
        AvaloniaProperty.RegisterDirect<SwimlaneChart, DateTime>(nameof(ViewportEndDate), o => o._viewportEndDate);

    /// <summary>Raised when <see cref="SelectedItem"/> changes.</summary>
    public static readonly RoutedEvent<SelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<SwimlaneChart, SelectionChangedEventArgs>(nameof(SelectionChanged),
            RoutingStrategies.Bubble);

    static SwimlaneChart()
    {
        AffectsRender<SwimlaneChart>(
            TitleProperty,
            ZoomProperty,
            LaneHeightProperty,
            TaskHeightProperty,
            TaskSpacingProperty,
            LaneLabelWidthProperty,
            TaskCornerRadiusProperty,
            TaskBrushProperty,
            TaskForegroundProperty,
            LaneBackgroundBrushProperty,
            GridLineBrushProperty,
            LaneSeparatorBrushProperty,
            TodayLineBrushProperty,
            SelectionBrushProperty,
            GridLinesVisibilityProperty,
            ShowTodayLineProperty,
            ShowTaskLabelsProperty,
            DateFormatProperty,
            SelectedItemProperty,
            BackgroundProperty,
            BorderBrushProperty,
            BorderThicknessProperty,
            ForegroundProperty);
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="LanePathProperty"/>
    public string? LanePath
    {
        get => GetValue(LanePathProperty);
        set => SetValue(LanePathProperty, value);
    }

    /// <inheritdoc cref="TaskNamePathProperty"/>
    public string? TaskNamePath
    {
        get => GetValue(TaskNamePathProperty);
        set => SetValue(TaskNamePathProperty, value);
    }

    /// <inheritdoc cref="StartPathProperty"/>
    public string? StartPath
    {
        get => GetValue(StartPathProperty);
        set => SetValue(StartPathProperty, value);
    }

    /// <inheritdoc cref="EndPathProperty"/>
    public string? EndPath
    {
        get => GetValue(EndPathProperty);
        set => SetValue(EndPathProperty, value);
    }

    /// <inheritdoc cref="BrushPathProperty"/>
    public string? BrushPath
    {
        get => GetValue(BrushPathProperty);
        set => SetValue(BrushPathProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="LaneHeightProperty"/>
    public double LaneHeight
    {
        get => GetValue(LaneHeightProperty);
        set => SetValue(LaneHeightProperty, value);
    }

    /// <inheritdoc cref="TaskHeightProperty"/>
    public double TaskHeight
    {
        get => GetValue(TaskHeightProperty);
        set => SetValue(TaskHeightProperty, value);
    }

    /// <inheritdoc cref="TaskSpacingProperty"/>
    public double TaskSpacing
    {
        get => GetValue(TaskSpacingProperty);
        set => SetValue(TaskSpacingProperty, value);
    }

    /// <inheritdoc cref="LaneLabelWidthProperty"/>
    public double LaneLabelWidth
    {
        get => GetValue(LaneLabelWidthProperty);
        set => SetValue(LaneLabelWidthProperty, value);
    }

    /// <inheritdoc cref="TaskCornerRadiusProperty"/>
    public CornerRadius TaskCornerRadius
    {
        get => GetValue(TaskCornerRadiusProperty);
        set => SetValue(TaskCornerRadiusProperty, value);
    }

    /// <inheritdoc cref="TaskBrushProperty"/>
    public IBrush? TaskBrush
    {
        get => GetValue(TaskBrushProperty);
        set => SetValue(TaskBrushProperty, value);
    }

    /// <inheritdoc cref="TaskForegroundProperty"/>
    public IBrush? TaskForeground
    {
        get => GetValue(TaskForegroundProperty);
        set => SetValue(TaskForegroundProperty, value);
    }

    /// <inheritdoc cref="LaneBackgroundBrushProperty"/>
    public IBrush? LaneBackgroundBrush
    {
        get => GetValue(LaneBackgroundBrushProperty);
        set => SetValue(LaneBackgroundBrushProperty, value);
    }

    /// <inheritdoc cref="GridLineBrushProperty"/>
    public IBrush? GridLineBrush
    {
        get => GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    /// <inheritdoc cref="LaneSeparatorBrushProperty"/>
    public IBrush? LaneSeparatorBrush
    {
        get => GetValue(LaneSeparatorBrushProperty);
        set => SetValue(LaneSeparatorBrushProperty, value);
    }

    /// <inheritdoc cref="TodayLineBrushProperty"/>
    public IBrush? TodayLineBrush
    {
        get => GetValue(TodayLineBrushProperty);
        set => SetValue(TodayLineBrushProperty, value);
    }

    /// <inheritdoc cref="SelectionBrushProperty"/>
    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <inheritdoc cref="GridLinesVisibilityProperty"/>
    public GridLinesVisibility GridLinesVisibility
    {
        get => GetValue(GridLinesVisibilityProperty);
        set => SetValue(GridLinesVisibilityProperty, value);
    }

    /// <inheritdoc cref="ShowTodayLineProperty"/>
    public bool ShowTodayLine
    {
        get => GetValue(ShowTodayLineProperty);
        set => SetValue(ShowTodayLineProperty, value);
    }

    /// <inheritdoc cref="ShowTaskLabelsProperty"/>
    public bool ShowTaskLabels
    {
        get => GetValue(ShowTaskLabelsProperty);
        set => SetValue(ShowTaskLabelsProperty, value);
    }

    /// <inheritdoc cref="DateFormatProperty"/>
    public string? DateFormat
    {
        get => GetValue(DateFormatProperty);
        set => SetValue(DateFormatProperty, value);
    }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <inheritdoc cref="MinZoomProperty"/>
    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    /// <inheritdoc cref="MaxZoomProperty"/>
    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <inheritdoc cref="IsPanEnabledProperty"/>
    public bool IsPanEnabled
    {
        get => GetValue(IsPanEnabledProperty);
        set => SetValue(IsPanEnabledProperty, value);
    }

    /// <inheritdoc cref="IsZoomEnabledProperty"/>
    public bool IsZoomEnabled
    {
        get => GetValue(IsZoomEnabledProperty);
        set => SetValue(IsZoomEnabledProperty, value);
    }

    /// <inheritdoc cref="HorizontalOffsetProperty"/>
    public double HorizontalOffset
    {
        get => GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    /// <inheritdoc cref="VerticalOffsetProperty"/>
    public double VerticalOffset
    {
        get => GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <inheritdoc cref="ViewportStartDateProperty"/>
    public DateTime ViewportStartDate => _viewportStartDate;

    /// <inheritdoc cref="ViewportEndDateProperty"/>
    public DateTime ViewportEndDate => _viewportEndDate;

    /// <summary>Raised when <see cref="SelectedItem"/> changes.</summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    private static double CoerceZoom(AvaloniaObject sender, double value)
    {
        var chart = (SwimlaneChart)sender;
        var min = chart.MinZoom;
        var max = Math.Max(min, chart.MaxZoom);
        return MathUtil.Clamp(value, min, max);
    }

    private static double CoerceHorizontalOffset(AvaloniaObject sender, double value)
    {
        var chart = (SwimlaneChart)sender;
        var layout = chart.ComputeLayout();
        var max = Math.Max(0, layout.ContentWidth - layout.PlotWidth);
        return MathUtil.Clamp(value, 0, max);
    }

    private static double CoerceVerticalOffset(AvaloniaObject sender, double value)
    {
        var chart = (SwimlaneChart)sender;
        var layout = chart.ComputeLayout();
        var max = Math.Max(0, layout.ContentHeight - layout.PlotHeight);
        return MathUtil.Clamp(value, 0, max);
    }
}
