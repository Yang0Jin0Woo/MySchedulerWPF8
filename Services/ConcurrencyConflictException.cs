using MyScheduler.Models;

namespace MyScheduler.Services;

public class ConcurrencyConflictException : Exception
{
    public ScheduleItem? Latest { get; }
    public bool IsDeleted { get; }

    public ConcurrencyConflictException(
        ScheduleItem? latest,
        bool isDeleted)
        : base("동시성 충돌이 발생했습니다.")
    {
        Latest = latest;
        IsDeleted = isDeleted;
    }
}
