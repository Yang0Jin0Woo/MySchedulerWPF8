using MyScheduler.Models;
using System.Threading;

namespace MyScheduler.Services;

public interface IScheduleService
{
    DateTime GetKoreaNow();
    DateTime UtcToKorea(DateTime utc);

    Task<(List<ScheduleListItem> Items, int TotalCount)> GetListByDateAsync(
        DateTime date,
        string? searchText,
        string? searchScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<List<ScheduleListItem>> GetUpcomingAsync(
        DateTime startKst,
        DateTime endKst,
        CancellationToken cancellationToken);

    Task<ScheduleItem> AddAsync(ScheduleItem item);
    Task<ScheduleItem> UpdateAsync(ScheduleItem item);
    Task DeleteAsync(int id, byte[] rowVersion);

    byte[] BuildCsvBytes(IEnumerable<ScheduleListItem> items);
}

