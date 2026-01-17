using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;

namespace MyScheduler.Services;

public class ScheduleService : IScheduleService
{
    // read - list: 날짜 기준 목록 조회
    public async Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        await using var db = new AppDbContext();

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

    // read - detail 선택한 일정 id로 상세 조회
    public async Task<ScheduleItem?> GetByIdAsync(int id)
    {
        await using var db = new AppDbContext();
        return await db.Schedules.FirstOrDefaultAsync(x => x.Id == id);
    }

    // create 일정 추가
    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        await using var db = new AppDbContext();
        db.Schedules.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    // update 일정 수정(현재는 단일 구조)
    // 서버형 환경으로 확장 시, 동시 수정 충돌 발생하면 최신 데이터 다시 불러오기 or 병합 후 재시도
    public async Task UpdateAsync(ScheduleItem item)
    {
        await using var db = new AppDbContext();

        db.Schedules.Update(item);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // 최신 DB 가져오기(삭제 경우 null)
            var entry = ex.Entries.Single();
            var dbValues = await entry.GetDatabaseValuesAsync();

            if (dbValues is null)
            {
                throw new ConcurrencyConflictException(
                    "다른 곳에서 이미 삭제된 일정입니다. 최신 목록으로 갱신합니다.",
                    latest: null,
                    isDeleted: true
                );
            }

            var latest = (ScheduleItem)dbValues.ToObject();

            throw new ConcurrencyConflictException(
                "다른 곳에서 일정이 수정되었습니다. 최신 데이터로 갱신합니다.",
                latest: latest,
                isDeleted: false
            );
        }
    }

    // delete 일정 삭제
    public async Task DeleteAsync(int id)
    {
        await using var db = new AppDbContext();
        var target = await db.Schedules.FindAsync(id);
        if (target is null) return;

        db.Schedules.Remove(target);
        await db.SaveChangesAsync();
    }
}
