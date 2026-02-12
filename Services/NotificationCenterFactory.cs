using MyScheduler.ViewModels;
using System;

namespace MyScheduler.Services;

public class NotificationCenterFactory : INotificationCenterFactory
{
    private readonly IScheduleService _scheduleService;

    public NotificationCenterFactory(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    public NotificationCenterViewModel Create(Func<DateTime> nowProvider)
    {
        return new NotificationCenterViewModel(_scheduleService, nowProvider);
    }
}
