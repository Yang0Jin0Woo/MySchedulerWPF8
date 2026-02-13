using MyScheduler.Services;
using MyScheduler.Utils;

namespace MyScheduler.IntegrationTests.TestInfrastructure;

public sealed class KoreaTimeService : ITimeService
{
    public DateTime GetKoreaNow() => TimeUtil.GetKoreaNow();
    public DateTime UtcToKorea(DateTime utc) => TimeUtil.UtcToKorea(utc);
    public DateTime KoreaToUtc(DateTime korea) => TimeUtil.KoreaToUtc(korea);
}
