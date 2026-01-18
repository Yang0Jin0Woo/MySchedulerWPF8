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
                await LoadSchedulesAsync(); //목록 갱신
            }
            catch (ConcurrencyConflictException cex)
            {
                var shouldReload = HandleConcurrencyConflict(cex, vm.Result);

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

    private bool HandleConcurrencyConflict(ConcurrencyConflictException ex, ScheduleItem myEdited)
    {
        // 이미 삭제된 경우
        if (ex.IsDeleted)
        {
            MessageBox.Show(
                "해당 일정은 이미 삭제되었습니다.\n목록을 최신 상태로 갱신합니다.",
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return true; // 목록 갱신 필요
        }

        // 최신 데이터를 가져온 경우
        if (ex.Latest is not null)
        {
            MessageBox.Show(
                "다른 곳에서 이미 수정된 일정입니다.\n최신 내용으로 갱신됩니다.",
                "동시성 충돌",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 상세 화면은 최신 데이터로 동기화
            SelectedScheduleDetail = ex.Latest;

            return true;
        }

        // 최신 데이터 없는 경우
        MessageBox.Show(
            "동시성 충돌이 발생했습니다.\n목록을 다시 불러옵니다.",
            "동시성 충돌",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return true;
    }

    private static TimeSpan SnapTo30Minutes(TimeSpan time)
    {
        var totalMinutes = (int)Math.Round(time.TotalMinutes / 30.0) * 30;
        totalMinutes %= (24 * 60);
        if (totalMinutes < 0) totalMinutes += (24 * 60);
        return TimeSpan.FromMinutes(totalMinutes);
    }
}
