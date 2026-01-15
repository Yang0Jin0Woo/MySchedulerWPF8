namespace MyScheduler.Models;

public class ScheduleListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}
