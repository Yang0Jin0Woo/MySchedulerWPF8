namespace MyScheduler.Services;

public interface ITimeService
{
    DateTime GetKoreaNow();
    DateTime UtcToKorea(DateTime utc);
    DateTime KoreaToUtc(DateTime korea);
}
