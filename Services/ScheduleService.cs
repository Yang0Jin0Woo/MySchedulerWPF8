using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using MyScheduler.Utils;

namespace MyScheduler.Services;

public class ScheduleService : IScheduleService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ScheduleService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date)
    {
        var startKst = date.Date;
        var endKst = startKst.AddDays(1);

        var startUtc = TimeUtil.KoreaToUtc(startKst);
        var endUtc = TimeUtil.KoreaToUtc(endKst);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var raw = await db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt >= startUtc)
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

        foreach (var it in raw)
        {
            it.StartAt = TimeUtil.UtcToKorea(it.StartAt);
            it.EndAt = TimeUtil.UtcToKorea(it.EndAt);
        }

        return raw;
    }

    public async Task<ScheduleItem?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var item = await db.Schedules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return null;

        item.StartAt = TimeUtil.UtcToKorea(item.StartAt);
        item.EndAt = TimeUtil.UtcToKorea(item.EndAt);

        return item;
    }

    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        item.StartAt = TimeUtil.KoreaToUtc(item.StartAt);
        item.EndAt = TimeUtil.KoreaToUtc(item.EndAt);

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Schedules.Add(item);
        await db.SaveChangesAsync();

        item.StartAt = TimeUtil.UtcToKorea(item.StartAt);
        item.EndAt = TimeUtil.UtcToKorea(item.EndAt);

        return item;
    }

    public async Task UpdateAsync(ScheduleItem item)
    {
        var utcItem = new ScheduleItem
        {
            Id = item.Id,
            Title = item.Title,
            Location = item.Location,
            Notes = item.Notes,
            StartAt = TimeUtil.KoreaToUtc(item.StartAt),
            EndAt = TimeUtil.KoreaToUtc(item.EndAt),
            RowVersion = item.RowVersion
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Schedules.Update(utcItem);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var dbValues = await entry.GetDatabaseValuesAsync();

            // 이미 삭제된 경우
            if (dbValues is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: true);

            // 수정 충돌인 경우 최신 데이터 조회 후 전달
            var latestUtc = (ScheduleItem)dbValues.ToObject();

            latestUtc.StartAt = TimeUtil.UtcToKorea(latestUtc.StartAt);
            latestUtc.EndAt = TimeUtil.UtcToKorea(latestUtc.EndAt);

            throw new ConcurrencyConflictException(latest: latestUtc, isDeleted: false);
        }
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
