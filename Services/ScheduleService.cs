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
        var start = date.Date;
        var end = start.AddDays(1);

        var startUtc = TimeUtil.KoreaToUtc(start);
        var endUtc = TimeUtil.KoreaToUtc(end);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var items = await db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt >= startUtc)
            .OrderBy(x => x.StartAt)
            .Select(x => new ScheduleListItem
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                StartAt = TimeUtil.UtcToKorea(x.StartAt),
                EndAt = TimeUtil.UtcToKorea(x.EndAt)
            })
            .ToListAsync();

        return items;
    }

    public async Task<ScheduleItem?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var item = await db.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null) return null;

        item.StartAt = TimeUtil.UtcToKorea(item.StartAt);
        item.EndAt = TimeUtil.UtcToKorea(item.EndAt);

        return item;
    }

    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        var utcItem = new ScheduleItem
        {
            Title = item.Title,
            Location = item.Location,
            Notes = item.Notes,
            StartAt = TimeUtil.KoreaToUtc(item.StartAt),
            EndAt = TimeUtil.KoreaToUtc(item.EndAt),
            Priority = item.Priority,
            IsAllDay = item.IsAllDay
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Schedules.Add(utcItem);
        await db.SaveChangesAsync();

        utcItem.StartAt = TimeUtil.UtcToKorea(utcItem.StartAt);
        utcItem.EndAt = TimeUtil.UtcToKorea(utcItem.EndAt);

        return utcItem;
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
            Priority = item.Priority,
            IsAllDay = item.IsAllDay,

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

            if (dbValues is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: true);

            var latestUtc = (ScheduleItem)dbValues.ToObject();
            latestUtc.StartAt = TimeUtil.UtcToKorea(latestUtc.StartAt);
            latestUtc.EndAt = TimeUtil.UtcToKorea(latestUtc.EndAt);

            throw new ConcurrencyConflictException(latest: latestUtc, isDeleted: false);
        }
    }

    public async Task DeleteAsync(int id, byte[] rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0)
            throw new ArgumentException("rowVersion이 비어있습니다.", nameof(rowVersion));

        await using var db = await _dbFactory.CreateDbContextAsync();

        var stub = new ScheduleItem
        {
            Id = id,
            RowVersion = rowVersion
        };

        db.Attach(stub);
        db.Entry(stub).Property(x => x.RowVersion).OriginalValue = rowVersion;

        db.Schedules.Remove(stub);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var dbValues = await entry.GetDatabaseValuesAsync();

            if (dbValues is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: true);

            var latestUtc = (ScheduleItem)dbValues.ToObject();
            latestUtc.StartAt = TimeUtil.UtcToKorea(latestUtc.StartAt);
            latestUtc.EndAt = TimeUtil.UtcToKorea(latestUtc.EndAt);

            throw new ConcurrencyConflictException(latest: latestUtc, isDeleted: false);
        }
    }
}
