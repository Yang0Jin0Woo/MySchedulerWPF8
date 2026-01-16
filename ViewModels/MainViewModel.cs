using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.Utils;
using MyScheduler.Views;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService = new ScheduleService();

    private readonly DispatcherTimer _clockTimer;

    private string _currentTimeText = "";
    public string CurrentTimeText
    {
        get => _currentTimeText;
        private set => SetProperty(ref _currentTimeText, value);
    }

    public ObservableCollection<ScheduleListItem> Schedules { get; } = new();

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

    // Read 로딩 상태
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

    // Write 재진입 방지
    // Add/Edit/Delete 실행 중이면 IsBusy=true로 두고
    // 모든 Write를 CanExecute에서 막아 연타/중복 실행 방지
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                // Busy 상태 바뀌면 버튼 활성/비활성 즉시 갱신
                AddScheduleCommand.NotifyCanExecuteChanged();
                EditScheduleCommand.NotifyCanExecuteChanged();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // 레이스 컨디션 방지(마지막 요청만 반영)
    private int _listRequestVersion = 0;
    private int _detailRequestVersion = 0;

    public MainViewModel()
    {
        _ = LoadSchedulesAsync();

        UpdateClock();

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, __) => UpdateClock();
        _clockTimer.Start();
    }

    private static readonly CultureInfo KoreanCulture = new("ko-KR");

    private void UpdateClock()
    {
        var kstNow = TimeUtil.GetKoreaNow();
        CurrentTimeText = kstNow.ToString("tt h:mm:ss", KoreanCulture); // (24시 기준)"HH:mm:ss" or "HH:mm"
    }

    // 종료 시 정리
    public void StopClock() => _clockTimer.Stop();


    private bool CanAdd() => !IsBusy;

    private bool CanEditOrDelete() => !IsBusy && SelectedScheduleDetail is not null;

    // READ - List
    [RelayCommand]
    private async Task LoadSchedulesAsync()
    {
        var myVersion = ++_listRequestVersion;
        IsLoadingList = true;

        try
        {
            var items = await _scheduleService.GetListByDateAsync(SelectedDate);

            // 레이스 방지: 마지막 요청만 반영
            if (myVersion != _listRequestVersion) return;

            Schedules.Clear();
            foreach (var it in items) Schedules.Add(it);

            // 날짜 바뀌면 상세 초기화
            SelectedSchedule = null;
            SelectedScheduleDetail = null;
        }
        finally
        {
            if (myVersion == _listRequestVersion)
                IsLoadingList = false;
        }
    }

    // READ - Detail
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
            //MessageBox.Show(detail is null ? "detail == null" : $"detail OK: {detail.TimeRangeText}");

            // 레이스 방지: 마지막 요청만 반영
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

    // CREATE (재진입 방지 적용)
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddScheduleAsync()
    {
        // 연타/중복 실행 방지
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

    // UPDATE (재진입 방지 적용)
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

            await _scheduleService.UpdateAsync(vm.Result);
            await LoadSchedulesAsync();

            // 수정 후 상세 재조회
            if (SelectedSchedule is not null)
                SelectedScheduleDetail = await _scheduleService.GetByIdAsync(SelectedSchedule.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // DELETE (재진입 방지 적용)
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
}
