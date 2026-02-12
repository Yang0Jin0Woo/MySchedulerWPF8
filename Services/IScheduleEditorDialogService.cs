using MyScheduler.Models;
using System;

namespace MyScheduler.Services;

public interface IScheduleEditorDialogService
{
    bool TryOpen(DateTime baseDate, ScheduleItem? existing, out ScheduleItem? result);
}
