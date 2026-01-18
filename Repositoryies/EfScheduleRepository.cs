using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;

namespace MyScheduler.Repositories;

public class EfScheduleRepository : IScheduleRepository
{
    private readonly AppDbContext _context;

    public EfScheduleRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date)
    {
        var d = date.Date;

        return _context.Schedules
            .Where(x => x.StartAt.Date == d)
            .OrderBy(x => x.StartAt)
            .Select(x => new ScheduleListItem
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                StartAt = x.StartAt,
                EndAt = x.EndAt
            })
            .ToListAsync();
    }

    public Task<ScheduleItem?> GetByIdAsync(int id)
        => _context.Schedules.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        _context.Schedules.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(ScheduleItem item)
    {
        _context.Entry(item).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Schedules.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;

        _context.Schedules.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
