using System.ComponentModel.DataAnnotations;

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

    // 동시성 방지 - 낙관적 동시성 토큰
    // 같은 레코드를 여러 사용자가 동시에 수정할 때 마지막 저장이 덮어쓰는 문제 방지
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
