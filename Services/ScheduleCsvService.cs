using MyScheduler.Models;
using System.Globalization;
using System.Text;

namespace MyScheduler.Services;

public class ScheduleCsvService : IScheduleCsvService
{
    public byte[] BuildCsvBytes(IEnumerable<ScheduleListItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Title,Location,StartAt,EndAt");

        foreach (var r in items)
        {
            sb.Append(EscapeCsv(r.Id.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(r.Title)).Append(',');
            sb.Append(EscapeCsv(r.Location)).Append(',');
            sb.Append(EscapeCsv(r.StartAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(r.EndAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        var s = value ?? "";
        var mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');

        if (s.Contains('"'))
            s = s.Replace("\"", "\"\"");

        return mustQuote ? $"\"{s}\"" : s;
    }
}
