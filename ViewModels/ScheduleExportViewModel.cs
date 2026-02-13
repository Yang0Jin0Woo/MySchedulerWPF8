using MyScheduler.Services;
using System.Linq;
using System.Threading;

namespace MyScheduler.ViewModels;

public class ScheduleExportViewModel
{
    private readonly IScheduleCsvService _scheduleCsvService;
    private readonly IFileExportService _fileExportService;
    private readonly IDialogService _dialogService;
    private readonly ScheduleBrowserViewModel _browserViewModel;

    public ScheduleExportViewModel(
        IScheduleCsvService scheduleCsvService,
        IFileExportService fileExportService,
        IDialogService dialogService,
        ScheduleBrowserViewModel browserViewModel)
    {
        _scheduleCsvService = scheduleCsvService;
        _fileExportService = fileExportService;
        _dialogService = dialogService;
        _browserViewModel = browserViewModel;
    }

    public async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        var rows = _browserViewModel.PagedSchedules.ToList();

        if (rows.Count == 0)
        {
            _dialogService.ShowInfo(
                "내보낼 일정이 없습니다.\n(검색/필터 결과가 비어있을 수 있습니다.)",
                "CSV 내보내기");
            return;
        }

        var fileName = _dialogService.ShowSaveCsvDialog($"MyScheduler_{_browserViewModel.SelectedDate:yyyyMMdd}.csv");
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            var bytes = _scheduleCsvService.BuildCsvBytes(rows);
            await _fileExportService.WriteAllBytesAsync(fileName, bytes, cancellationToken);

            _dialogService.ShowInfo(
                $"CSV 저장이 완료되었습니다.\n\n{fileName}",
                "CSV 내보내기");
        }
        catch (OperationCanceledException)
        {
            _dialogService.ShowInfo(
                "CSV 저장이 취소되었습니다.",
                "CSV 내보내기");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                "CSV 내보내기 실패",
                "CSV 파일 저장 중 문제가 발생했습니다.\n\n" +
                "확인해볼 수 있는 항목:\n" +
                "1) 저장 경로 권한(쓰기 가능 여부)\n" +
                "2) 파일이 다른 프로그램에서 열려있는지\n" +
                "3) 디스크 용량\n\n" +
                $"오류: {ex.Message}");
        }
    }
}
