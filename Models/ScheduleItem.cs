using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyScheduler.Models;

public class ScheduleItem
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public string? Location { get; set; }
    public string? Notes { get; set; }

    public int Priority { get; set; }

    public bool IsAllDay { get; set; } = false;

    // 동시성 토큰
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [NotMapped]
    public string TimeRangeText =>
        $"{ToKoreanDateTime(StartAt)} - {ToKoreanDateTime(EndAt)}";

    private static string ToKoreanDateTime(DateTime dt)
    {
        var meridiem = dt.Hour < 12 ? "오전" : "오후";
        var hour12 = dt.Hour % 12;
        if (hour12 == 0) hour12 = 12;

        return $"{dt:yyyy-MM-dd} {meridiem} {hour12:00}:{dt.Minute:00}";
    }
}
