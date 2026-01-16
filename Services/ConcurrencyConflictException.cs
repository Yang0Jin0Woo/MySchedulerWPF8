using System;
using MyScheduler.Models;

namespace MyScheduler.Services;

public class ConcurrencyConflictException : Exception
{
    public ScheduleItem? Latest { get; }
    public bool IsDeleted { get; }

    public ConcurrencyConflictException(string message, ScheduleItem? latest, bool isDeleted)
        : base(message)
    {
        Latest = latest;
        IsDeleted = isDeleted;
    }
}
