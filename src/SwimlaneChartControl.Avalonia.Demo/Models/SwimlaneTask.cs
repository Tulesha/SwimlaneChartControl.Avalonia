using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SwimlaneChartControl.Avalonia.Demo.Models;

/// <summary>
/// Mutable, <see cref="System.ComponentModel.INotifyPropertyChanged"/>-implementing task model.
/// Raising PropertyChanged for Start/End/Name/Lane/Brush lets a bound SwimlaneChart pick up
/// in-place edits automatically, without replacing the item in the source collection.
/// </summary>
public sealed partial class SwimlaneTask : ObservableObject
{
    [ObservableProperty] private IBrush? _brush;
    [ObservableProperty] private DateTime _end;
    [ObservableProperty] private string _lane;
    [ObservableProperty] private string _name;
    [ObservableProperty] private DateTime _start;

    public SwimlaneTask(string lane, string name, DateTime start, DateTime end, IBrush? brush = null)
    {
        _lane = lane;
        _name = name;
        _start = start;
        _end = end;
        _brush = brush;
    }
}
