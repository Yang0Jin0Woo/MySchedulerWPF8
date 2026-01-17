using MyScheduler.Models;

namespace MyScheduler.Repositories;

public interface IScheduleRepository
{
    Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date);
    Task<ScheduleItem?> GetByIdAsync(int id);

    Task<ScheduleItem> AddAsync(ScheduleItem item);
    Task UpdateAsync(ScheduleItem item);   // RowVersion 동시성 충돌 시 Exception 발생
    Task DeleteAsync(int id);
}
