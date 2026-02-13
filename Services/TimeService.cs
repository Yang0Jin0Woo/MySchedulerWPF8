using MyScheduler.Utils;

namespace MyScheduler.Services;

public class TimeService : ITimeService
{
    public DateTime GetKoreaNow()
        => TimeUtil.GetKoreaNow();

    public DateTime UtcToKorea(DateTime utc)
        => TimeUtil.UtcToKorea(utc);

    public DateTime KoreaToUtc(DateTime korea)
        => TimeUtil.KoreaToUtc(korea);
}
