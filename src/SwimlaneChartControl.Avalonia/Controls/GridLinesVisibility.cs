namespace SwimlaneChartControl.Avalonia;

/// <summary>Controls which gridlines a <see cref="SwimlaneChart"/> draws.</summary>
public enum GridLinesVisibility
{
    /// <summary>No gridlines are drawn.</summary>
    None,

    /// <summary>Only horizontal (lane-separator) gridlines are drawn.</summary>
    Horizontal,

    /// <summary>Only vertical (date-tick) gridlines are drawn.</summary>
    Vertical,

    /// <summary>Both horizontal and vertical gridlines are drawn.</summary>
    All
}
