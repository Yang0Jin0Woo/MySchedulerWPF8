using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.Utils;
using MyScheduler.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;

    private readonly DispatcherTimer _clockTimer;

    private string _currentTimeText = "";
    public string CurrentTimeText
    {
        get => _currentTimeText;
        private set => SetProperty(ref _currentTimeText, value);
    }

    public ObservableCollection<ScheduleListItem> Schedules { get; } = new();
    public ICollectionView SchedulesView { get; }

    public ObservableCollection<string> SearchScopes { get; } = new()
    {
        "전체", "제목", "장소"
    };

    private string _selectedSearchScope = "전체";
    public string SelectedSearchScope
    {
        get => _selectedSearchScope;
        set
        {
            if (SetProperty(ref _selectedSearchScope, value))
                RefreshSchedulesView();
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RefreshSchedulesView();
        }
    }

    public ObservableCollection<ScheduleListItem> FilteredSchedules => Schedules;

    private DateTime _selectedDate = DateTime.Today;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
                _ = LoadSchedulesAsync();
        }
    }

    private ScheduleListItem? _selectedSchedule;
    public ScheduleListItem? SelectedSchedule
    {
        get => _selectedSchedule;
        set
        {
            if (SetProperty(ref _selectedSchedule, value))
            {
                _ = LoadSelectedDetailAsync();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
                EditScheduleCommand.NotifyCanExecuteChanged();
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
                DeleteScheduleCommand.NotifyCanExecuteChanged();
                EditScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isLoadingList;
    public bool IsLoadingList
    {
        get => _isLoadingList;
        private set => SetProperty(ref _isLoadingList, value);
    }

    private bool _isLoadingDetail;
    public bool IsLoadingDetail
    {
        get => _isLoadingDetail;
        private set => SetProperty(ref _isLoadingDetail, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AddScheduleCommand.NotifyCanExecuteChanged();
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private int _listRequestVersion = 0;
    private int _detailRequestVersion = 0;

    private static readonly CultureInfo KoreanCulture = new("ko-KR");

    public MainViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;

        SchedulesView = CollectionViewSource.GetDefaultView(Schedules);
        SchedulesView.Filter = FilterSchedule;

        _ = LoadSchedulesAsync();

        UpdateClock();
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, __) => UpdateClock();
        _clockTimer.Start();
    }

    private void UpdateClock()
    {
        var kstNow = TimeUtil.GetKoreaNow();
        CurrentTimeText = kstNow.ToString("tt h:mm:ss", KoreanCulture);
    }

    public void StopClock() => _clockTimer.Stop();

    private bool CanAdd() => !IsBusy;
    private bool CanEditOrDelete() => !IsBusy && SelectedScheduleDetail is not null;

    private bool FilterSchedule(object obj)
    {
        if (obj is not ScheduleListItem item) return false;

        var keyword = (SearchText ?? "").Trim();
        if (keyword.Length == 0) return true;

        bool Contains(string? text) =>
            !string.IsNullOrWhiteSpace(text) &&
            text.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);

        return SelectedSearchScope switch
        {
            "제목" => Contains(item.Title),
            "장소" => Contains(item.Location),
            _ => Contains(item.Title) || Contains(item.Location)
        };
    }

    private void RefreshSchedulesView()
    {
        SchedulesView.Refresh();

        if (SelectedSchedule is not null && !SchedulesView.Cast<object>().Contains(SelectedSchedule))
        {
            SelectedSchedule = null;
            SelectedScheduleDetail = null;
        }
    }

    [RelayCommand]
    private void ApplySearch() => SchedulesView.Refresh();

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        SelectedSearchScope = "전체";
    }

    [RelayCommand]
    private async Task LoadSchedulesAsync()
    {
        var myVersion = ++_listRequestVersion;
        IsLoadingList = true;

        var prevSelectedId = SelectedSchedule?.Id;

        try
        {
            var items = await _scheduleService.GetListByDateAsync(SelectedDate);
            if (myVersion != _listRequestVersion) return;

            Schedules.Clear();
            foreach (var it in items) Schedules.Add(it);

            RefreshSchedulesView();

            if (prevSelectedId is not null)
            {
                var restored = Schedules.FirstOrDefault(x => x.Id == prevSelectedId);

                if (restored is not null)
                {
                    SelectedSchedule = restored;
                }
                else
                {
                    SelectedSchedule = null;
                    SelectedScheduleDetail = null;
                }
            }
            else
            {
                SelectedSchedule = null;
                SelectedScheduleDetail = null;
            }
        }
        finally
        {
            if (myVersion == _listRequestVersion)
                IsLoadingList = false;
        }
    }

    [RelayCommand]
    private async Task LoadSelectedDetailAsync()
    {
        var myVersion = ++_detailRequestVersion;
        IsLoadingDetail = true;

        try
        {
            if (SelectedSchedule is null)
            {
                SelectedScheduleDetail = null;
                return;
            }

            var detail = await _scheduleService.GetByIdAsync(SelectedSchedule.Id);

            if (myVersion == _detailRequestVersion)
                SelectedScheduleDetail = detail;
        }
        finally
        {
            if (myVersion == _detailRequestVersion)
                IsLoadingDetail = false;
        }

        DeleteScheduleCommand.NotifyCanExecuteChanged();
        EditScheduleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddScheduleAsync()
    {
        IsBusy = true;

        try
        {
            var vm = new ScheduleEditViewModel(SelectedDate);
            var win = new ScheduleEditWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = vm
            };

            var ok = win.ShowDialog();
            if (ok != true || vm.Result is null) return;

            await _scheduleService.AddAsync(vm.Result);
            await LoadSchedulesAsync();
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
                Owner = Application.Current?.MainWindow,
                DataContext = vm
            };

            var ok = win.ShowDialog();
            if (ok != true || vm.Result is null) return;

            try
            {
                await _scheduleService.UpdateAsync(vm.Result);
                await LoadSchedulesAsync();
            }
            catch (ConcurrencyConflictException cex)
            {
                // 충돌 처리 결과에 따라 목록 갱신 필요 판단
                var shouldReload = await HandleConcurrencyConflictAsync(cex, vm.Result);

                // 목록 갱신은 1번만 수행
                if (shouldReload)
                    await LoadSchedulesAsync();
            }
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

        IsBusy = true;

        try
        {
            await _scheduleService.DeleteAsync(SelectedScheduleDetail.Id);

            SelectedSchedule = null;
            SelectedScheduleDetail = null;

            await LoadSchedulesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> HandleConcurrencyConflictAsync(ConcurrencyConflictException ex, ScheduleItem myEdited)
    {
        if (ex.IsDeleted)
        {
            MessageBox.Show(
                ex.Message,
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return true;
        }

        if (ex.Latest is null)
        {
            MessageBox.Show(
                "최신 데이터를 가져올 수 없습니다. 목록을 갱신합니다.",
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return true;
        }

        // Yes: 최신으로 갱신, No: 내 변경 덮어쓰기(강제 저장), Cancel: 병합(최신 기준으로 다시 편집)
        var result = MessageBox.Show(
            "다른 곳에서 이미 수정된 일정입니다.\n\n" +
            "예(Y): 최신 내용으로 갱신\n" +
            "아니오(N): 내 변경으로 덮어쓰기\n" +
            "취소(C): 병합(편집창에서 다시 조정)",
            "동시성 충돌 처리",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        // 최신 갱신
        if (result == MessageBoxResult.Yes)
        {
            SelectedScheduleDetail = ex.Latest;
            MessageBox.Show("최신 내용으로 갱신했습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);

            return false;
        }

        // 내 변경 덮어쓰기
        if (result == MessageBoxResult.No)
        {
            myEdited.RowVersion = ex.Latest.RowVersion;

            try
            {
                await _scheduleService.UpdateAsync(myEdited);
                MessageBox.Show("내 변경으로 덮어썼습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (ConcurrencyConflictException again)
            {
                MessageBox.Show(
                    "저장 중 다시 충돌이 발생했습니다.\n최신 내용을 다시 확인해주세요.",
                    "동시성 충돌",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (again.IsDeleted)
                    return true;

                if (again.Latest is not null)
                    SelectedScheduleDetail = again.Latest;
                else
                    SelectedScheduleDetail = await _scheduleService.GetByIdAsync(myEdited.Id);

                return false;
            }
        }

        // 병합
        var mergeVm = new ScheduleEditViewModel(SelectedDate, ex.Latest);

        mergeVm.Title = myEdited.Title;
        mergeVm.Location = myEdited.Location;
        mergeVm.Notes = myEdited.Notes;
        mergeVm.StartDate = myEdited.StartAt.Date;
        mergeVm.EndDate = myEdited.EndAt.Date;
        mergeVm.StartTime = SnapTo30Minutes(myEdited.StartAt.TimeOfDay);
        mergeVm.EndTime = SnapTo30Minutes(myEdited.EndAt.TimeOfDay);

        var mergeWin = new ScheduleEditWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = mergeVm
        };

        var ok = mergeWin.ShowDialog();
        if (ok != true || mergeVm.Result is null)
            return false;

        mergeVm.Result.RowVersion = ex.Latest.RowVersion;

        try
        {
            await _scheduleService.UpdateAsync(mergeVm.Result);
            MessageBox.Show("병합 후 저장했습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (ConcurrencyConflictException again)
        {
            MessageBox.Show(
                "병합 저장 중 다시 충돌이 발생했습니다.\n최신 내용을 다시 확인 후 저장해주세요.",
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            if (again.IsDeleted)
                return true;

            if (again.Latest is not null)
                SelectedScheduleDetail = again.Latest;
            else
                SelectedScheduleDetail = await _scheduleService.GetByIdAsync(myEdited.Id);

            return false;
        }
    }

    private static TimeSpan SnapTo30Minutes(TimeSpan time)
    {
        var totalMinutes = (int)Math.Round(time.TotalMinutes / 30.0) * 30;
        totalMinutes %= (24 * 60);
        if (totalMinutes < 0) totalMinutes += (24 * 60);
        return TimeSpan.FromMinutes(totalMinutes);
    }
}
