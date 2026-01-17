using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;

namespace MyScheduler.Repositories;

public class EfScheduleRepository : IScheduleRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public EfScheduleRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Schedules
            .Where(x => x.StartAt < end && x.EndAt >= start)
            .OrderBy(x => x.StartAt)
            .Select(x => new ScheduleListItem
            {
                Id = x.Id,
                Title = x.Title,
                StartAt = x.StartAt,
                EndAt = x.EndAt,
                Location = x.Location
            })
            .ToListAsync();
    }

    public async Task<ScheduleItem?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Schedules.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Schedules.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(ScheduleItem item)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // RowVersion을 포함한 업데이트 시도
        db.Schedules.Update(item);

        // 충돌 시 Exception 발생
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var target = await db.Schedules.FindAsync(id);
        if (target is null) return;

        db.Schedules.Remove(target);
        await db.SaveChangesAsync();
    }
}
