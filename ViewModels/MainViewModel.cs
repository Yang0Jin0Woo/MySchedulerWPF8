using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace MyScheduler.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService = new ScheduleService();

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

    // 목록 선택
    private ScheduleListItem? _selectedSchedule;
    public ScheduleListItem? SelectedSchedule
    {
        get => _selectedSchedule;
        set
        {
            if (SetProperty(ref _selectedSchedule, value))
            {
                // read - detail
                _ = LoadSelectedDetailAsync();
                DeleteScheduleCommand.NotifyCanExecuteChanged();
                EditScheduleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // 상세 - read 결과
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

    // --- 레이스 방지 (동시성 방지) ---
    // 늦게 끝난 요청이 화면을 덮는 엉뚱한 상세가 보이는 문제 개선
    private int _listRequestVersion = 0;
    private int _detailRequestVersion = 0;

    private bool _isLoadingList;
    public bool IsLoadingList
    {
        get => _isLoadingList;
        set => SetProperty(ref _isLoadingList, value);
    }

    private bool _isLoadingDetail;
    public bool IsLoadingDetail
    {
        get => _isLoadingDetail;
        set => SetProperty(ref _isLoadingDetail, value);
    }

    public MainViewModel()
    {
        _ = LoadSchedulesAsync();
    }

    private bool CanEditOrDelete() => SelectedScheduleDetail is not null;

    // read - list 날짜별 목록 조회(+레이스 방지)
    [RelayCommand]
    private async Task LoadSchedulesAsync()
    {
        var myVersion = ++_listRequestVersion;
        IsLoadingList = true;

        try
        {
            var items = await _scheduleService.GetListByDateAsync(SelectedDate);

            // 마지막 요청이 아니면 UI 반영 x
            if (myVersion != _listRequestVersion) return;

            Schedules.Clear();
            foreach (var it in items) Schedules.Add(it);

            SelectedSchedule = null;
            SelectedScheduleDetail = null;
        }
        finally
        {
            if (myVersion == _listRequestVersion)
                IsLoadingList = false;
        }
    }

    // read - detail 선택한 일정의 상세 조회(+레이스 방지)
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

            // 빠르게 다른 항목 클릭 때, 늦게 끝난 이전 상세 조회가 덮어쓰기 방지
            if (myVersion == _detailRequestVersion)
                SelectedScheduleDetail = detail;
        }
        finally
        {
            if (myVersion == _detailRequestVersion)
                IsLoadingDetail = false;
        }
    }

    // create 일정 추가
    [RelayCommand]
    private async Task AddScheduleAsync()
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

    // update 일정 수정
    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditScheduleAsync()
    {
        if (SelectedScheduleDetail is null) return;

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

        if (SelectedSchedule is not null)
            SelectedScheduleDetail = await _scheduleService.GetByIdAsync(SelectedSchedule.Id);
    }

    // delete 일정 삭제
    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteScheduleAsync()
    {
        if (SelectedScheduleDetail is null) return;

        await _scheduleService.DeleteAsync(SelectedScheduleDetail.Id);

        SelectedSchedule = null;
        SelectedScheduleDetail = null;

        await LoadSchedulesAsync();
    }
}
