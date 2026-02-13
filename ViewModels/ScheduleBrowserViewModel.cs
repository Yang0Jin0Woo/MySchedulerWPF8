using CommunityToolkit.Mvvm.ComponentModel;
using MyScheduler.Models;
using MyScheduler.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace MyScheduler.ViewModels;

public partial class ScheduleBrowserViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;
    private readonly IDialogService _dialogService;
    private readonly ScheduleListStateViewModel _listState;

    private int _listRequestVersion;
    private int _detailRequestVersion;
    private CancellationTokenSource? _listCts;
    private CancellationTokenSource? _detailCts;
    private bool _suppressDateReload;

    private const int PageSize = 10;

    public ObservableCollection<ScheduleListItem> PagedSchedules { get; } = new();
    public ObservableCollection<string> SearchScopes => _listState.SearchScopes;
    public ObservableCollection<int> PageNumbers => _listState.PageNumbers;

    public int CurrentPage => _listState.CurrentPage;
    public int TotalPages => _listState.TotalPages;
    public bool HasPrevPage => _listState.HasPrevPage;
    public bool HasNextPage => _listState.HasNextPage;

    public string SelectedSearchScope
    {
        get => _listState.SelectedSearchScope;
        set
        {
            if (_listState.SelectedSearchScope == value) return;
            _listState.SelectedSearchScope = value;
            OnPropertyChanged();
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
        }
    }

    [ObservableProperty]
    private DateTime selectedDate;

    [ObservableProperty]
    private ScheduleListItem? selectedSchedule;

    [ObservableProperty]
    private ScheduleItem? selectedScheduleDetail;

    [ObservableProperty]
    private bool isLoadingList;

    [ObservableProperty]
    private bool isLoadingDetail;

    public ScheduleBrowserViewModel(
        IScheduleService scheduleService,
        IDialogService dialogService,
        ScheduleListStateViewModel listState)
    {
        _scheduleService = scheduleService;
        _dialogService = dialogService;
        _listState = listState;
    }

    public void Initialize(DateTime initialDate)
    {
        _suppressDateReload = true;
        SelectedDate = initialDate;
        _suppressDateReload = false;
        UpdatePagingUi();
    }

    public void ApplySearch()
    {
        _listState.ApplySearch();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedSearchScope));
        UpdatePagingUi();
        _ = LoadSchedulesAsync();
    }

    public void ClearSearch()
    {
        _listState.ClearSearch();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedSearchScope));
        UpdatePagingUi();
        _ = LoadSchedulesAsync();
    }

    public bool MovePrevPage()
    {
        if (!_listState.MovePrevPage()) return false;
        UpdatePagingUi();
        _ = LoadSchedulesAsync(false);
        return true;
    }

    public bool MoveNextPage()
    {
        if (!_listState.MoveNextPage()) return false;
        UpdatePagingUi();
        _ = LoadSchedulesAsync(false);
        return true;
    }

    public bool GoToPage(int page)
    {
        if (!_listState.GoToPage(page)) return false;
        UpdatePagingUi();
        _ = LoadSchedulesAsync(false);
        return true;
    }

    public async Task LoadSchedulesAsync(bool showLoading = true)
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

            if (ReferenceEquals(_listCts, listCts))
            {
                _listCts?.Dispose();
                _listCts = null;
            }
        }
    }

    public async Task NavigateToScheduleAsync(int scheduleId, DateTime date)
    {
        _suppressDateReload = true;
        SelectedDate = date.Date;
        _suppressDateReload = false;
        OnPropertyChanged(nameof(SelectedDate));

        await LoadSchedulesAsync(false);

        var match = PagedSchedules.FirstOrDefault(x => x.Id == scheduleId);
        if (match is not null)
            SelectedSchedule = match;
    }

    public void Stop()
    {
        _listCts?.Cancel();
        _listCts?.Dispose();
        _listCts = null;

        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = null;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (!_suppressDateReload)
        {
            _listState.MoveToFirstPage();
            UpdatePagingUi();
            _ = LoadSchedulesAsync();
        }
    }

    partial void OnSelectedScheduleChanged(ScheduleListItem? value)
    {
        SelectedScheduleDetail = null;
        _ = LoadSelectedDetailAsync();
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

    private void UpdatePagingUi()
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPrevPage));
        OnPropertyChanged(nameof(HasNextPage));
    }
}
