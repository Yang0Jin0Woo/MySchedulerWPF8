using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        SelectedDate = DateTime.Today;

        StartClock();
        _ = LoadSchedulesAsync();
    }

    public ObservableCollection<ScheduleListItem> Schedules { get; } = new();
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

    private DateTime _selectedDate;
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
            }
        }
    }

    private int _listRequestVersion;
    private int _detailRequestVersion;

    private bool CanEditOrDelete()
        => !IsBusy && !IsLoadingList && SelectedScheduleDetail is not null;

    private bool FilterSchedule(object obj)
    {
        if (obj is not ScheduleListItem item) return false;

        var keyword = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(keyword)) return true;

        bool Contains(string? s) =>
            !string.IsNullOrEmpty(s) &&
            s.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);

        return SelectedSearchScope switch
        {
            "제목" => Contains(item.Title),
            "장소" => Contains(item.Location),
            _ => Contains(item.Title) || Contains(item.Location),
        };
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
    }

    [RelayCommand]
    private void ApplySearch() => SchedulesView.Refresh();

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        SelectedSearchScope = SearchScopes.First();
    }

    [RelayCommand]
    private async Task LoadSchedulesAsync()
    {
        var version = ++_listRequestVersion;
        IsLoadingList = true;

        var prevId = SelectedSchedule?.Id;

        try
        {
            var items = await _scheduleService.GetListByDateAsync(SelectedDate);
            if (version != _listRequestVersion) return;

            Schedules.Clear();
            foreach (var item in items)
                Schedules.Add(item);

            RefreshSchedulesView();

            if (prevId is not null)
            {
                var restored = Schedules.FirstOrDefault(x => x.Id == prevId);
                if (restored is not null)
                    SelectedSchedule = restored;
            }
        }
        finally
        {
            if (version == _listRequestVersion)
                IsLoadingList = false;
        }
    }

    private async Task LoadSelectedDetailAsync()
    {
        if (SelectedSchedule is null)
        {
            SelectedScheduleDetail = null;
            return;
        }

        var version = ++_detailRequestVersion;
        IsBusy = true;

        try
        {
            var detail = await _scheduleService.GetByIdAsync(SelectedSchedule.Id);
            if (version != _detailRequestVersion) return;

            SelectedScheduleDetail = detail;
        }
        finally
        {
            if (version == _detailRequestVersion)
                IsBusy = false;
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
            await LoadSchedulesAsync();

            SelectedSchedule = Schedules.FirstOrDefault(x => x.Id == created.Id);
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
            await LoadSchedulesAsync();
        }
        catch (ConcurrencyConflictException ex)
        {
            HandleConcurrencyConflict(ex);
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

        if (MessageBox.Show(
                "정말 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            var latest = await _scheduleService.GetByIdAsync(id);

            if (latest?.RowVersion is null || latest.RowVersion.Length == 0)
                throw new ConcurrencyConflictException(null, true);

            await _scheduleService.DeleteAsync(latest.Id, latest.RowVersion);

            SelectedSchedule = null;
            SelectedScheduleDetail = null;

            await LoadSchedulesAsync();
        }
        catch (ConcurrencyConflictException ex)
        {
            HandleConcurrencyConflict(ex);
        }
        finally
        {
            IsBusy = false;
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

    private readonly DispatcherTimer _clockTimer = new();
    private DateTime _now = DateTime.Now;

    public DateTime Now
    {
        get => _now;
        private set => SetProperty(ref _now, value);
    }

    private void StartClock()
    {
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, __) => Now = DateTime.Now;
        _clockTimer.Start();
    }

    public void StopClock() => _clockTimer.Stop();
}
