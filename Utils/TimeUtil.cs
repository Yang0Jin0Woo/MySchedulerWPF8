using System;

namespace MyScheduler.Utils;

public static class TimeUtil
{
    public static DateTime GetKoreaNow()
    {
        // UTC 기준
        var utcNow = DateTimeOffset.UtcNow;
        var tz = GetKoreaTimeZoneInfo();

        return TimeZoneInfo.ConvertTime(utcNow, tz).DateTime;
    }

    private static TimeZoneInfo GetKoreaTimeZoneInfo()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
    }
}
