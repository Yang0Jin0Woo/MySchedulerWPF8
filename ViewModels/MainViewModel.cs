using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Services;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ScheduleExportViewModel _exportViewModel;
    private bool _isDisposed;

    public ScheduleBrowserViewModel Browser { get; }
    public ScheduleCommandViewModel CommandState { get; }
    public ClockViewModel Clock { get; }
    public NotificationCenterViewModel NotificationCenter { get; }

    public MainViewModel(
        ITimeService timeService,
        IScheduleService scheduleService,
        IScheduleCsvService scheduleCsvService,
        IDialogService dialogService,
        IFileExportService fileExportService,
        IScheduleEditorDialogService scheduleEditorDialogService,
        ScheduleBrowserViewModel browserViewModel,
        ClockViewModel clockViewModel,
        NotificationCenterViewModel notificationCenterViewModel)
    {
        Browser = browserViewModel;
        Clock = clockViewModel;
        NotificationCenter = notificationCenterViewModel;

        CommandState = new ScheduleCommandViewModel(
            scheduleService,
            dialogService,
            scheduleEditorDialogService,
            Browser);

        _exportViewModel = new ScheduleExportViewModel(
            scheduleCsvService,
            fileExportService,
            dialogService,
            Browser);

        Browser.PropertyChanged += OnBrowserPropertyChanged;
        CommandState.PropertyChanged += OnCommandPropertyChanged;
        Browser.PagedSchedules.CollectionChanged += OnPagedSchedulesCollectionChanged;

        Browser.Initialize(timeService.GetKoreaNow().Date);

        Clock.Start();
        NotificationCenter.Start();
        _ = Browser.LoadSchedulesAsync();
    }

    private bool CanEditOrDelete()
        => !CommandState.IsBusy && !Browser.IsLoadingList && !Browser.IsLoadingDetail && Browser.SelectedScheduleDetail is not null;

    private bool CanExportCsv()
        => !CommandState.IsBusy && !Browser.IsLoadingList && Browser.PagedSchedules.Any();

    [RelayCommand]
    private void ApplySearch()
    {
        Browser.ApplySearch();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        Browser.ClearSearch();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task LoadSchedulesAsync(bool showLoading = true)
        => Browser.LoadSchedulesAsync(showLoading);

    [RelayCommand]
    private Task AddScheduleAsync()
        => CommandState.AddScheduleAsync();

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private Task EditScheduleAsync()
        => CommandState.EditScheduleAsync();

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private Task DeleteScheduleAsync()
        => CommandState.DeleteScheduleAsync();

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private Task ExportCsvAsync(CancellationToken cancellationToken)
        => _exportViewModel.ExportCsvAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanPrevPage))]
    private void PrevPage()
    {
        Browser.MovePrevPage();
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private void NextPage()
    {
        Browser.MoveNextPage();
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        Browser.GoToPage(page);
    }

    private bool CanPrevPage() => Browser.HasPrevPage;
    private bool CanNextPage() => Browser.HasNextPage;

    [RelayCommand]
    private async Task OpenNotificationAsync(NotificationItem item)
    {
        await NotificationCenter.OpenNotificationAsync(item, Browser.NavigateToScheduleAsync);
    }

    [RelayCommand]
    private void DismissNotification(NotificationItem item)
    {
        NotificationCenter.DismissNotification(item);
    }

    [RelayCommand]
    private async Task OpenGroupedNotificationAsync(NotificationGroupItem item)
    {
        await NotificationCenter.OpenGroupedNotificationAsync(item, Browser.NavigateToScheduleAsync);
    }

    [RelayCommand]
    private void CloseNotificationGroup()
    {
        NotificationCenter.CloseNotificationGroup();
    }

    private void OnPagedSchedulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    private void OnBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.IsLoadingList) ||
            e.PropertyName == nameof(ScheduleBrowserViewModel.IsLoadingDetail) ||
            e.PropertyName == nameof(ScheduleBrowserViewModel.SelectedScheduleDetail))
        {
            EditScheduleCommand.NotifyCanExecuteChanged();
            DeleteScheduleCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.IsLoadingList))
            ExportCsvCommand.NotifyCanExecuteChanged();

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.HasPrevPage))
            PrevPageCommand.NotifyCanExecuteChanged();

        if (e.PropertyName == nameof(ScheduleBrowserViewModel.HasNextPage))
            NextPageCommand.NotifyCanExecuteChanged();
    }

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScheduleCommandViewModel.IsBusy)) return;

        EditScheduleCommand.NotifyCanExecuteChanged();
        DeleteScheduleCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    public void StopClock()
    {
        Browser.PropertyChanged -= OnBrowserPropertyChanged;
        CommandState.PropertyChanged -= OnCommandPropertyChanged;
        Browser.PagedSchedules.CollectionChanged -= OnPagedSchedulesCollectionChanged;

        Clock.Stop();
        NotificationCenter.Stop();
        Browser.Stop();
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        StopClock();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
