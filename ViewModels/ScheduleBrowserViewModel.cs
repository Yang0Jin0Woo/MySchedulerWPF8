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
    private int _navigateRequestVersion;
    private CancellationTokenSource? _listCts;
    private CancellationTokenSource? _detailCts;
    private CancellationTokenSource? _navigateCts;
    private bool _suppressDateReload;
    private bool _suppressSelectionDetailReload;
    private bool _preserveDetailOnEmptyList;
    private Task _scheduledLoadTask = Task.CompletedTask;
    private Task _scheduledDetailTask = Task.CompletedTask;

    private const int PageSize = 10;

    public ObservableCollection<ScheduleListItem> PagedSchedules { get; } = new();
    public ObservableCollection<string> SearchScopes => _listState.SearchScopes;
    public ObservableCollection<int> PageNumbers => _listState.PageNumbers;
    internal Task ScheduledLoadTask => _scheduledLoadTask;
    internal Task ScheduledDetailTask => _scheduledDetailTask;

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

    [ObservableProperty]
    private bool isNavigating;

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
        ScheduleLoad();
    }

    public void ClearSearch()
    {
        _listState.ClearSearch();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedSearchScope));
        UpdatePagingUi();
        ScheduleLoad();
    }

    public bool MovePrevPage()
    {
        if (!_listState.MovePrevPage()) return false;
        UpdatePagingUi();
        ScheduleLoad(false);
        return true;
    }

    public bool MoveNextPage()
    {
        if (!_listState.MoveNextPage()) return false;
        UpdatePagingUi();
        ScheduleLoad(false);
        return true;
    }

    public bool GoToPage(int page)
    {
        if (!_listState.GoToPage(page)) return false;
        UpdatePagingUi();
        ScheduleLoad(false);
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
                if (!_preserveDetailOnEmptyList)
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
                    UserMessageTemplates.BuildDbHint("일정 목록을 불러오는 작업") + $"\n\n오류: {ex.Message}");
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

    public async Task NavigateToScheduleAsync(int scheduleId)
    {
        var version = ++_navigateRequestVersion;
        _navigateCts?.Cancel();
        _navigateCts?.Dispose();
        var navigateCts = new CancellationTokenSource();
        _navigateCts = navigateCts;
        var token = navigateCts.Token;
        IsNavigating = true;

        try
        {
            var detail = await _scheduleService.GetByIdAsync(scheduleId, token);
            if (version != _navigateRequestVersion) return;

            if (detail is null)
            {
                SetSelectedScheduleWithoutDetailReload(null);
                SelectedScheduleDetail = null;
                ShowUserError("일정 열기 실패", "요청한 일정을 찾을 수 없습니다. 이미 삭제되었을 수 있습니다.");
                return;
            }

            SelectedScheduleDetail = detail;
            _suppressDateReload = true;
            SelectedDate = detail.StartAt.Date;
            _suppressDateReload = false;
            OnPropertyChanged(nameof(SelectedDate));

            var (appliedText, appliedScope) = _listState.GetAppliedSearch();
            var targetPage = await _scheduleService.GetPageNumberForScheduleAsync(
                scheduleId,
                SelectedDate,
                appliedText,
                appliedScope,
                PageSize,
                token);
            if (version != _navigateRequestVersion) return;

            if (targetPage is null)
            {
                _dialogService.ShowInfo(
                    "일정 상세는 열렸지만 현재 목록 조건(날짜/검색)에서는 보이지 않습니다.",
                    "목록 동기화 안내");
                return;
            }

            var moved = _listState.MoveToPageUnchecked(targetPage.Value);
            if (moved)
                UpdatePagingUi();

            _preserveDetailOnEmptyList = true;
            try
            {
                await LoadSchedulesAsync(false);
            }
            finally
            {
                _preserveDetailOnEmptyList = false;
            }

            if (version != _navigateRequestVersion) return;

            var match = PagedSchedules.FirstOrDefault(x => x.Id == scheduleId);
            SetSelectedScheduleWithoutDetailReload(match);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (version == _navigateRequestVersion)
            {
                ShowUserError(
                    "일정 열기 실패",
                    UserMessageTemplates.BuildDbHint("알림에서 일정을 여는 작업") + $"\n\n오류: {ex.Message}");
            }
        }
        finally
        {
            if (version == _navigateRequestVersion)
                IsNavigating = false;

            if (ReferenceEquals(_navigateCts, navigateCts))
            {
                _navigateCts?.Dispose();
                _navigateCts = null;
            }
        }
    }

    public void Stop()
    {
        _listCts?.Cancel();
        _listCts?.Dispose();
        _listCts = null;

        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = null;

        _navigateCts?.Cancel();
        _navigateCts?.Dispose();
        _navigateCts = null;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (!_suppressDateReload)
        {
            _listState.MoveToFirstPage();
            UpdatePagingUi();
            ScheduleLoad();
        }
    }

    partial void OnSelectedScheduleChanged(ScheduleListItem? value)
    {
        if (_suppressSelectionDetailReload)
            return;

        SelectedScheduleDetail = null;
        ScheduleDetailLoad();
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
                    UserMessageTemplates.BuildDbHint("일정 상세를 불러오는 작업") + $"\n\n오류: {ex.Message}");
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

    private void UpdatePagingUi()
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPrevPage));
        OnPropertyChanged(nameof(HasNextPage));
    }

    private void ScheduleLoad(bool showLoading = true)
    {
        _scheduledLoadTask = LoadSchedulesAsync(showLoading);
    }

    private void ScheduleDetailLoad()
    {
        _scheduledDetailTask = LoadSelectedDetailAsync();
    }

    private void SetSelectedScheduleWithoutDetailReload(ScheduleListItem? value)
    {
        _suppressSelectionDetailReload = true;
        SelectedSchedule = value;
        _suppressSelectionDetailReload = false;
    }
}
