using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ITimeService _timeService;
    private readonly ScheduleBrowserViewModel _browserViewModel;
    private readonly ScheduleCommandViewModel _commandViewModel;
    private readonly ScheduleExportViewModel _exportViewModel;
    private readonly ClockViewModel _clockViewModel;
    private readonly NotificationCenterViewModel _notificationCenterViewModel;

    private bool _isDisposed;

    public MainViewModel(
        ITimeService timeService,
        ScheduleBrowserViewModel browserViewModel,
        ScheduleCommandViewModel commandViewModel,
        ScheduleExportViewModel exportViewModel,
        ClockViewModel clockViewModel,
        NotificationCenterViewModel notificationCenterViewModel)
    {
        _timeService = timeService;
        _browserViewModel = browserViewModel;
        _commandViewModel = commandViewModel;
        _exportViewModel = exportViewModel;
        _clockViewModel = clockViewModel;
        _notificationCenterViewModel = notificationCenterViewModel;

        _clockViewModel.PropertyChanged += OnClockPropertyChanged;
        _notificationCenterViewModel.PropertyChanged += OnNotificationCenterPropertyChanged;
        _browserViewModel.PropertyChanged += OnBrowserPropertyChanged;
        _commandViewModel.PropertyChanged += OnCommandPropertyChanged;
        _browserViewModel.PagedSchedules.CollectionChanged += OnPagedSchedulesCollectionChanged;

        _browserViewModel.Initialize(_timeService.GetKoreaNow().Date);

        _clockViewModel.Start();
        _notificationCenterViewModel.Start();
        _ = _browserViewModel.LoadSchedulesAsync();
    }

    public ObservableCollection<ScheduleListItem> PagedSchedules => _browserViewModel.PagedSchedules;

    public ObservableCollection<NotificationItem> ActiveNotifications => _notificationCenterViewModel.ActiveNotifications;
    public ObservableCollection<NotificationGroupItem> NotificationGroupItems => _notificationCenterViewModel.NotificationGroupItems;

    public bool IsNotificationGroupOpen
    {
        get => _notificationCenterViewModel.IsNotificationGroupOpen;
        set
        {
            if (_notificationCenterViewModel.IsNotificationGroupOpen == value) return;
            _notificationCenterViewModel.IsNotificationGroupOpen = value;
            OnPropertyChanged();
        }
    }

    public IEnumerable<NotificationItem> DisplayNotifications => _notificationCenterViewModel.DisplayNotifications;
    public int OverflowCount => _notificationCenterViewModel.OverflowCount;
    public bool HasOverflow => _notificationCenterViewModel.HasOverflow;

    public ObservableCollection<string> SearchScopes => _browserViewModel.SearchScopes;

    public string SelectedSearchScope
    {
        get => _browserViewModel.SelectedSearchScope;
        set => _browserViewModel.SelectedSearchScope = value;
    }

    public string SearchText
    {
        get => _browserViewModel.SearchText;
        set => _browserViewModel.SearchText = value;
    }

    public DateTime SelectedDate
    {
        get => _browserViewModel.SelectedDate;
        set => _browserViewModel.SelectedDate = value;
    }

    public DateTime NowKst => _clockViewModel.NowKst;

    public ScheduleListItem? SelectedSchedule
    {
        get => _browserViewModel.SelectedSchedule;
        set => _browserViewModel.SelectedSchedule = value;
    }

    public ScheduleItem? SelectedScheduleDetail
    {
        get => _browserViewModel.SelectedScheduleDetail;
        set => _browserViewModel.SelectedScheduleDetail = value;
    }

    public bool IsLoadingList => _browserViewModel.IsLoadingList;
    public bool IsLoadingDetail => _browserViewModel.IsLoadingDetail;
    public bool IsBusy => _commandViewModel.IsBusy;

    public int CurrentPage => _browserViewModel.CurrentPage;
    public int TotalPages => _browserViewModel.TotalPages;
    public bool HasPrevPage => _browserViewModel.HasPrevPage;
    public bool HasNextPage => _browserViewModel.HasNextPage;
    public ObservableCollection<int> PageNumbers => _browserViewModel.PageNumbers;

    private bool CanEditOrDelete()
        => !IsBusy && !IsLoadingList && !IsLoadingDetail && SelectedScheduleDetail is not null;

    private bool CanExportCsv()
        => !IsBusy && !IsLoadingList && PagedSchedules.Any();

    [RelayCommand]
    private void ApplySearch()
    {
        _browserViewModel.ApplySearch();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        _browserViewModel.ClearSearch();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task LoadSchedulesAsync(bool showLoading = true)
        => _browserViewModel.LoadSchedulesAsync(showLoading);

    [RelayCommand]
    private Task AddScheduleAsync()
        => _commandViewModel.AddScheduleAsync();

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private Task EditScheduleAsync()
        => _commandViewModel.EditScheduleAsync();

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private Task DeleteScheduleAsync()
        => _commandViewModel.DeleteScheduleAsync();

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private Task ExportCsvAsync(CancellationToken cancellationToken)
        => _exportViewModel.ExportCsvAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(HasPrevPage))]
    private void PrevPage()
    {
        _browserViewModel.MovePrevPage();
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void NextPage()
    {
        _browserViewModel.MoveNextPage();
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        _browserViewModel.GoToPage(page);
    }

    [RelayCommand]
    private async Task OpenNotificationAsync(NotificationItem item)
    {
        await _notificationCenterViewModel.OpenNotificationAsync(item, NavigateToScheduleAsync);
    }

    [RelayCommand]
    private void DismissNotification(NotificationItem item)
    {
        _notificationCenterViewModel.DismissNotification(item);
    }

    [RelayCommand]
    private async Task OpenGroupedNotificationAsync(NotificationGroupItem item)
    {
        await _notificationCenterViewModel.OpenGroupedNotificationAsync(item, NavigateToScheduleAsync);
    }

    [RelayCommand]
    private void CloseNotificationGroup()
    {
        _notificationCenterViewModel.CloseNotificationGroup();
    }

    private void OnPagedSchedulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    private void OnClockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockViewModel.NowKst))
            OnPropertyChanged(nameof(NowKst));
    }

    private void OnBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        OnPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.IsLoadingList) ||
            e.PropertyName == nameof(ScheduleBrowserViewModel.IsLoadingDetail) ||
            e.PropertyName == nameof(ScheduleBrowserViewModel.SelectedScheduleDetail))
        {
            EditScheduleCommand.NotifyCanExecuteChanged();
            DeleteScheduleCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.IsLoadingList))
            ExportCsvCommand.NotifyCanExecuteChanged();

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.HasPrevPage) ||
            e.PropertyName == nameof(ScheduleBrowserViewModel.HasNextPage))
        {
            PrevPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScheduleCommandViewModel.IsBusy)) return;

        OnPropertyChanged(nameof(IsBusy));
        EditScheduleCommand.NotifyCanExecuteChanged();
        DeleteScheduleCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    private void OnNotificationCenterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotificationCenterViewModel.IsNotificationGroupOpen))
            OnPropertyChanged(nameof(IsNotificationGroupOpen));

        if (e.PropertyName == nameof(NotificationCenterViewModel.DisplayNotifications))
            OnPropertyChanged(nameof(DisplayNotifications));

        if (e.PropertyName == nameof(NotificationCenterViewModel.OverflowCount))
            OnPropertyChanged(nameof(OverflowCount));

        if (e.PropertyName == nameof(NotificationCenterViewModel.HasOverflow))
            OnPropertyChanged(nameof(HasOverflow));
    }

    private Task NavigateToScheduleAsync(int scheduleId, DateTime date)
        => _browserViewModel.NavigateToScheduleAsync(scheduleId, date);

    public void StopClock()
    {
        _clockViewModel.PropertyChanged -= OnClockPropertyChanged;
        _notificationCenterViewModel.PropertyChanged -= OnNotificationCenterPropertyChanged;
        _browserViewModel.PropertyChanged -= OnBrowserPropertyChanged;
        _commandViewModel.PropertyChanged -= OnCommandPropertyChanged;
        _browserViewModel.PagedSchedules.CollectionChanged -= OnPagedSchedulesCollectionChanged;

        _clockViewModel.Stop();
        _notificationCenterViewModel.Stop();
        _browserViewModel.Stop();
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        StopClock();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

