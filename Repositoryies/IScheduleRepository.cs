using MyScheduler.Models;

namespace MyScheduler.Repositories;

public interface IScheduleRepository
{
    Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date);
    Task<ScheduleItem?> GetByIdAsync(int id);

    Task<ScheduleItem> AddAsync(ScheduleItem item);
    Task UpdateAsync(ScheduleItem item);
    Task DeleteAsync(int id);
}
