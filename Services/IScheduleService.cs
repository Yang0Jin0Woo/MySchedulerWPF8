using MyScheduler.Models;
using System.Threading;

namespace MyScheduler.Services;

public interface IScheduleService
{
    DateTime GetKoreaNow();
    DateTime UtcToKorea(DateTime utc);

    Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date, CancellationToken cancellationToken);
    Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<ScheduleItem> AddAsync(ScheduleItem item);
    Task UpdateAsync(ScheduleItem item);
    Task DeleteAsync(int id, byte[] rowVersion);

    bool MatchesFilter(ScheduleListItem item, string? searchText, string? searchScope);
    byte[] BuildCsvBytes(IEnumerable<ScheduleListItem> items);
}

