using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;

public class AppDbContext : DbContext
{
    public DbSet<ScheduleItem> Schedules => Set<ScheduleItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var conn =
            "Server=localhost\\SQLEXPRESS;" +
            "Database=MySchedulerDb;" +
            "Trusted_Connection=True;" +
            "TrustServerCertificate=True;";

        optionsBuilder.UseSqlServer(conn);
    }
}
