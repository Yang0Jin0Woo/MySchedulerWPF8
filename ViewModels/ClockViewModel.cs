using CommunityToolkit.Mvvm.ComponentModel;
using MyScheduler.Services;
using System;
using System.Windows.Threading;

namespace MyScheduler.ViewModels;

public partial class ClockViewModel : ObservableObject
{
    private readonly ITimeService _timeService;
    private readonly DispatcherTimer _clockTimer = new();

    [ObservableProperty]
    private DateTime nowKst;

    public ClockViewModel(ITimeService timeService)
    {
        _timeService = timeService;
    }

    public void Start()
    {
        _clockTimer.Stop();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick -= OnTick;
        _clockTimer.Tick += OnTick;

        NowKst = _timeService.GetKoreaNow();
        _clockTimer.Start();
    }

    public void Stop()
    {
        _clockTimer.Stop();
        _clockTimer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        NowKst = _timeService.GetKoreaNow();
    }
}
