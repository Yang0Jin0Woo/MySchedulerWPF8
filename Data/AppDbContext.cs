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
            .Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(256);

        modelBuilder.Entity<ScheduleItem>()
            .Property(x => x.TitleNormalized)
            .HasMaxLength(256)
            .HasComputedColumnSql(
                "CONVERT(nvarchar(256), UPPER(" +
                "REPLACE(REPLACE(REPLACE(REPLACE([Title], " +
                "N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N'')))",
                stored: true);

        modelBuilder.Entity<ScheduleItem>()
            .Property(x => x.LocationNormalized)
            .HasMaxLength(256)
            .HasComputedColumnSql(
                "CASE WHEN [Location] IS NULL THEN NULL ELSE " +
                "CONVERT(nvarchar(256), UPPER(" +
                "REPLACE(REPLACE(REPLACE(REPLACE([Location], " +
                "N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''))) " +
                "END",
                stored: true);

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => x.StartAt);

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => x.EndAt);

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => new { x.StartAt, x.EndAt });

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => new { x.StartAt, x.TitleNormalized, x.Id });

        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(x => new { x.StartAt, x.LocationNormalized, x.Id });

        modelBuilder.Entity<ScheduleItem>()
            .Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
