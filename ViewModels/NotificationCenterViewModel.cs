using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MyScheduler.Models;
using MyScheduler.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MyScheduler.ViewModels;

public partial class NotificationCenterViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;
    private readonly ITimeService _timeService;
    private readonly ILogger<NotificationCenterViewModel> _logger;
    private readonly DispatcherTimer _notificationTimer = new();

    private bool _isNotificationScanning;
    private readonly Dictionary<string, DateTime> _notifiedKeys = new();
    private bool _startupNotificationShown;
    private CancellationTokenSource _notificationCts = new();
    private NotificationItem? _activeGroupSource;
    private bool _notificationTimerAligned;

    private const int NotificationLeadMinutes = 10;
    private static readonly TimeSpan NotificationScanInterval = TimeSpan.FromMinutes(5);
    private const int MaxActiveNotifications = 20;

    public ObservableCollection<NotificationItem> ActiveNotifications { get; } = new();
    public ObservableCollection<NotificationGroupItem> NotificationGroupItems { get; } = new();

    [ObservableProperty]
    private bool isNotificationGroupOpen;

    public IEnumerable<NotificationItem> DisplayNotifications => ActiveNotifications.Take(3);
    public int OverflowCount => Math.Max(0, ActiveNotifications.Count - 3);
    public bool HasOverflow => OverflowCount > 0;

    public NotificationCenterViewModel(
        IScheduleService scheduleService,
        ITimeService timeService,
        ILogger<NotificationCenterViewModel> logger)
    {
        _scheduleService = scheduleService;
        _timeService = timeService;
        _logger = logger;
    }

    public void Start()
    {
        if (_notificationCts.IsCancellationRequested)
        {
            _notificationCts.Dispose();
            _notificationCts = new CancellationTokenSource();
        }

        ActiveNotifications.CollectionChanged -= OnActiveNotificationsCollectionChanged;
        ActiveNotifications.CollectionChanged += OnActiveNotificationsCollectionChanged;

        _notificationTimer.Stop();
        _notificationTimerAligned = false;
        _notificationTimer.Tick -= NotificationTimerTick;
        _notificationTimer.Tick += NotificationTimerTick;
        _notificationTimer.Interval = GetNotificationAlignDelay(_timeService.GetKoreaNow());
        _notificationTimer.Start();

        _ = ScanNotificationsAsync();
        _ = ShowStartupNotificationAsync();
    }

    public void Stop()
    {
        _notificationTimer.Stop();
        _notificationTimer.Tick -= NotificationTimerTick;
        ActiveNotifications.CollectionChanged -= OnActiveNotificationsCollectionChanged;

        if (!_notificationCts.IsCancellationRequested)
            _notificationCts.Cancel();

        _notificationCts.Dispose();
        _notificationCts = new CancellationTokenSource();
    }

    public async Task OpenNotificationAsync(NotificationItem item, Func<int, DateTime, Task> navigateAsync)
    {
        if (item is null) return;

        if (item.HasGroup)
        {
            ShowNotificationGroup(item);
            return;
        }

        await navigateAsync(item.ScheduleId, item.StartAt.Date);
        DismissNotification(item);
    }

    public void DismissNotification(NotificationItem item)
    {
        if (item is null) return;

        if (ActiveNotifications.Contains(item))
            ActiveNotifications.Remove(item);

        if (ReferenceEquals(_activeGroupSource, item))
            CloseNotificationGroup();
    }

    public async Task OpenGroupedNotificationAsync(NotificationGroupItem item, Func<int, DateTime, Task> navigateAsync)
    {
        if (item is null) return;

        var groupSource = _activeGroupSource;
        CloseNotificationGroup();

        if (groupSource is not null)
            DismissNotification(groupSource);

        await navigateAsync(item.ScheduleId, item.StartAt.Date);
    }

    public void CloseNotificationGroup()
    {
        _activeGroupSource = null;
        NotificationGroupItems.Clear();
        IsNotificationGroupOpen = false;
    }

    private void OnActiveNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayNotifications));
        OnPropertyChanged(nameof(OverflowCount));
        OnPropertyChanged(nameof(HasOverflow));
    }

    private static TimeSpan GetNotificationAlignDelay(DateTime now)
    {
        var remainderMs = ((now.Second % 30) * 1000) + now.Millisecond;
        var delayMs = (30000 - remainderMs) % 30000;
        return TimeSpan.FromMilliseconds(delayMs);
    }

    private async void NotificationTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (!_notificationTimerAligned)
            {
                _notificationTimerAligned = true;
                _notificationTimer.Interval = NotificationScanInterval;
            }

            await ScanNotificationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "알림 타이머 처리 중 오류가 발생했습니다.");
        }
    }

    private void ShowNotificationGroup(NotificationItem item)
    {
        _activeGroupSource = item;
        NotificationGroupItems.Clear();

        foreach (var related in item.RelatedItems)
            NotificationGroupItems.Add(related);

        IsNotificationGroupOpen = NotificationGroupItems.Count > 0;
    }

    private async Task ScanNotificationsAsync()
    {
        if (_isNotificationScanning) return;
        _isNotificationScanning = true;

        try
        {
            var now = _timeService.GetKoreaNow();
            PruneNotifiedKeys(now);
            PruneActiveNotifications(now);

            var targetStart = now;
            var targetEnd = now.AddMinutes(NotificationLeadMinutes);

            var upcoming = await _scheduleService.GetStartingInRangeAsync(
                targetStart,
                targetEnd,
                _notificationCts.Token);

            var fresh = upcoming
                .Where(x => !_notifiedKeys.ContainsKey(BuildNotificationKey(x.Id, x.StartAt)))
                .OrderBy(x => x.StartAt)
                .ThenBy(x => x.Id)
                .ToList();

            if (fresh.Count == 0) return;

            if (TryMergeIntoLatestNotification(fresh, targetStart, targetEnd))
            {
                MarkNotifiedKeys(fresh, now);
                return;
            }

            var groupItems = BuildGroupItems(fresh);
            var first = fresh[0];
            var additional = Math.Max(0, fresh.Count - 1);
            AddNotification(first, now, targetStart, targetEnd, markNotified: true, additional, groupItems);
            MarkNotifiedKeys(fresh.Skip(1), now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "알림 스캔 중 오류가 발생했습니다.");
        }
        finally
        {
            _isNotificationScanning = false;
        }
    }

    private async Task ShowStartupNotificationAsync()
    {
        if (_startupNotificationShown) return;
        _startupNotificationShown = true;

        try
        {
            var now = _timeService.GetKoreaNow();
            var upcoming = await _scheduleService.GetStartingInRangeAsync(
                now,
                now.AddMinutes(NotificationLeadMinutes),
                _notificationCts.Token);

            var fresh = upcoming
                .Where(x => !_notifiedKeys.ContainsKey(BuildNotificationKey(x.Id, x.StartAt)))
                .OrderBy(x => x.StartAt)
                .ThenBy(x => x.Id)
                .ToList();

            var first = fresh.FirstOrDefault();
            if (first is null) return;

            var additional = Math.Max(0, fresh.Count - 1);
            var groupItems = BuildGroupItems(fresh);
            AddNotification(first, now, now, now.AddMinutes(NotificationLeadMinutes), markNotified: true, additional, groupItems);
            MarkNotifiedKeys(fresh.Skip(1), now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "시작 알림 조회 중 오류가 발생했습니다.");
        }
    }

    private void AddNotification(
        ScheduleListItem item,
        DateTime now,
        DateTime windowStart,
        DateTime windowEnd,
        bool markNotified,
        int additionalCount,
        IReadOnlyList<NotificationGroupItem> relatedItems)
    {
        var key = BuildNotificationKey(item.Id, item.StartAt);
        if (_notifiedKeys.ContainsKey(key)) return;

        if (markNotified)
            _notifiedKeys[key] = now;

        ActiveNotifications.Insert(0, new NotificationItem
        {
            ScheduleId = item.Id,
            Title = item.Title,
            StartAt = item.StartAt,
            EndAt = item.EndAt,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            AccentBrush = PickAccentBrush(now, item.StartAt),
            AdditionalCount = additionalCount,
            RelatedItems = relatedItems
        });

        while (ActiveNotifications.Count > MaxActiveNotifications)
            ActiveNotifications.RemoveAt(ActiveNotifications.Count - 1);
    }

    private bool TryMergeIntoLatestNotification(IReadOnlyList<ScheduleListItem> fresh, DateTime windowStart, DateTime windowEnd)
    {
        if (ActiveNotifications.Count == 0) return false;

        var existing = ActiveNotifications[0];
        if (existing.WindowStart != windowStart || existing.WindowEnd != windowEnd)
            return false;

        var merged = MergeNotificationItems(existing, fresh);
        ActiveNotifications.RemoveAt(0);
        ActiveNotifications.Insert(0, merged);

        if (ReferenceEquals(_activeGroupSource, existing))
        {
            _activeGroupSource = merged;
            RefreshNotificationGroup(merged);
        }

        return true;
    }

    private static NotificationItem MergeNotificationItems(NotificationItem existing, IReadOnlyList<ScheduleListItem> fresh)
    {
        var map = new Dictionary<string, NotificationGroupItem>();

        foreach (var item in ToGroupItems(existing))
            map[BuildNotificationKey(item.ScheduleId, item.StartAt)] = item;

        foreach (var item in fresh)
        {
            var key = BuildNotificationKey(item.Id, item.StartAt);
            if (!map.ContainsKey(key))
            {
                map[key] = new NotificationGroupItem
                {
                    ScheduleId = item.Id,
                    Title = item.Title,
                    StartAt = item.StartAt,
                    EndAt = item.EndAt
                };
            }
        }

        var list = map.Values.OrderBy(x => x.StartAt).ToList();
        var additional = Math.Max(0, list.Count - 1);

        return new NotificationItem
        {
            ScheduleId = existing.ScheduleId,
            Title = existing.Title,
            StartAt = existing.StartAt,
            EndAt = existing.EndAt,
            WindowStart = existing.WindowStart,
            WindowEnd = existing.WindowEnd,
            AccentBrush = existing.AccentBrush,
            AdditionalCount = additional,
            RelatedItems = list
        };
    }

    private static IReadOnlyList<NotificationGroupItem> ToGroupItems(NotificationItem item)
    {
        if (item.RelatedItems.Count > 0)
            return item.RelatedItems;

        return new[]
        {
            new NotificationGroupItem
            {
                ScheduleId = item.ScheduleId,
                Title = item.Title,
                StartAt = item.StartAt,
                EndAt = item.EndAt
            }
        };
    }

    private void RefreshNotificationGroup(NotificationItem item)
    {
        if (!IsNotificationGroupOpen) return;

        NotificationGroupItems.Clear();
        foreach (var related in item.RelatedItems)
            NotificationGroupItems.Add(related);
    }

    private static string BuildNotificationKey(int scheduleId, DateTime startAt)
        => $"{scheduleId}:{startAt.Ticks}";

    private void MarkNotifiedKeys(IEnumerable<ScheduleListItem> items, DateTime now)
    {
        foreach (var item in items)
        {
            var key = BuildNotificationKey(item.Id, item.StartAt);
            if (!_notifiedKeys.ContainsKey(key))
                _notifiedKeys[key] = now;
        }
    }

    private void PruneNotifiedKeys(DateTime now)
    {
        if (_notifiedKeys.Count == 0) return;

        var threshold = now.AddHours(-6);
        var expired = _notifiedKeys.Where(x => x.Value < threshold).Select(x => x.Key).ToList();
        foreach (var key in expired)
            _notifiedKeys.Remove(key);
    }

    private void PruneActiveNotifications(DateTime now)
    {
        if (ActiveNotifications.Count == 0) return;

        var threshold = now.AddMinutes(-1);
        for (var i = ActiveNotifications.Count - 1; i >= 0; i--)
        {
            if (ActiveNotifications[i].StartAt < threshold)
                ActiveNotifications.RemoveAt(i);
        }
    }

    private static System.Windows.Media.Brush PickAccentBrush(DateTime now, DateTime startAt)
    {
        var minutesLeft = (startAt - now).TotalMinutes;

        if (minutesLeft <= 2)
            return System.Windows.Media.Brushes.OrangeRed;

        if (minutesLeft <= 5)
            return System.Windows.Media.Brushes.Orange;

        if (minutesLeft <= 10)
            return System.Windows.Media.Brushes.DodgerBlue;

        return System.Windows.Media.Brushes.SlateGray;
    }

    private static IReadOnlyList<NotificationGroupItem> BuildGroupItems(IReadOnlyList<ScheduleListItem> items)
    {
        var list = new List<NotificationGroupItem>(items.Count);
        foreach (var item in items)
        {
            list.Add(new NotificationGroupItem
            {
                ScheduleId = item.Id,
                Title = item.Title,
                StartAt = item.StartAt,
                EndAt = item.EndAt
            });
        }

        return list;
    }
}
