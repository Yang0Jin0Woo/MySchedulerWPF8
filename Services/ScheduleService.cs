using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using MyScheduler.Repositories;

namespace MyScheduler.Services;

public class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _repository;

    public ScheduleService(IScheduleRepository repository)
    {
        _repository = repository;
    }

    public Task<List<ScheduleListItem>> GetListByDateAsync(DateTime date)
        => _repository.GetListByDateAsync(date);

    public Task<ScheduleItem?> GetByIdAsync(int id)
        => _repository.GetByIdAsync(id);

    public Task<ScheduleItem> AddAsync(ScheduleItem item)
        => _repository.AddAsync(item);

    public async Task UpdateAsync(ScheduleItem item)
    {
        try
        {
            await _repository.UpdateAsync(item);
        }
        catch (DbUpdateConcurrencyException)
        {
            var latest = await _repository.GetByIdAsync(item.Id);
            var isDeleted = latest is null;

            throw new ConcurrencyConflictException(latest, isDeleted);
        }
    }

    public Task DeleteAsync(int id)
        => _repository.DeleteAsync(id);
}
