# SwimlaneChartControl.Avalonia

A `SwimlaneChart` control for [Avalonia](https://avaloniaui.net/), inspired by the
[Swimlane chart control from Avalonia Pro](https://docs.avaloniaui.net/controls/data-display/charts/scheduling/swimlane-chart).
It groups tasks into horizontal lanes (departments, teams, workflow stages, ...) and renders them
as bars against a **date/time timeline** — unlike the Avalonia Pro control, task spans are plain
`DateTime` values rather than arbitrary `double` positions. Like
[`GanttChartControl.Avalonia`](https://github.com/Tulesha/GanttChartControl.Avalonia), it adds
built-in **pan & zoom** (mouse drag, mouse wheel and scrollbars) so large schedules stay usable
without any extra wiring.

![SwimlaneChart demo](docs/swimlane-chart-demo.gif)

## Features

- Data-bound tasks via `ItemsSource` + string property paths (`LanePath`, `TaskNamePath`,
  `StartPath`, `EndPath`, `BrushPath`) — no need for a fixed item type.
- Live updates — if a task item implements `INotifyPropertyChanged`, editing `Lane`/`Name`/
  `Start`/`End`/`Brush` (or whatever properties the paths point at) in place automatically
  redraws the affected bar. No need to replace the item or reassign `ItemsSource`.
- Tasks are grouped into lanes (in first-appearance order) by the value of `LanePath`, with a
  lane-label column on the left.
- Alternating full-width lane bands via `LaneBackgroundBrush` (even-indexed lanes) and
  `LaneSeparatorBrush` (odd-indexed lanes).
- Overlapping tasks within the same lane are automatically detected and stacked into sub-rows
  (using a greedy interval-scheduling pass), respecting `TaskSpacing` gaps; a lane grows taller
  than `LaneHeight` automatically if it needs more than one sub-row.
- Per-task color via `BrushPath` (accepts an `IBrush` value or a color string such as `"#FF5733"`),
  falling back to `TaskBrush` when unset.
- Date axis that automatically switches between day / week / month tick granularity depending on
  the current zoom level, with automatic label thinning to avoid overlap.
- "Today" marker line.
- Task selection (click a bar), with a `SelectionChanged` routed event and a two-way
  `SelectedItem` property. Clicking empty space clears the selection.
- Customizable axis date format via `DateFormat`, and independently toggleable horizontal /
  vertical gridlines via `GridLinesVisibility`.
- Pan: drag anywhere on the chart, Ctrl+wheel (vertical), Shift+wheel (horizontal), or the
  built-in scrollbars.
- Zoom: plain mouse wheel, zooming around the pointer position.
- Fully themable through a `ControlTheme` — colors, fonts, lane height, task height, etc. are all
  styled properties.

## Getting started

Reference the control's `ControlTheme` (already merged into `SwimlaneChartControl.Avalonia.axaml`)
from your `App.axaml`:

```xml

<Application.Styles>
  <FluentTheme/>
  <StyleInclude Source="avares://SwimlaneChartControl.Avalonia/Themes/SwimlaneChartControl.Avalonia.axaml"/>
</Application.Styles>
```

Then use the control:

```xml

<controls:SwimlaneChart xmlns:controls="using:SwimlaneChartControl.Avalonia"
                        ItemsSource="{Binding Tasks}"
                        LanePath="Lane"
                        TaskNamePath="Name"
                        StartPath="Start"
                        EndPath="End"
                        BrushPath="Brush"
                        Title="Project Schedule"/>
```

```csharp
public sealed record SwimlaneTask(string Lane, string Name, DateTime Start, DateTime End, IBrush? Brush);

public ObservableCollection<SwimlaneTask> Tasks { get; } = new()
{
    new("Design", "Wireframe", DateTime.Today, DateTime.Today.AddDays(3), Brushes.CornflowerBlue),
    new("Design", "Mockups", DateTime.Today.AddDays(3), DateTime.Today.AddDays(6), Brushes.MediumSeaGreen),
    new("Development", "Backend", DateTime.Today.AddDays(4), DateTime.Today.AddDays(11), Brushes.IndianRed),
    new("Testing", "Unit tests", DateTime.Today.AddDays(6), DateTime.Today.AddDays(9), Brushes.SkyBlue),
};
```

A plain `record` like the one above is enough to get started, but its properties are `init`-only,
so updating a task later means replacing it in the collection (see
[Live updates](#live-updates) below for a mutable alternative).

See [`src/SwimlaneChartControl.Avalonia.Demo`](src/SwimlaneChartControl.Avalonia.Demo) for a
complete runnable example.

## Live updates

`SwimlaneChart` redraws automatically when:

- **The `ItemsSource` collection changes shape** — items are added, removed, replaced, or the
  collection is reset — provided `ItemsSource` implements `INotifyCollectionChanged` (e.g.
  `ObservableCollection<T>`).
- **A property on an individual task item changes**, provided the item implements
  `INotifyPropertyChanged` **and** the changed property name matches one of `LanePath`,
  `TaskNamePath`, `StartPath`, `EndPath` or `BrushPath` (or the item raises `PropertyChanged` with
  a `null`/empty property name, which conventionally means "several/all properties changed").

Given a mutable, notifying task model:

```csharp
public sealed partial class SwimlaneTask : ObservableObject // CommunityToolkit.Mvvm
{
    [ObservableProperty] private string _lane;
    [ObservableProperty] private string _name;
    [ObservableProperty] private DateTime _start;
    [ObservableProperty] private DateTime _end;
    [ObservableProperty] private IBrush? _brush;

    public SwimlaneTask(string lane, string name, DateTime start, DateTime end, IBrush? brush)
        => (_lane, _name, _start, _end, _brush) = (lane, name, start, end, brush);
}
```

you can update a task in place and the bound bar updates on its own, no item replacement or
`ItemsSource` reassignment needed:

```csharp
task.Start = task.Start.AddDays(1);
task.End = task.End.AddDays(1); // the chart re-renders that task's bar automatically
```

`INotifyPropertyChanged` doesn't require the MVVM toolkit — any hand-rolled implementation that
raises `PropertyChanged` with the matching property name works the same way.

If your task type doesn't implement `INotifyPropertyChanged` (e.g. it's an immutable `record`),
in-place mutation isn't observed. Instead, replace the item in an `ObservableCollection<T>`:

```csharp
Tasks[i] = Tasks[i] with { Start = Tasks[i].Start.AddDays(1), End = Tasks[i].End.AddDays(1) };
```

## Public API

### Data properties

| Property       | Type           | Default | Description                                                                                 |
|----------------|----------------|---------|-----------------------------------------------------------------------------------------------|
| `ItemsSource`  | `IEnumerable?` | `null`  | The collection of tasks to display.                                                          |
| `LanePath`     | `string?`      | `null`  | Name of the item property that provides the lane a task belongs to.                          |
| `TaskNamePath` | `string?`      | `null`  | Name of the item property that provides the task label.                                      |
| `StartPath`    | `string?`      | `null`  | Name of the item property that provides the task start date (`DateTime`/`DateTimeOffset`).   |
| `EndPath`      | `string?`      | `null`  | Name of the item property that provides the task end date.                                   |
| `BrushPath`    | `string?`      | `null`  | Name of the item property that provides a per-task `IBrush` (or a color string) to paint it. |

Tasks are grouped into lanes by the string value read through `LanePath` (`ToString()` of
whatever the property returns), preserving the order in which each lane name first appears in
`ItemsSource`. If `LanePath` is unset, every task falls into a single unnamed lane.

### Header / lane / task appearance

| Property              | Type                  | Default   | Description                                                                                                                                                                          |
|-----------------------|-----------------------|-----------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Title`               | `string?`             | `null`    | Title displayed centered above the timeline.                                                                                                                                         |
| `LaneHeight`          | `double`              | `80.0`    | Minimum vertical space allotted to each lane, in pixels. A lane grows taller automatically if it needs more than one stacked sub-row.                                               |
| `TaskHeight`          | `double`              | `30.0`    | Height of an individual task bar, in pixels.                                                                                                                                          |
| `TaskSpacing`         | `double`              | `6.0`     | Gap between stacked task rows within a lane, in pixels (also used as the padding above/below the stacked rows when a lane grows to fit them).                                       |
| `LaneLabelWidth`      | `double`              | `140.0`   | Width of the left-hand lane-label column.                                                                                                                                            |
| `TaskCornerRadius`    | `CornerRadius`        | `4`       | Corner radius applied to task bars.                                                                                                                                                  |
| `TaskBrush`           | `IBrush?`             | *(theme)* | Brush used to paint a task bar when the item has no resolvable `BrushPath` value.                                                                                                    |
| `TaskForeground`      | `IBrush?`             | *(theme)* | Brush used to paint task labels drawn on top of task bars.                                                                                                                           |
| `LaneBackgroundBrush` | `IBrush?`             | *(theme)* | Brush used to paint the full-width background band of even-indexed lanes (0, 2, 4, ...).                                                                                            |
| `LaneSeparatorBrush`  | `IBrush?`             | `Transparent` | Brush used for lane separator lines: painted as the background band of odd-indexed lanes (1, 3, 5, ...), interleaved with `LaneBackgroundBrush` to produce alternating lane bands. |
| `GridLineBrush`       | `IBrush?`             | *(theme)* | Brush used for lane-separator and timeline gridlines.                                                                                                                                |
| `TodayLineBrush`      | `IBrush?`             | *(theme)* | Brush used for the "today" marker line.                                                                                                                                              |
| `SelectionBrush`      | `IBrush?`             | *(theme)* | Brush used to outline the selected task bar.                                                                                                                                         |
| `GridLinesVisibility` | `GridLinesVisibility` | `All`     | Which gridlines are drawn: `None`, `Horizontal`, `Vertical` or `All`.                                                                                                                |
| `ShowTodayLine`       | `bool`                | `true`    | Whether the "today" marker line is drawn.                                                                                                                                            |
| `ShowTaskLabels`      | `bool`                | `true`    | Whether each task's name is drawn as text on top of its bar (only when it fits).                                                                                                     |
| `DateFormat`          | `string?`             | `null`    | Custom .NET date format string for axis labels (e.g. `"dd.MM.yyyy"`). When `null`, a granularity-aware default is used (`"MMM d"` for day/week ticks, `"MMM yyyy"` for month ticks). |

Standard `TemplatedControl` properties (`Background`, `BorderBrush`, `BorderThickness`,
`CornerRadius`, `Foreground`, `FontSize`, `FontFamily`, …) are also honored.

Properties marked `*(theme)*` have no hardcoded CLR default; their values are supplied by the
control theme in
[`SwimlaneChart.axaml`](src/SwimlaneChartControl.Avalonia/Themes/Controls/SwimlaneChart.axaml) via
`DynamicResource` bindings, so they follow the active Fluent/theme palette automatically:

| Property               | Theme resource                           |
|------------------------|-------------------------------------------|
| `BorderBrush`          | `SystemControlForegroundBaseLowBrush`    |
| `Foreground`           | `SystemControlForegroundBaseHighBrush`   |
| `GridLineBrush`        | `SystemControlBackgroundBaseMediumBrush` |
| `LaneSeparatorBrush`   | `Transparent`                            |
| `LaneBackgroundBrush`  | `SystemControlBackgroundBaseLowBrush`    |
| `TaskBrush`            | `SystemControlHighlightAccentBrush`      |
| `TaskForeground`       | `SystemControlForegroundBaseHighBrush`   |
| `SelectionBrush`       | `SystemControlHighlightAccentBrush`      |
| `TodayLineBrush`       | `SystemControlHighlightAccentBrush`      |

Set the property explicitly (or override the resource key) to use a fixed color instead.

### Selection

| Member             | Type                                             | Description                                                                                                      |
|--------------------|--------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|
| `SelectedItem`     | `object?`                                        | The source item of the selected task (two-way bindable). Clicking a bar sets it; clicking empty space clears it. |
| `SelectionChanged` | `event EventHandler<SelectionChangedEventArgs>?` | Raised when `SelectedItem` changes.                                                                                |

### Pan & zoom

| Property            | Type                   | Default | Description                                                                                                                                                                                                                                                                                      |
|---------------------|------------------------|---------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Zoom`              | `double`               | `1.0`   | Horizontal zoom factor (clamped to the effective min/max, see `AutoMinZoom`/`AutoMaxZoom` below).                                                                                                                                                                                                |
| `MinZoom`           | `double`               | `0.2`   | Minimum allowed `Zoom`. Ignored while `AutoMinZoom` is `true` and there is data to measure.                                                                                                                                                                                                       |
| `MaxZoom`           | `double`               | `8.0`   | Maximum allowed `Zoom`. Ignored while `AutoMaxZoom` is `true` and there is data to measure.                                                                                                                                                                                                       |
| `AutoMinZoom`       | `bool`                 | `false` | When `true`, the minimum `Zoom` is computed automatically instead of using `MinZoom`: the chart can't be zoomed out past the point where the entire date range of the current data exactly fills the plot width. Falls back to `MinZoom` while there's no data (or no plot width) to measure. |
| `AutoMaxZoom`       | `bool`                 | `false` | When `true`, the maximum `Zoom` is computed automatically instead of using `MaxZoom`: the chart can't be zoomed in past the point where the shortest task in the current data exactly fills the plot width. Falls back to `MaxZoom` while there's no data (or no plot width) to measure.        |
| `IsPanEnabled`      | `bool`                 | `true`  | Enables/disables drag, Ctrl/Shift+wheel and scrollbar panning.                                                                                                                                                                                                                                    |
| `IsZoomEnabled`     | `bool`                 | `true`  | Enables/disables mouse-wheel zooming.                          |
| `HorizontalOffset`  | `double`               | `0.0`   | Current horizontal pan offset, in pixels.                      |
| `VerticalOffset`    | `double`               | `0.0`   | Current vertical pan offset, in pixels.                        |
| `ViewportStartDate` | `DateTime` (read-only) | —       | Date currently shown at the left edge of the plot area.        |
| `ViewportEndDate`   | `DateTime` (read-only) | —       | Date currently shown at the right edge of the plot area.       |

### Interactions

| Input               | Effect                                                          |
|---------------------|-------------------------------------------------------------------|
| Drag on the chart   | Pans horizontally and vertically.                               |
| Mouse wheel         | Zooms in/out, centered on the pointer.                          |
| Ctrl + mouse wheel  | Pans vertically.                                                |
| Shift + mouse wheel | Pans horizontally.                                              |
| Click a task bar    | Selects it (updates `SelectedItem`, raises `SelectionChanged`). |
| Click empty space   | Clears the current selection.                                    |
| Scrollbars          | Pan directly.                                                    |

## Project layout

- [`src/SwimlaneChartControl.Avalonia`](src/SwimlaneChartControl.Avalonia) — the control library
  (`Controls/SwimlaneChart.cs` for behavior, `Controls/SwimlaneChart.Properties.cs` for Avalonia
  properties, `Themes/Controls/SwimlaneChart.axaml` for the `ControlTheme`).
- [`src/SwimlaneChartControl.Avalonia.Demo`](src/SwimlaneChartControl.Avalonia.Demo) — a runnable
  demo application.
