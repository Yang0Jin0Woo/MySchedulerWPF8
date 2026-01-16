using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;
using System.IO;

namespace MyScheduler.Data;

public class AppDbContext : DbContext
{
    public DbSet<ScheduleItem> Schedules => Set<ScheduleItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "myscheduler.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduleItem>()
            .Property(x => x.IsAllDay)
            .HasDefaultValue(false);

        // 동시성 토큰
        modelBuilder.Entity<ScheduleItem>()
            .Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
