using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MyScheduler.IntegrationTests.Services;

public class ScheduleSearchPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ScheduleSearchPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SearchStrategies_ReportMinP95AndAverageMs_Over100Runs()
    {
        const int totalRows = 50_000;
        const int matchedRows = 10_000;
        const int iterations = 100;
        const string keyword = "MEET";
        const string keywordNormalized = "MEET";

        var baseConnection = ResolveBaseConnectionString();
        var benchmarkDbName = $"MySchedulerPerf_{Guid.NewGuid():N}";
        var benchmarkConnection = BuildBenchmarkConnectionString(baseConnection, benchmarkDbName);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(benchmarkConnection)
            .Options;

        await using var setupDb = new AppDbContext(options);
        try
        {
            await EnsureSqlServerAvailableAsync(baseConnection);
            await setupDb.Database.EnsureDeletedAsync();
            await setupDb.Database.EnsureCreatedAsync();

            await SeedAsync(setupDb, totalRows, matchedRows, keyword);

            var date = new DateTime(2026, 2, 13);
            var startUtc = date;
            var endUtc = date.AddDays(1);

            await RunWarmupAsync(options, startUtc, endUtc, keyword, keywordNormalized);

            var toUpperContains = await MeasureAsync(
                iterations,
                () => ExecuteToUpperContainsAsync(options, startUtc, endUtc, keyword));

            var likeContains = await MeasureAsync(
                iterations,
                () => ExecuteSqlLikeContainsAsync(options, startUtc, endUtc, keywordNormalized));

            var likePrefix = await MeasureAsync(
                iterations,
                () => ExecuteSqlLikePrefixAsync(options, startUtc, endUtc, keywordNormalized));

            var toUpperStats = Metrics.From(toUpperContains);
            var likeContainsStats = Metrics.From(likeContains);
            var likePrefixStats = Metrics.From(likePrefix);

            _output.WriteLine("Strategy | Min(ms) | P95(ms) | Avg(ms)");
            _output.WriteLine("----------------------------------------");
            _output.WriteLine($"ToUpper.Contains (client filter) | {toUpperStats.MinMs:F3} | {toUpperStats.P95Ms:F3} | {toUpperStats.AvgMs:F3}");
            _output.WriteLine($"WHERE [TitleNormalized] LIKE '%keyword%' | {likeContainsStats.MinMs:F3} | {likeContainsStats.P95Ms:F3} | {likeContainsStats.AvgMs:F3}");
            _output.WriteLine($"WHERE [TitleNormalized] LIKE 'keyword%' | {likePrefixStats.MinMs:F3} | {likePrefixStats.P95Ms:F3} | {likePrefixStats.AvgMs:F3}");
            _output.WriteLine("");
            _output.WriteLine($"Condition: totalRows={totalRows}, matchedRows={matchedRows}, dateRange=1day, iterations={iterations}, keyword='{keyword}', db='{benchmarkDbName}'");
        }
        finally
        {
            await setupDb.Database.EnsureDeletedAsync();
        }
    }

    private static async Task EnsureSqlServerAvailableAsync(string baseConnection)
    {
        var builder = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = "master"
        };
        builder["Encrypt"] = "False";
        builder["TrustServerCertificate"] = "True";

        try
        {
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
        }
        catch (SqlException)
        {
            throw new XunitException("SQL Server connection failed: verify SQL Server instance and connectivity.");
        }
    }

    private static async Task SeedAsync(AppDbContext db, int totalRows, int matchedRows, string keyword)
    {
        var seedDate = new DateTime(2026, 2, 13);
        const int batchSize = 1000;

        var inserted = 0;
        while (inserted < totalRows)
        {
            var count = Math.Min(batchSize, totalRows - inserted);
            var batch = new List<ScheduleItem>(capacity: count);

            for (var i = 0; i < count; i++)
            {
                var globalIndex = inserted + i;
                var isMatch = globalIndex < matchedRows;
                var title = isMatch
                    ? $"{keyword}{globalIndex:D6} PLANNING"
                    : $"TASK{globalIndex:D6} REVIEW";

                batch.Add(new ScheduleItem
                {
                    Title = title,
                    StartAt = seedDate,
                    EndAt = seedDate.AddHours(1),
                    Priority = globalIndex % 3,
                    IsAllDay = false
                });
            }

            db.Schedules.AddRange(batch);
            await db.SaveChangesAsync();
            inserted += count;
        }
    }

    private static async Task RunWarmupAsync(
        DbContextOptions<AppDbContext> options,
        DateTime startUtc,
        DateTime endUtc,
        string keyword,
        string keywordNormalized)
    {
        await ExecuteToUpperContainsAsync(options, startUtc, endUtc, keyword);
        await ExecuteSqlLikeContainsAsync(options, startUtc, endUtc, keywordNormalized);
        await ExecuteSqlLikePrefixAsync(options, startUtc, endUtc, keywordNormalized);
    }

    private static async Task<int> ExecuteToUpperContainsAsync(
        DbContextOptions<AppDbContext> options,
        DateTime startUtc,
        DateTime endUtc,
        string keyword)
    {
        var keywordUpper = keyword.ToUpperInvariant();

        await using var db = new AppDbContext(options);
        var titles = await db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt > startUtc)
            .Select(x => x.Title)
            .ToListAsync();

        return titles.Count(x => x.ToUpperInvariant().Contains(keywordUpper, StringComparison.Ordinal));
    }

    private static async Task<int> ExecuteSqlLikeContainsAsync(
        DbContextOptions<AppDbContext> options,
        DateTime startUtc,
        DateTime endUtc,
        string keywordNormalized)
    {
        var pattern = $"%{keywordNormalized}%";

        await using var db = new AppDbContext(options);
        return await db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt > startUtc)
            .Where(x => EF.Functions.Like(x.TitleNormalized, pattern))
            .CountAsync();
    }

    private static async Task<int> ExecuteSqlLikePrefixAsync(
        DbContextOptions<AppDbContext> options,
        DateTime startUtc,
        DateTime endUtc,
        string keywordNormalized)
    {
        var pattern = $"{keywordNormalized}%";

        await using var db = new AppDbContext(options);
        return await db.Schedules
            .AsNoTracking()
            .Where(x => x.StartAt < endUtc && x.EndAt > startUtc)
            .Where(x => EF.Functions.Like(x.TitleNormalized, pattern))
            .CountAsync();
    }

    private static async Task<List<double>> MeasureAsync(int iterations, Func<Task<int>> operation)
    {
        var results = new List<double>(capacity: iterations);
        for (var i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _ = await operation();
            sw.Stop();
            results.Add(sw.Elapsed.TotalMilliseconds);
        }

        return results;
    }

    private static string ResolveBaseConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MYSCHEDULER_BENCHMARK_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var appsettingsPath = Path.Combine(current.FullName, "appsettings.json");
            if (File.Exists(appsettingsPath))
            {
                var json = File.ReadAllText(appsettingsPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connSection) &&
                    connSection.TryGetProperty("Default", out var defaultConn) &&
                    !string.IsNullOrWhiteSpace(defaultConn.GetString()))
                {
                    return defaultConn.GetString()!;
                }
            }

            current = current.Parent;
        }

        return "Server=localhost\\SQLEXPRESS;Database=MySchedulerDb;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    private static string BuildBenchmarkConnectionString(string baseConnection, string benchmarkDbName)
    {
        var builder = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = benchmarkDbName
        };
        builder["Encrypt"] = "False";
        builder["TrustServerCertificate"] = "True";
        return builder.ToString();
    }

    private sealed record Metrics(double MinMs, double P95Ms, double AvgMs)
    {
        public static Metrics From(List<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            var min = sorted.First();
            var avg = sorted.Average();
            var p95Index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
            var p95 = sorted[Math.Clamp(p95Index, 0, sorted.Length - 1)];
            return new Metrics(min, p95, avg);
        }
    }
}
