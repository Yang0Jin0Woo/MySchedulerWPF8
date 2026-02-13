using MyScheduler.Utils;

namespace MyScheduler.UnitTests.Utils;

public class TimeUtilTests
{
    [Fact]
    public void KoreaToUtc_KnownWallClock_ConvertsToExpectedUtc()
    {
        var koreaWallClock = new DateTime(2026, 2, 13, 9, 0, 0, DateTimeKind.Unspecified);

        var utc = TimeUtil.KoreaToUtc(koreaWallClock);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 2, 13, 0, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void UtcToKorea_KnownUtc_ConvertsToExpectedKoreaTime()
    {
        var utc = new DateTime(2026, 2, 13, 0, 0, 0, DateTimeKind.Utc);

        var korea = TimeUtil.UtcToKorea(utc);

        Assert.Equal(2026, korea.Year);
        Assert.Equal(2, korea.Month);
        Assert.Equal(13, korea.Day);
        Assert.Equal(9, korea.Hour);
        Assert.Equal(0, korea.Minute);
    }

    [Fact]
    public void KoreaToUtc_ThenUtcToKorea_RoundTripsWallClock()
    {
        var source = new DateTime(2026, 10, 1, 14, 35, 0, DateTimeKind.Unspecified);

        var utc = TimeUtil.KoreaToUtc(source);
        var roundTrip = TimeUtil.UtcToKorea(utc);

        Assert.Equal(source, roundTrip);
    }
}
