using MyScheduler.Models;

namespace MyScheduler.Services;

public interface IScheduleService
{
    DateTime GetKoreaNow();
    DateTime UtcToKorea(DateTime utc);

    Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date);
    Task<ScheduleItem?> GetByIdAsync(int id);

    Task<ScheduleItem> AddAsync(ScheduleItem item);
    Task UpdateAsync(ScheduleItem item);
    Task DeleteAsync(int id, byte[] rowVersion);
}
