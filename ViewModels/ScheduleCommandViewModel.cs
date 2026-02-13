using CommunityToolkit.Mvvm.ComponentModel;
using MyScheduler.Services;
using System.Linq;

namespace MyScheduler.ViewModels;

public partial class ScheduleCommandViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;
    private readonly IDialogService _dialogService;
    private readonly IScheduleEditorDialogService _scheduleEditorDialogService;
    private readonly ScheduleBrowserViewModel _browserViewModel;

    [ObservableProperty]
    private bool isBusy;

    public ScheduleCommandViewModel(
        IScheduleService scheduleService,
        IDialogService dialogService,
        IScheduleEditorDialogService scheduleEditorDialogService,
        ScheduleBrowserViewModel browserViewModel)
    {
        _scheduleService = scheduleService;
        _dialogService = dialogService;
        _scheduleEditorDialogService = scheduleEditorDialogService;
        _browserViewModel = browserViewModel;
    }

    public async Task AddScheduleAsync()
    {
        if (IsBusy || _browserViewModel.IsLoadingList) return;

        IsBusy = true;

        try
        {
            if (!_scheduleEditorDialogService.TryOpen(_browserViewModel.SelectedDate, null, out var result) || result is null)
                return;

            var created = await _scheduleService.AddAsync(result);
            await _browserViewModel.LoadSchedulesAsync(false);

            _browserViewModel.SelectedSchedule = _browserViewModel.PagedSchedules.FirstOrDefault(x => x.Id == created.Id);
        }
        catch (Exception ex)
        {
            ShowUserError(
                "일정 추가 실패",
                UserMessageTemplates.BuildDbHint("일정을 추가하는 작업") + $"\n\n오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task EditScheduleAsync()
    {
        if (_browserViewModel.SelectedScheduleDetail is null) return;

        IsBusy = true;

        try
        {
            if (!_scheduleEditorDialogService.TryOpen(
                    _browserViewModel.SelectedDate,
                    _browserViewModel.SelectedScheduleDetail,
                    out var result) || result is null)
                return;

            var updated = await _scheduleService.UpdateAsync(result);
            result.RowVersion = updated.RowVersion;
            await _browserViewModel.LoadSchedulesAsync(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            await HandleConcurrencyConflictAsync(ex);
        }
        catch (Exception ex)
        {
            ShowUserError(
                "일정 수정 실패",
                UserMessageTemplates.BuildDbHint("일정을 수정하는 작업") + $"\n\n오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteScheduleAsync()
    {
        if (_browserViewModel.SelectedScheduleDetail is null) return;

        var id = _browserViewModel.SelectedScheduleDetail.Id;
        var rowVersion = _browserViewModel.SelectedScheduleDetail.RowVersion;

        if (!_dialogService.Confirm("정말 삭제하시겠습니까?", "삭제 확인"))
            return;

        IsBusy = true;

        try
        {
            if (rowVersion is null || rowVersion.Length == 0)
                throw new ConcurrencyConflictException(null, true);

            await _scheduleService.DeleteAsync(id, rowVersion);

            _browserViewModel.SelectedSchedule = null;
            _browserViewModel.SelectedScheduleDetail = null;

            await _browserViewModel.LoadSchedulesAsync(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            await HandleConcurrencyConflictAsync(ex);
        }
        catch (Exception ex)
        {
            ShowUserError(
                "일정 삭제 실패",
                UserMessageTemplates.BuildDbHint("일정을 삭제하는 작업") + $"\n\n오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task HandleConcurrencyConflictAsync(ConcurrencyConflictException ex)
    {
        if (ex.IsDeleted)
        {
            _dialogService.ShowWarning(
                "해당 일정이 이미 삭제되었습니다.\n목록을 최신 상태로 갱신합니다.",
                "동시성 충돌");

            _browserViewModel.SelectedSchedule = null;
            _browserViewModel.SelectedScheduleDetail = null;
            await _browserViewModel.LoadSchedulesAsync();
            return;
        }

        if (ex.Latest is not null)
        {
            _dialogService.ShowWarning(
                "다른 곳에서 일정이 수정되었습니다.\n최신 데이터로 갱신합니다.",
                "동시성 충돌");

            _browserViewModel.SelectedScheduleDetail = ex.Latest;
            await _browserViewModel.LoadSchedulesAsync();
        }
    }

    private void ShowUserError(string title, string message)
    {
        _dialogService.ShowError(title, message);
    }
}
