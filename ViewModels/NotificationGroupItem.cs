namespace MyScheduler.ViewModels;

public class NotificationGroupItem
{
    public int ScheduleId { get; init; }
    public string Title { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }

    public string TimeText => $"{StartAt:tt h:mm} - {EndAt:tt h:mm}";
}
