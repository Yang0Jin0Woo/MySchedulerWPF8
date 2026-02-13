using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.Utils;

namespace MyScheduler.UnitTests.Services;

public class ScheduleServiceValidationTests
{
    private static ScheduleService CreateSut()
    {
        return new ScheduleService(new ThrowingDbContextFactory(), new KoreaTimeService());
    }

    [Fact]
    public async Task AddAsync_WhenEndIsNotAfterStart_ThrowsDomainValidationException()
    {
        var sut = CreateSut();
        var item = new ScheduleItem
        {
            Title = "검증 테스트",
            StartAt = new DateTime(2026, 2, 13, 10, 0, 0, DateTimeKind.Unspecified),
            EndAt = new DateTime(2026, 2, 13, 10, 0, 0, DateTimeKind.Unspecified)
        };

        await Assert.ThrowsAsync<DomainValidationException>(() => sut.AddAsync(item));
    }

    [Fact]
    public async Task AddAsync_WhenUtcKindInputProvided_ThrowsDomainValidationException()
    {
        var sut = CreateSut();
        var item = new ScheduleItem
        {
            Title = "UTC 입력",
            StartAt = new DateTime(2026, 2, 13, 1, 0, 0, DateTimeKind.Utc),
            EndAt = new DateTime(2026, 2, 13, 2, 0, 0, DateTimeKind.Unspecified)
        };

        await Assert.ThrowsAsync<DomainValidationException>(() => sut.AddAsync(item));
    }

    [Fact]
    public async Task UpdateAsync_WhenRowVersionMissing_ThrowsConcurrencyConflictException()
    {
        var sut = CreateSut();
        var item = new ScheduleItem
        {
            Id = 1,
            Title = "수정",
            StartAt = new DateTime(2026, 2, 13, 9, 0, 0, DateTimeKind.Unspecified),
            EndAt = new DateTime(2026, 2, 13, 10, 0, 0, DateTimeKind.Unspecified),
            RowVersion = null
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => sut.UpdateAsync(item));
    }

    [Fact]
    public async Task DeleteAsync_WhenRowVersionEmpty_ThrowsArgumentException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAsync(1, Array.Empty<byte>()));
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => throw new InvalidOperationException("DbContext should not be created in validation tests.");
    }

    private sealed class KoreaTimeService : ITimeService
    {
        public DateTime GetKoreaNow() => TimeUtil.GetKoreaNow();
        public DateTime UtcToKorea(DateTime utc) => TimeUtil.UtcToKorea(utc);
        public DateTime KoreaToUtc(DateTime korea) => TimeUtil.KoreaToUtc(korea);
    }
}
