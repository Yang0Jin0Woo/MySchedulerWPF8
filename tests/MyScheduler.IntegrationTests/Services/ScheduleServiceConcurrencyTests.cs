using MyScheduler.IntegrationTests.TestInfrastructure;
using MyScheduler.Models;
using MyScheduler.Services;

namespace MyScheduler.IntegrationTests.Services;

public class ScheduleServiceConcurrencyTests
{
    [Fact]
    public async Task UpdateAsync_WhenRowDeletedByAnotherActor_ThrowsDeletedConflict()
    {
        var timeService = new KoreaTimeService();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        await SeedSingleScheduleAsync(factory, timeService, rowVersion: new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 });

        var sut = new ScheduleService(factory, timeService);
        var stale = await sut.GetByIdAsync(1, CancellationToken.None);
        Assert.NotNull(stale);

        await using (var db = factory.CreateDbContext())
        {
            var existing = await db.Schedules.FindAsync(1);
            Assert.NotNull(existing);
            db.Schedules.Remove(existing!);
            await db.SaveChangesAsync();
        }

        stale!.Title = "삭제 이후 수정 시도";
        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => sut.UpdateAsync(stale));

        Assert.True(ex.IsDeleted);
        Assert.Null(ex.Latest);
    }

    [Fact]
    public async Task UpdateAsync_WhenRowUpdatedByAnotherActor_ThrowsConflictWithLatestData()
    {
        var timeService = new KoreaTimeService();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        await SeedSingleScheduleAsync(factory, timeService, rowVersion: new byte[] { 1, 0, 0, 0, 0, 0, 0, 1 });

        var sut = new ScheduleService(factory, timeService);
        var stale = await sut.GetByIdAsync(1, CancellationToken.None);
        Assert.NotNull(stale);

        await using (var db = factory.CreateDbContext())
        {
            var existing = await db.Schedules.FindAsync(1);
            Assert.NotNull(existing);

            existing!.Title = "다른 사용자가 먼저 수정";
            existing.RowVersion = new byte[] { 2, 0, 0, 0, 0, 0, 0, 2 };
            await db.SaveChangesAsync();
        }

        stale!.Title = "내 수정";
        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => sut.UpdateAsync(stale));

        Assert.False(ex.IsDeleted);
        Assert.NotNull(ex.Latest);
        Assert.Equal("다른 사용자가 먼저 수정", ex.Latest!.Title);
    }

    private static async Task SeedSingleScheduleAsync(
        InMemoryDbContextFactory factory,
        ITimeService timeService,
        byte[] rowVersion)
    {
        await using var db = factory.CreateDbContext();

        db.Schedules.Add(new ScheduleItem
        {
            Id = 1,
            Title = "원본 일정",
            StartAt = timeService.KoreaToUtc(new DateTime(2026, 2, 13, 9, 0, 0, DateTimeKind.Unspecified)),
            EndAt = timeService.KoreaToUtc(new DateTime(2026, 2, 13, 10, 0, 0, DateTimeKind.Unspecified)),
            RowVersion = rowVersion
        });

        await db.SaveChangesAsync();
    }
}
