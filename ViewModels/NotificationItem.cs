using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace MyScheduler.ViewModels;

public class NotificationItem
{
    public int ScheduleId { get; init; }
    public string Title { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd { get; init; }

    public Brush AccentBrush { get; init; } = Brushes.DodgerBlue;
    public int AdditionalCount { get; init; }
    public IReadOnlyList<NotificationGroupItem> RelatedItems { get; init; } = Array.Empty<NotificationGroupItem>();

    public string DisplayTitle =>
        AdditionalCount > 0 ? $"{Title} 외 {AdditionalCount}건" : Title;

    public bool HasGroup => RelatedItems.Count > 1;

    public string TimeText => $"{StartAt:tt h:mm} - {EndAt:tt h:mm}";
}
