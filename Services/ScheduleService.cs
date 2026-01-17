using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using MyScheduler.Repositories;

namespace MyScheduler.Services;

public class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _repo;

    public ScheduleService(IScheduleRepository repo)
    {
        _repo = repo;
    }

    public Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date)
        => _repo.GetListByDateAsync(date);

    public Task<ScheduleItem?> GetByIdAsync(int id)
        => _repo.GetByIdAsync(id);

    public Task<ScheduleItem> AddAsync(ScheduleItem item)
        => _repo.AddAsync(item);

    public async Task UpdateAsync(ScheduleItem item)
    {
        try
        {
            await _repo.UpdateAsync(item);
        }
        catch (DbUpdateConcurrencyException)
        {
            // 충돌 시 최신 데이터를 조회해서 ViewModel에 전달 가능한 형태로 변환
            var latest = await _repo.GetByIdAsync(item.Id);

            if (latest is null)
            {
                throw new ConcurrencyConflictException(
                    "다른 곳에서 이미 삭제된 일정입니다. 최신 목록으로 갱신합니다.",
                    latest: null,
                    isDeleted: true
                );
            }

            throw new ConcurrencyConflictException(
                "다른 곳에서 일정이 수정되었습니다. 최신 데이터로 갱신합니다.",
                latest: latest,
                isDeleted: false
            );
        }
    }

    public Task DeleteAsync(int id)
        => _repo.DeleteAsync(id);
}
