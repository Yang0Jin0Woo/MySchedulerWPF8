using CommunityToolkit.Mvvm.ComponentModel;
using MyScheduler.Services;
using System;
using System.Windows.Threading;

namespace MyScheduler.ViewModels;

public partial class ClockViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;
    private readonly DispatcherTimer _clockTimer = new();

    [ObservableProperty]
    private DateTime nowKst;

    public ClockViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    public void Start()
    {
        _clockTimer.Stop();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick -= OnTick;
        _clockTimer.Tick += OnTick;

        NowKst = _scheduleService.UtcToKorea(DateTime.UtcNow);
        _clockTimer.Start();
    }

    public void Stop()
    {
        _clockTimer.Stop();
        _clockTimer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        NowKst = _scheduleService.UtcToKorea(DateTime.UtcNow);
    }
}
