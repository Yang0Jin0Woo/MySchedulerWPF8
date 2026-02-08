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

}
