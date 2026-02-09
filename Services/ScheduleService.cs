using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using MyScheduler.Utils;
using System.Globalization;
using System.Text;
using System.Threading;

namespace MyScheduler.Services;

public class ScheduleService : IScheduleService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ScheduleService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public DateTime GetKoreaNow()
        => TimeUtil.GetKoreaNow();

    public DateTime UtcToKorea(DateTime utc)
        => TimeUtil.UtcToKorea(utc);

    public async Task<(List<ScheduleListItem> Items, int TotalCount)> GetListByDateAsync(
        DateTime date,
        string? searchText,
        string? searchScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        var startUtc = TimeUtil.KoreaToUtc(start);
        var endUtc = TimeUtil.KoreaToUtc(end);

        await using var db = _dbFactory.CreateDbContext();

        IQueryable<ScheduleItem> query = db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt >= startUtc);

        var keyword = searchText?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = searchScope switch
            {
                "제목" => query.Where(x => x.Title.Contains(keyword)),
                "장소" => query.Where(x => x.Location != null && x.Location.Contains(keyword)),
                _ => query.Where(x => x.Title.Contains(keyword) ||
                                      (x.Location != null && x.Location.Contains(keyword))),
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = Math.Max(0, (page - 1) * pageSize);
        var items = await query
            .OrderBy(x => x.StartAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new ScheduleListItem
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                StartAt = TimeUtil.UtcToKorea(x.StartAt),
                EndAt = TimeUtil.UtcToKorea(x.EndAt)
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = _dbFactory.CreateDbContext();

        var item = await db.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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

        await using var db = _dbFactory.CreateDbContext();
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

        await using var db = _dbFactory.CreateDbContext();
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

        await using var db = _dbFactory.CreateDbContext();

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

    public byte[] BuildCsvBytes(IEnumerable<ScheduleListItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Title,Location,StartAt,EndAt");

        foreach (var r in items)
        {
            sb.Append(EscapeCsv(r.Id.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(r.Title)).Append(',');
            sb.Append(EscapeCsv(r.Location)).Append(',');
            sb.Append(EscapeCsv(r.StartAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(r.EndAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        var s = value ?? "";
        var mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');

        if (s.Contains('"'))
            s = s.Replace("\"", "\"\"");

        return mustQuote ? $"\"{s}\"" : s;
    }
}




