using MyScheduler.Models;

namespace MyScheduler.Services;

public interface IScheduleCsvService
{
    byte[] BuildCsvBytes(IEnumerable<ScheduleListItem> items);
}
