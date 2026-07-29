using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using SwimlaneChartControl.Avalonia.Demo.Models;

namespace SwimlaneChartControl.Avalonia.Demo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Random _random = new();

    public ObservableCollection<SwimlaneTask> Tasks { get; } = new(CreateTasks());

    private static SwimlaneTask[] CreateTasks()
    {
        var today = DateTime.Today;

        return new[]
        {
            // Design
            new SwimlaneTask("Design", "Wireframe", today, today.AddDays(3), Brushes.CornflowerBlue),
            new SwimlaneTask("Design", "Mockups", today.AddDays(3), today.AddDays(6), Brushes.MediumSeaGreen),
            new SwimlaneTask("Design", "Stakeholder review", today.AddDays(1), today.AddDays(4), Brushes.Plum),

            // Development
            new SwimlaneTask("Development", "Backend", today.AddDays(4), today.AddDays(11), Brushes.IndianRed),
            new SwimlaneTask("Development", "Frontend", today.AddDays(6), today.AddDays(13), Brushes.SteelBlue),
            new SwimlaneTask("Development", "Code freeze", today.AddDays(11), today.AddDays(12), Brushes.Goldenrod),

            // Testing
            new SwimlaneTask("Testing", "Unit tests", today.AddDays(6), today.AddDays(9), Brushes.SkyBlue),
            new SwimlaneTask("Testing", "Integration", today.AddDays(9), today.AddDays(12), Brushes.MediumPurple),

            // Deployment
            new SwimlaneTask("Deployment", "Staging", today.AddDays(11), today.AddDays(13), Brushes.Orange),
            new SwimlaneTask("Deployment", "Production", today.AddDays(13), today.AddDays(14), Brushes.Teal),
        };
    }

    /// <summary>
    /// Nudges each task's Start/End in place (no item replacement, no ItemsSource reassignment)
    /// to demonstrate that SwimlaneChart redraws automatically in response to per-item
    /// INotifyPropertyChanged notifications.
    /// </summary>
    [RelayCommand]
    private void NudgeSchedule()
    {
        foreach (var task in Tasks)
        {
            var shift = TimeSpan.FromHours(_random.Next(-8, 9));
            task.Start += shift;
            task.End += shift;
        }
    }
}
