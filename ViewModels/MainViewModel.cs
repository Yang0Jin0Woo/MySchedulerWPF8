using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.Views;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;

    public MainViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;

        _selectedSearchScope = SearchScopes.First();

        SchedulesView = CollectionViewSource.GetDefaultView(Schedules);
        SchedulesView.SortDescriptions.Add(
            new SortDescription(nameof(ScheduleListItem.StartAt), ListSortDirection.Ascending));
        SchedulesView.Filter = FilterSchedule;

        SelectedDate = _scheduleService.GetKoreaNow().Date;

        StartClock();
        _ = LoadSchedulesAsync();
    }

    public ObservableCollection<ScheduleListItem> Schedules { get; } = new();
    public ObservableCollection<ScheduleListItem> PagedSchedules { get; } = new();
    public ICollectionView SchedulesView { get; }

    public ObservableCollection<string> SearchScopes { get; } =
        new() { "전체", "제목", "장소" };

    private string _selectedSearchScope;
    public string SelectedSearchScope
    {
        get => _selectedSearchScope;
        set
        {
            if (SetProperty(ref _selectedSearchScope, value))
            {
                RefreshSchedulesView();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshSchedulesView();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
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
                _ = LoadSchedulesAsync();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private DateTime _nowKst;
    public DateTime NowKst
    {
        get => _nowKst;
        private set => SetProperty(ref _nowKst, value);
    }

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

    private const int PageSize = 10;
    private int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    private int _totalPages = 1;
    public int TotalPages
    {
        get => _totalPages;
        private set => SetProperty(ref _totalPages, value);
    }

    public bool HasPrevPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public ObservableCollection<int> PageNumbers { get; } = new();

    private bool CanEditOrDelete()
        => !IsBusy && !IsLoadingList && !IsLoadingDetail && SelectedScheduleDetail is not null;

    private bool CanExportCsv()
        => !IsBusy && !IsLoadingList && SchedulesView.Cast<object>().Any();

    private bool FilterSchedule(object obj)
    {
        if (obj is not ScheduleListItem item) return false;

        var keyword = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(keyword)) return true;

        return _scheduleService.MatchesFilter(item, SearchText, SelectedSearchScope);
    }

    private void RefreshSchedulesView()
    {
        SchedulesView.Refresh();

        if (SelectedSchedule is not null &&
            !SchedulesView.Cast<object>().Contains(SelectedSchedule))
        {
            SelectedSchedule = null;
            SelectedScheduleDetail = null;
        }

        UpdatePaging();
    }

    [RelayCommand]
    private void ApplySearch()
    {
        RefreshSchedulesView();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        SelectedSearchScope = SearchScopes.First();
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

        try
        {
            var items = await _scheduleService.GetListByDateAsync(SelectedDate, token);
            if (version != _listRequestVersion) return;

            Schedules.Clear();
            foreach (var item in items)
                Schedules.Add(item);

            RefreshSchedulesView();

            if (prevId is not null)
            {
                var restored = Schedules.FirstOrDefault(x => x.Id == prevId);
                if (restored is not null && PagedSchedules.Contains(restored))
                    SelectedSchedule = restored;
            }
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
            var vm = new ScheduleEditViewModel(SelectedDate);
            var win = new ScheduleEditWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (win.ShowDialog() != true || vm.Result is null)
                return;

            var created = await _scheduleService.AddAsync(vm.Result);
            await LoadSchedulesAsync(false);

            SelectedSchedule = Schedules.FirstOrDefault(x => x.Id == created.Id);
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
            var vm = new ScheduleEditViewModel(SelectedDate, SelectedScheduleDetail);
            var win = new ScheduleEditWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (win.ShowDialog() != true || vm.Result is null)
                return;

            await _scheduleService.UpdateAsync(vm.Result);
            await LoadSchedulesAsync(false);
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

        if (MessageBox.Show(
                "정말 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
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
    private async Task ExportCsvAsync()
    {
        var rows = SchedulesView.Cast<object>()
            .OfType<ScheduleListItem>()
            .ToList();

        if (rows.Count == 0)
        {
            MessageBox.Show(
                "내보낼 일정이 없습니다.\n(검색/필터 결과가 비어있을 수 있습니다.)",
                "CSV 내보내기",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "CSV로 내보내기",
            Filter = "CSV 파일 (*.csv)|*.csv",
            FileName = $"MyScheduler_{SelectedDate:yyyyMMdd}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var bytes = _scheduleService.BuildCsvBytes(rows);
            await File.WriteAllBytesAsync(dlg.FileName, bytes);

            MessageBox.Show(
                $"CSV 저장이 완료되었습니다.\n\n{dlg.FileName}",
                "CSV 내보내기",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            MessageBox.Show(
                "해당 일정은 이미 삭제되었습니다.\n목록을 최신 상태로 갱신합니다.",
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            SelectedSchedule = null;
            SelectedScheduleDetail = null;
            _ = LoadSchedulesAsync();
            return;
        }

        if (ex.Latest is not null)
        {
            MessageBox.Show(
                "다른 곳에서 일정이 수정되었습니다.\n최신 데이터로 갱신합니다.",
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            SelectedScheduleDetail = ex.Latest;
            _ = LoadSchedulesAsync();
        }
    }

    private static void ShowUserError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
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

    private readonly DispatcherTimer _clockTimer = new();

    private void StartClock()
    {
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, __) =>
        {
            NowKst = _scheduleService.UtcToKorea(DateTime.UtcNow);
        };

        NowKst = _scheduleService.UtcToKorea(DateTime.UtcNow);
        _clockTimer.Start();
    }

    public void StopClock() => _clockTimer.Stop();

    [RelayCommand(CanExecute = nameof(HasPrevPage))]
    private void PrevPage()
    {
        if (CurrentPage <= 1) return;
        CurrentPage -= 1;
        UpdatePaging();
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void NextPage()
    {
        if (CurrentPage >= TotalPages) return;
        CurrentPage += 1;
        UpdatePaging();
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        if (CurrentPage == page) return;
        CurrentPage = page;
        UpdatePaging();
    }

    private void UpdatePaging()
    {
        var all = SchedulesView.Cast<object>()
            .OfType<ScheduleListItem>()
            .ToList();

        var totalCount = all.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        var pageItems = all
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        PagedSchedules.Clear();
        foreach (var item in pageItems)
            PagedSchedules.Add(item);

        if (SelectedSchedule is not null &&
            !pageItems.Contains(SelectedSchedule))
        {
            SelectedSchedule = null;
            SelectedScheduleDetail = null;
        }

        UpdatePageNumbers();

        OnPropertyChanged(nameof(HasPrevPage));
        OnPropertyChanged(nameof(HasNextPage));
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void UpdatePageNumbers()
    {
        PageNumbers.Clear();
        const int windowSize = 5;
        if (TotalPages <= 0) return;

        var half = windowSize / 2;
        var start = CurrentPage - half;
        var end = CurrentPage + half;

        if (start < 1)
        {
            end += 1 - start;
            start = 1;
        }

        if (end > TotalPages)
        {
            start -= end - TotalPages;
            end = TotalPages;
        }

        if (start < 1) start = 1;

        for (var i = start; i <= end && PageNumbers.Count < windowSize; i++)
            PageNumbers.Add(i);
    }
}
