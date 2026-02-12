using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IScheduleService _scheduleService;
    private readonly IDialogService _dialogService;
    private readonly IFileExportService _fileExportService;
    private readonly IScheduleEditorDialogService _scheduleEditorDialogService;

    private readonly ScheduleListStateViewModel _listState;
    private readonly ClockViewModel _clockViewModel;
    private readonly NotificationCenterViewModel _notificationCenterViewModel;

    public MainViewModel(
        IScheduleService scheduleService,
        IDialogService dialogService,
        IFileExportService fileExportService,
        IScheduleEditorDialogService scheduleEditorDialogService)
    {
        _scheduleService = scheduleService;
        _dialogService = dialogService;
        _fileExportService = fileExportService;
        _scheduleEditorDialogService = scheduleEditorDialogService;

        _listState = new ScheduleListStateViewModel();
        _clockViewModel = new ClockViewModel(_scheduleService);
        _notificationCenterViewModel = new NotificationCenterViewModel(_scheduleService, () => _clockViewModel.NowKst);

        _clockViewModel.PropertyChanged += OnClockPropertyChanged;
        _notificationCenterViewModel.PropertyChanged += OnNotificationCenterPropertyChanged;

        SelectedDate = _scheduleService.GetKoreaNow().Date;

        _clockViewModel.Start();
        _notificationCenterViewModel.Start();
        _ = LoadSchedulesAsync();
    }

    public ObservableCollection<ScheduleListItem> PagedSchedules { get; } = new();

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

    public ObservableCollection<string> SearchScopes => _listState.SearchScopes;

    public string SelectedSearchScope
    {
        get => _listState.SelectedSearchScope;
        set
        {
            if (_listState.SelectedSearchScope == value) return;
            _listState.SelectedSearchScope = value;
            OnPropertyChanged();
            ExportCsvCommand.NotifyCanExecuteChanged();
        }
    }

    public string SearchText
    {
        get => _listState.SearchText;
        set
        {
            if (_listState.SearchText == value) return;
            _listState.SearchText = value;
            OnPropertyChanged();
            ExportCsvCommand.NotifyCanExecuteChanged();
        }
    }

    private DateTime _selectedDate;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                if (!_suppressDateReload)
                    ResetToFirstPageAndReload();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DateTime NowKst => _clockViewModel.NowKst;

    private ScheduleListItem? _selectedSchedule;
    public ScheduleListItem? SelectedSchedule
    {
        get => _selectedSchedule;
        set
        {
            if (SetProperty(ref _selectedSchedule, value))
            {
                SelectedScheduleDetail = null;
                _ = LoadSelectedDetailAsync();
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private ScheduleItem? _selectedScheduleDetail;
    public ScheduleItem? SelectedScheduleDetail
    {
        get => _selectedScheduleDetail;
        set
        {
            if (SetProperty(ref _selectedScheduleDetail, value))
            {
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isLoadingList;
    public bool IsLoadingList
    {
        get => _isLoadingList;
        private set
        {
            if (SetProperty(ref _isLoadingList, value))
            {
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isLoadingDetail;
    public bool IsLoadingDetail
    {
        get => _isLoadingDetail;
        private set
        {
            if (SetProperty(ref _isLoadingDetail, value))
            {
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private int _listRequestVersion;
    private int _detailRequestVersion;
    private CancellationTokenSource? _listCts;
    private CancellationTokenSource? _detailCts;
    private bool _isDisposed;

    private const int PageSize = 10;

    public int CurrentPage => _listState.CurrentPage;
    public int TotalPages => _listState.TotalPages;
    public bool HasPrevPage => _listState.HasPrevPage;
    public bool HasNextPage => _listState.HasNextPage;
    public ObservableCollection<int> PageNumbers => _listState.PageNumbers;

    private bool CanEditOrDelete()
        => !IsBusy && !IsLoadingList && !IsLoadingDetail && SelectedScheduleDetail is not null;

    private bool CanExportCsv()
        => !IsBusy && !IsLoadingList && PagedSchedules.Any();

    private void ResetToFirstPageAndReload()
    {
        _listState.MoveToFirstPage();
        UpdatePagingUi();
        _ = LoadSchedulesAsync();
    }

    [RelayCommand]
    private void ApplySearch()
    {
        _listState.ApplySearch();
        UpdatePagingUi();
        _ = LoadSchedulesAsync();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        _listState.ClearSearch();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedSearchScope));
        UpdatePagingUi();
        _ = LoadSchedulesAsync();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadSchedulesAsync(bool showLoading = true)
    {
        var version = ++_listRequestVersion;
        _listCts?.Cancel();
        _listCts?.Dispose();
        var listCts = new CancellationTokenSource();
        _listCts = listCts;
        var token = listCts.Token;

        if (showLoading)
            IsLoadingList = true;

        var prevId = SelectedSchedule?.Id;
        var requestedPage = CurrentPage;
        var (appliedText, appliedScope) = _listState.GetAppliedSearch();

        try
        {
            var (items, totalCount) = await _scheduleService.GetListByDateAsync(
                SelectedDate,
                appliedText,
                appliedScope,
                CurrentPage,
                PageSize,
                token);
            if (version != _listRequestVersion) return;

            _listState.SetTotalCount(totalCount, PageSize);

            if (CurrentPage != requestedPage)
            {
                UpdatePagingUi();
                await LoadSchedulesAsync(showLoading);
                return;
            }

            PagedSchedules.Clear();
            foreach (var item in items)
                PagedSchedules.Add(item);

            if (PagedSchedules.Count == 0)
            {
                SelectedSchedule = null;
                SelectedScheduleDetail = null;
            }

            if (prevId is not null)
            {
                var restored = PagedSchedules.FirstOrDefault(x => x.Id == prevId);
                if (restored is not null)
                    SelectedSchedule = restored;
            }

            UpdatePagingUi();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (version == _listRequestVersion)
            {
                ShowUserError(
                    "목록 조회 실패",
                    BuildDbHint("일정 목록을 불러오는 작업") + $"\n\n오류: {ex.Message}");
            }
        }
        finally
        {
            if (showLoading && version == _listRequestVersion)
                IsLoadingList = false;

            if (version == _listRequestVersion)
                ExportCsvCommand.NotifyCanExecuteChanged();

            if (ReferenceEquals(_listCts, listCts))
            {
                _listCts?.Dispose();
                _listCts = null;
            }
        }
    }

    private async Task LoadSelectedDetailAsync()
    {
        if (SelectedSchedule is null)
        {
            SelectedScheduleDetail = null;
            IsLoadingDetail = false;
            return;
        }

        var version = ++_detailRequestVersion;
        _detailCts?.Cancel();
        _detailCts?.Dispose();
        var detailCts = new CancellationTokenSource();
        _detailCts = detailCts;
        var token = detailCts.Token;
        IsLoadingDetail = true;

        try
        {
            var detail = await _scheduleService.GetByIdAsync(SelectedSchedule.Id, token);
            if (version != _detailRequestVersion) return;

            SelectedScheduleDetail = detail;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (version == _detailRequestVersion)
            {
                ShowUserError(
                    "상세 조회 실패",
                    BuildDbHint("일정 상세를 불러오는 작업") + $"\n\n오류: {ex.Message}");
            }
        }
        finally
        {
            if (version == _detailRequestVersion)
                IsLoadingDetail = false;

            if (ReferenceEquals(_detailCts, detailCts))
            {
                _detailCts?.Dispose();
                _detailCts = null;
            }
        }
    }

    [RelayCommand]
    private async Task AddScheduleAsync()
    {
        if (IsBusy || IsLoadingList) return;

        IsBusy = true;

        try
        {
            if (!_scheduleEditorDialogService.TryOpen(SelectedDate, null, out var result) || result is null)
                return;

            var created = await _scheduleService.AddAsync(result);
            await LoadSchedulesAsync(false);

            SelectedSchedule = PagedSchedules.FirstOrDefault(x => x.Id == created.Id);
        }
        catch (Exception ex)
        {
            ShowUserError(
                "일정 추가 실패",
                BuildDbHint("일정을 추가하는 작업") + $"\n\n오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditScheduleAsync()
    {
        if (SelectedScheduleDetail is null) return;

        IsBusy = true;

        try
        {
            if (!_scheduleEditorDialogService.TryOpen(SelectedDate, SelectedScheduleDetail, out var result) || result is null)
                return;

            var updated = await _scheduleService.UpdateAsync(result);
            result.RowVersion = updated.RowVersion;
            await LoadSchedulesAsync(false);
            await LoadSelectedDetailAsync();
        }
        catch (ConcurrencyConflictException ex)
        {
            HandleConcurrencyConflict(ex);
        }
        catch (Exception ex)
        {
            ShowUserError(
                "일정 수정 실패",
                BuildDbHint("일정을 수정하는 작업") + $"\n\n오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteScheduleAsync()
    {
        if (SelectedScheduleDetail is null) return;

        var id = SelectedScheduleDetail.Id;
        var rowVersion = SelectedScheduleDetail.RowVersion;

        if (!_dialogService.Confirm(
                "정말 삭제하시겠습니까?",
                "삭제 확인"))
            return;

        IsBusy = true;

        try
        {
            if (rowVersion is null || rowVersion.Length == 0)
                throw new ConcurrencyConflictException(null, true);

            await _scheduleService.DeleteAsync(id, rowVersion);

            SelectedSchedule = null;
            SelectedScheduleDetail = null;

            await LoadSchedulesAsync(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            HandleConcurrencyConflict(ex);
        }
        catch (Exception ex)
        {
            ShowUserError(
                "일정 삭제 실패",
                BuildDbHint("일정을 삭제하는 작업") + $"\n\n오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        var rows = PagedSchedules.ToList();

        if (rows.Count == 0)
        {
            _dialogService.ShowInfo(
                "내보낼 일정이 없습니다.\n(검색/필터 결과가 비어있을 수 있습니다.)",
                "CSV 내보내기");
            return;
        }

        var fileName = _dialogService.ShowSaveCsvDialog($"MyScheduler_{SelectedDate:yyyyMMdd}.csv");
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            var bytes = _scheduleService.BuildCsvBytes(rows);
            await _fileExportService.WriteAllBytesAsync(fileName, bytes, cancellationToken);

            _dialogService.ShowInfo(
                $"CSV 저장이 완료되었습니다.\n\n{fileName}",
                "CSV 내보내기");
        }
        catch (OperationCanceledException)
        {
            _dialogService.ShowInfo(
                "CSV 저장이 취소되었습니다.",
                "CSV 내보내기");
        }
        catch (Exception ex)
        {
            ShowUserError(
                "CSV 내보내기 실패",
                "CSV 파일 저장 중 문제가 발생했습니다.\n\n" +
                "확인해볼 수 있는 항목:\n" +
                "1) 저장 경로 권한(쓰기 가능 여부)\n" +
                "2) 파일이 다른 프로그램에서 열려있는지\n" +
                "3) 디스크 용량\n\n" +
                $"오류: {ex.Message}");
        }
    }

    private void HandleConcurrencyConflict(ConcurrencyConflictException ex)
    {
        if (ex.IsDeleted)
        {
            _dialogService.ShowWarning(
                "해당 일정은 이미 삭제되었습니다.\n목록을 최신 상태로 갱신합니다.",
                "동시성 충돌");

            SelectedSchedule = null;
            SelectedScheduleDetail = null;
            _ = LoadSchedulesAsync();
            return;
        }

        if (ex.Latest is not null)
        {
            _dialogService.ShowWarning(
                "다른 곳에서 일정이 수정되었습니다.\n최신 데이터로 갱신합니다.",
                "동시성 충돌");

            SelectedScheduleDetail = ex.Latest;
            _ = LoadSchedulesAsync();
        }
    }

    private void ShowUserError(string title, string message)
    {
        _dialogService.ShowError(title, message);
    }

    private static string BuildDbHint(string action)
    {
        return
            $"{action} 중 문제가 발생했습니다.\n\n" +
            "확인해볼 수 있는 항목:\n" +
            "1) SQL Server가 실행 중인지\n" +
            "2) appsettings.json 연결 문자열이 올바른지\n" +
            "3) 권한 문제 여부\n\n" +
            "조치 방법:\n" +
            "- Windows 서비스에서 'SQL Server(SQLEXPRESS)' 실행 확인\n" +
            "- SSMS로 localhost\\SQLEXPRESS 접속 테스트\n" +
            "- appsettings.json의 ConnectionStrings:Default 확인";
    }

    public void StopClock()
    {
        _clockViewModel.PropertyChanged -= OnClockPropertyChanged;
        _notificationCenterViewModel.PropertyChanged -= OnNotificationCenterPropertyChanged;

        _clockViewModel.Stop();
        _notificationCenterViewModel.Stop();

        _listCts?.Cancel();
        _listCts?.Dispose();
        _listCts = null;

        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        StopClock();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    [RelayCommand(CanExecute = nameof(HasPrevPage))]
    private void PrevPage()
    {
        if (!_listState.MovePrevPage()) return;
        UpdatePagingUi();
        _ = LoadSchedulesAsync(false);
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void NextPage()
    {
        if (!_listState.MoveNextPage()) return;
        UpdatePagingUi();
        _ = LoadSchedulesAsync(false);
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        if (!_listState.GoToPage(page)) return;
        UpdatePagingUi();
        _ = LoadSchedulesAsync(false);
    }

    private void UpdatePagingUi()
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPrevPage));
        OnPropertyChanged(nameof(HasNextPage));
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
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

    private void OnClockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockViewModel.NowKst))
            OnPropertyChanged(nameof(NowKst));
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

    private bool _suppressDateReload;

    private async Task NavigateToScheduleAsync(int scheduleId, DateTime date)
    {
        _suppressDateReload = true;
        SelectedDate = date.Date;
        _suppressDateReload = false;

        await LoadSchedulesAsync(false);

        var match = PagedSchedules.FirstOrDefault(x => x.Id == scheduleId);
        if (match is not null)
            SelectedSchedule = match;
    }
}
