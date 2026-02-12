using System.IO;

namespace MyScheduler.Services;

public class FileExportService : IFileExportService
{
    public Task WriteAllBytesAsync(string filePath, byte[] bytes, CancellationToken cancellationToken = default)
    {
        return File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
    }
}
