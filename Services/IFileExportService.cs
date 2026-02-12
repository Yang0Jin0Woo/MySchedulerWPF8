namespace MyScheduler.Services;

public interface IFileExportService
{
    Task WriteAllBytesAsync(string filePath, byte[] bytes, CancellationToken cancellationToken = default);
}
