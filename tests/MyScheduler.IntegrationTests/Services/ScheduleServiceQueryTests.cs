using MyScheduler.IntegrationTests.TestInfrastructure;
using MyScheduler.Models;
using MyScheduler.Services;

namespace MyScheduler.IntegrationTests.Services;

public class ScheduleServiceQueryTests
{
    [Fact]
    public async Task GetListByDateAsync_UsesHalfOpenDateRange()
    {
        var timeService = new KoreaTimeService();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        await SeedHalfOpenRangeDataAsync(factory, timeService);

        var sut = new ScheduleService(factory, timeService);

        var (items, totalCount) = await sut.GetListByDateAsync(
            new DateTime(2026, 2, 13),
            searchText: null,
            searchScope: null,
            page: 1,
            pageSize: 10,
            CancellationToken.None);

        Assert.Equal(2, totalCount);
        Assert.Equal(new[] { "아침 회의", "심야 작업" }, items.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task GetListByDateAsync_ReturnsPagedItemsOrderedByStartAtThenId()
    {
        var timeService = new KoreaTimeService();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));

        await using (var db = factory.CreateDbContext())
        {
            db.Schedules.AddRange(
                CreateSchedule(1, "첫 일정", new DateTime(2026, 2, 13, 8, 0, 0), new DateTime(2026, 2, 13, 9, 0, 0), timeService),
                CreateSchedule(2, "둘 일정", new DateTime(2026, 2, 13, 9, 0, 0), new DateTime(2026, 2, 13, 10, 0, 0), timeService),
                CreateSchedule(3, "셋 일정", new DateTime(2026, 2, 13, 10, 0, 0), new DateTime(2026, 2, 13, 11, 0, 0), timeService));
            await db.SaveChangesAsync();
        }

        var sut = new ScheduleService(factory, timeService);

        var page1 = await sut.GetListByDateAsync(new DateTime(2026, 2, 13), null, null, page: 1, pageSize: 2, CancellationToken.None);
        var page2 = await sut.GetListByDateAsync(new DateTime(2026, 2, 13), null, null, page: 2, pageSize: 2, CancellationToken.None);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(new[] { "첫 일정", "둘 일정" }, page1.Items.Select(x => x.Title).ToArray());
        Assert.Single(page2.Items);
        Assert.Equal("셋 일정", page2.Items[0].Title);
    }

    private static async Task SeedHalfOpenRangeDataAsync(InMemoryDbContextFactory factory, ITimeService timeService)
    {
        await using var db = factory.CreateDbContext();

        db.Schedules.AddRange(
            CreateSchedule(1, "아침 회의", new DateTime(2026, 2, 13, 9, 0, 0), new DateTime(2026, 2, 13, 10, 0, 0), timeService),
            CreateSchedule(2, "전날 일정", new DateTime(2026, 2, 12, 23, 0, 0), new DateTime(2026, 2, 13, 0, 0, 0), timeService),
            CreateSchedule(3, "심야 작업", new DateTime(2026, 2, 13, 23, 30, 0), new DateTime(2026, 2, 14, 0, 30, 0), timeService),
            CreateSchedule(4, "다음날 오전", new DateTime(2026, 2, 14, 8, 0, 0), new DateTime(2026, 2, 14, 9, 0, 0), timeService));

        await db.SaveChangesAsync();
    }

    private static ScheduleItem CreateSchedule(int id, string title, DateTime startKst, DateTime endKst, ITimeService timeService)
    {
        return new ScheduleItem
        {
            Id = id,
            Title = title,
            StartAt = timeService.KoreaToUtc(DateTime.SpecifyKind(startKst, DateTimeKind.Unspecified)),
            EndAt = timeService.KoreaToUtc(DateTime.SpecifyKind(endKst, DateTimeKind.Unspecified)),
            RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, (byte)id }
        };
    }
}
