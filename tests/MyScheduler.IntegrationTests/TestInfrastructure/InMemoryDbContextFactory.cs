using Microsoft.EntityFrameworkCore;

namespace MyScheduler.IntegrationTests.TestInfrastructure;

public sealed class InMemoryDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public InMemoryDbContextFactory(string databaseName)
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    public AppDbContext CreateDbContext()
        => new(_options);
}
