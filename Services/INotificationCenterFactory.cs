using MyScheduler.ViewModels;
using System;

namespace MyScheduler.Services;

public interface INotificationCenterFactory
{
    NotificationCenterViewModel Create(Func<DateTime> nowProvider);
}
