using Microsoft.EntityFrameworkCore;
using MyScheduler.Models;

namespace MyScheduler;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ScheduleItem> Schedules => Set<ScheduleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => x.StartAt);

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => x.EndAt);

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => new { x.StartAt, x.EndAt });

        modelBuilder.Entity<ScheduleItem>()
            .Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
