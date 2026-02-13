using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyScheduler.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyScheduler.ViewModels;

public partial class ScheduleEditViewModel : ObservableObject
{
    [ObservableProperty] private string title = "";
    [ObservableProperty] private DateTime startDate;
    [ObservableProperty] private DateTime endDate;

    [ObservableProperty] private string? location;
    [ObservableProperty] private string? notes;

    [ObservableProperty] private string? errorMessage;

    public ScheduleItem? Result { get; private set; }

    // 시간 선택(30분 단위)
    public IReadOnlyList<TimeSpan> TimeOptions { get; } =
        Enumerable.Range(0, 48).Select(i => TimeSpan.FromMinutes(i * 30)).ToList();

    // 선택 전에는 null -> 저장 불가
    [ObservableProperty] private TimeSpan? startTime;
    [ObservableProperty] private TimeSpan? endTime;

    public string StartAmPmText =>
        StartTime is null ? "" : (StartTime.Value.Hours < 12 ? "오전" : "오후");

    public string EndAmPmText =>
        EndTime is null ? "" : (EndTime.Value.Hours < 12 ? "오전" : "오후");

    // StartTime/EndTime이 바뀌면 오전/오후 텍스트 갱신
    partial void OnStartTimeChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(StartAmPmText));
    }

    partial void OnEndTimeChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(EndAmPmText));
    }

    public ScheduleEditViewModel(DateTime baseDate, ScheduleItem? existing = null)
    {
        if (existing is null)
        {
            StartDate = baseDate.Date;
            EndDate = baseDate.Date;
            StartTime = null;
            EndTime = null;
        }
        else
        {
            Title = existing.Title;
            StartDate = existing.StartAt.Date;
            EndDate = existing.EndAt.Date;

            StartTime = SnapTo30Minutes(existing.StartAt.TimeOfDay);
            EndTime = SnapTo30Minutes(existing.EndAt.TimeOfDay);

            Location = existing.Location;
            Notes = existing.Notes;

            Result = existing;
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

        var titleTrimmed = Title.Trim();
        if (titleTrimmed.Length > 256)
        {
            ErrorMessage = "제목은 256자 이하여야 합니다.";
            return;
        }

        if (StartTime is null)
        {
            ErrorMessage = "시작 시간을 선택해주세요.";
            return;
        }

        if (EndTime is null)
        {
            ErrorMessage = "종료 시간을 선택해주세요.";
            return;
        }

        var startAt = StartDate.Date + StartTime.Value;
        var endAt = EndDate.Date + EndTime.Value;

        if (endAt <= startAt)
        {
            ErrorMessage = "종료 시점은 시작 시점보다 이후여야 합니다.";
            return;
        }

        if (Result is null)
            Result = new ScheduleItem();

        Result.Title = titleTrimmed;
        Result.StartAt = startAt;
        Result.EndAt = endAt;
        Result.Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        Result.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        Result.Priority = 1;
    }

    private static TimeSpan SnapTo30Minutes(TimeSpan time)
    {
        // 30분 단위로 반올림
        var totalMinutes = (int)Math.Round(time.TotalMinutes / 30.0) * 30;

        // 하루 범위로 정규화
        totalMinutes %= (24 * 60);
        if (totalMinutes < 0) totalMinutes += (24 * 60);

        return TimeSpan.FromMinutes(totalMinutes);
    }
}
