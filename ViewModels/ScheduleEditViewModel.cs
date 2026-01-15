using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using System.Globalization;

namespace MyScheduler.ViewModels;

public partial class ScheduleEditViewModel : ObservableObject
{
    // 입력 필드
    [ObservableProperty] private string title = "";
    [ObservableProperty] private DateTime startDate = DateTime.Today;
    [ObservableProperty] private DateTime endDate = DateTime.Today;
    [ObservableProperty] private string startTimeText = "10:00";
    [ObservableProperty] private string endTimeText = "11:00";
    [ObservableProperty] private string? location;
    [ObservableProperty] private string? notes;

    [ObservableProperty] private string? errorMessage;

    // 다이얼로그 결과(저장 누르면 채워짐)
    public ScheduleItem? Result { get; private set; }

    public ScheduleEditViewModel(DateTime baseDate, ScheduleItem? existing = null)
    {
        if (existing is null)
        {
            StartDate = baseDate.Date;
            EndDate = baseDate.Date;
            StartTimeText = "10:00";
            EndTimeText = "11:00";
        }
        else
        {
            Title = existing.Title;
            StartDate = existing.StartAt.Date;
            EndDate = existing.EndAt.Date;
            StartTimeText = existing.StartAt.ToString("HH:mm");
            EndTimeText = existing.EndAt.ToString("HH:mm");
            Location = existing.Location;
            Notes = existing.Notes;
            Result = existing; // 수정 모드에서 동일 객체를 업데이트해도 되고, 새로 만들어도 됨
        }
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "제목을 입력해주세요.";
            return;
        }

        if (!TryParseTime(StartTimeText, out var startTime))
        {
            ErrorMessage = "시작 시간 형식이 올바르지 않습니다. (예: 09:30)";
            return;
        }

        if (!TryParseTime(EndTimeText, out var endTime))
        {
            ErrorMessage = "종료 시간 형식이 올바르지 않습니다. (예: 11:00)";
            return;
        }

        var startAt = StartDate.Date + startTime;
        var endAt = EndDate.Date + endTime;

        if (endAt <= startAt)
        {
            ErrorMessage = "종료 시점은 시작 시점보다 이후여야 합니다.";
            return;
        }

        // Result가 있으면 수정, 없으면 신규 생성
        if (Result is null)
        {
            Result = new ScheduleItem();
        }

        Result.Title = Title.Trim();
        Result.StartAt = startAt;
        Result.EndAt = endAt;
        Result.Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        Result.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        Result.Priority = 1; // 필요하면 UI로 뺄 수 있음
    }

    private static bool TryParseTime(string input, out TimeSpan time)
    {
        // "HH:mm" 형태를 우선적으로 파싱
        if (TimeSpan.TryParseExact(input.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time))
            return true;

        // 로컬 기본 파싱도 fallback
        return TimeSpan.TryParse(input.Trim(), out time);
    }
}
