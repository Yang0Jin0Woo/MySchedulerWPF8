using System;

namespace MyScheduler.Utils;

public static class TimeUtil
{
    private static readonly TimeZoneInfo KoreaTimeZone = GetKoreaTimeZone();

    private static TimeZoneInfo GetKoreaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
    }

    public static DateTime GetKoreaNow()
    {
        var utcNow = DateTimeOffset.UtcNow;
        return TimeZoneInfo.ConvertTime(utcNow, KoreaTimeZone).DateTime;
    }

    public static DateTime KoreaToUtc(DateTime kst)
    {
        var unspecified = DateTime.SpecifyKind(kst, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, KoreaTimeZone);
    }

    public static DateTime UtcToKorea(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, KoreaTimeZone);
    }
}
