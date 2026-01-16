namespace MyScheduler.Models;

public class ScheduleListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? Location { get; set; }

    public string TimeRangeText =>
        $"{ToKoreanTime(StartAt)} - {ToKoreanTime(EndAt)}";

    private static string ToKoreanTime(DateTime dt)
    {
        var meridiem = dt.Hour < 12 ? "오전" : "오후";
        var hour12 = dt.Hour % 12;
        if (hour12 == 0) hour12 = 12;

        return $"{meridiem} {hour12:00}:{dt.Minute:00}";
    }
}
