using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using System.Collections.Generic;
using System.Threading;

namespace MyScheduler.Services;

public class ScheduleService : IScheduleService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITimeService _timeService;

    public ScheduleService(IDbContextFactory<AppDbContext> dbFactory, ITimeService timeService)
    {
        _dbFactory = dbFactory;
        _timeService = timeService;
    }

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

        var startUtc = _timeService.KoreaToUtc(start);
        var endUtc = _timeService.KoreaToUtc(end);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Max(1, pageSize);
        var skip = Math.Max(0, (safePage - 1) * safePageSize);

        await using var db = _dbFactory.CreateDbContext();

        IQueryable<ScheduleItem> query = db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt > startUtc);

        var keywordNormalized = NormalizeKeyword(searchText);
        if (string.IsNullOrWhiteSpace(keywordNormalized))
        {
            return await LoadByLikeAsync(query, null, safePageSize, skip, cancellationToken);
        }

        return await LoadByLikeAsync(query, (keywordNormalized, searchScope), safePageSize, skip, cancellationToken);
    }

    public async Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = _dbFactory.CreateDbContext();

        var item = await db.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null) return null;

        item.StartAt = _timeService.UtcToKorea(item.StartAt);
        item.EndAt = _timeService.UtcToKorea(item.EndAt);

        return item;
    }

    public async Task<List<ScheduleListItem>> GetStartingInRangeAsync(
        DateTime startKst,
        DateTime endKst,
        CancellationToken cancellationToken)
    {
        if (endKst <= startKst) return new List<ScheduleListItem>();

        var startUtc = _timeService.KoreaToUtc(startKst);
        var endUtc = _timeService.KoreaToUtc(endKst);

        await using var db = _dbFactory.CreateDbContext();

        var upcoming = await db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt >= startUtc && x.StartAt < endUtc)
            .OrderBy(x => x.StartAt)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                x.StartAt,
                x.EndAt
            })
            .ToListAsync(cancellationToken);

        return upcoming
            .Select(x => new ScheduleListItem
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                StartAt = _timeService.UtcToKorea(x.StartAt),
                EndAt = _timeService.UtcToKorea(x.EndAt)
            })
            .ToList();
    }

    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        var (normalizedStartKst, normalizedEndKst) = NormalizeAndValidateRange(item);

        var utcItem = new ScheduleItem
        {
            Title = item.Title,
            Location = item.Location,
            Notes = item.Notes,
            StartAt = _timeService.KoreaToUtc(normalizedStartKst),
            EndAt = _timeService.KoreaToUtc(normalizedEndKst),
            Priority = item.Priority,
            IsAllDay = item.IsAllDay
        };

        await using var db = _dbFactory.CreateDbContext();
        db.Schedules.Add(utcItem);
        await db.SaveChangesAsync();

        utcItem.StartAt = _timeService.UtcToKorea(utcItem.StartAt);
        utcItem.EndAt = _timeService.UtcToKorea(utcItem.EndAt);

        return utcItem;
    }

    public async Task<ScheduleItem> UpdateAsync(ScheduleItem item)
    {
        if (item.RowVersion is null)
            throw new ConcurrencyConflictException(null, isDeleted: false);

        var (normalizedStartKst, normalizedEndKst) = NormalizeAndValidateRange(item);

        var utcItem = new ScheduleItem
        {
            Id = item.Id,
            Title = item.Title,
            Location = item.Location,
            Notes = item.Notes,
            StartAt = _timeService.KoreaToUtc(normalizedStartKst),
            EndAt = _timeService.KoreaToUtc(normalizedEndKst),
            Priority = item.Priority,
            IsAllDay = item.IsAllDay,

            RowVersion = item.RowVersion
        };

        await using var db = _dbFactory.CreateDbContext();
        db.Attach(utcItem);

        var updateEntry = db.Entry(utcItem);
        updateEntry.Property(x => x.Title).IsModified = true;
        updateEntry.Property(x => x.Location).IsModified = true;
        updateEntry.Property(x => x.Notes).IsModified = true;
        updateEntry.Property(x => x.StartAt).IsModified = true;
        updateEntry.Property(x => x.EndAt).IsModified = true;
        updateEntry.Property(x => x.Priority).IsModified = true;
        updateEntry.Property(x => x.IsAllDay).IsModified = true;

        updateEntry.Property(x => x.RowVersion).OriginalValue = item.RowVersion;

        try
        {
            await db.SaveChangesAsync();
            utcItem.StartAt = _timeService.UtcToKorea(utcItem.StartAt);
            utcItem.EndAt = _timeService.UtcToKorea(utcItem.EndAt);
            return utcItem;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            if (entry is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: false);

            var dbValues = await entry.GetDatabaseValuesAsync();

            if (dbValues is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: true);

            var latestUtc = (ScheduleItem)dbValues.ToObject();
            latestUtc.StartAt = _timeService.UtcToKorea(latestUtc.StartAt);
            latestUtc.EndAt = _timeService.UtcToKorea(latestUtc.EndAt);

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
            var entry = ex.Entries.FirstOrDefault();
            if (entry is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: false);

            var dbValues = await entry.GetDatabaseValuesAsync();

            if (dbValues is null)
                throw new ConcurrencyConflictException(latest: null, isDeleted: true);

            var latestUtc = (ScheduleItem)dbValues.ToObject();
            latestUtc.StartAt = _timeService.UtcToKorea(latestUtc.StartAt);
            latestUtc.EndAt = _timeService.UtcToKorea(latestUtc.EndAt);

            throw new ConcurrencyConflictException(latest: latestUtc, isDeleted: false);
        }
    }

    private async Task<(List<ScheduleListItem> Items, int TotalCount)> LoadByLikeAsync(
        IQueryable<ScheduleItem> query,
        (string KeywordNormalized, string? SearchScope)? filter,
        int pageSize,
        int skip,
        CancellationToken cancellationToken)
    {
        if (filter is not null)
        {
            var likePattern = $"{filter.Value.KeywordNormalized}%";
            var searchScope = filter.Value.SearchScope;
            query = searchScope switch
            {
                "제목" => query.Where(x => EF.Functions.Like(x.TitleNormalized, likePattern)),
                "장소" => query.Where(x => x.LocationNormalized != null &&
                                          EF.Functions.Like(x.LocationNormalized, likePattern)),
                _ => query.Where(x => EF.Functions.Like(x.TitleNormalized, likePattern) ||
                                      (x.LocationNormalized != null &&
                                       EF.Functions.Like(x.LocationNormalized, likePattern))),
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.StartAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                x.StartAt,
                x.EndAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new ScheduleListItem
            {
                Id = x.Id,
                Title = x.Title,
                Location = x.Location,
                StartAt = _timeService.UtcToKorea(x.StartAt),
                EndAt = _timeService.UtcToKorea(x.EndAt)
            })
            .ToList();

        return (items, totalCount);
    }

    private static (DateTime StartAt, DateTime EndAt) NormalizeAndValidateRange(ScheduleItem item)
    {
        var startAt = NormalizeKstInput(item.StartAt);
        var endAt = NormalizeKstInput(item.EndAt);

        if (item.IsAllDay)
        {
            startAt = startAt.Date;
            endAt = startAt.AddDays(1);
        }

        if (endAt <= startAt)
            throw new DomainValidationException("종료 시점은 시작 시점보다 이후여야 합니다.");

        return (startAt, endAt);
    }

    private static DateTime NormalizeKstInput(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            throw new DomainValidationException("일정 시간은 UTC가 아닌 KST 기준으로 입력되어야 합니다.");

        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static string NormalizeKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var normalized = value.Trim()
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("\n", "")
            .Replace("\r", "");

        return normalized.ToUpperInvariant();
    }
}
