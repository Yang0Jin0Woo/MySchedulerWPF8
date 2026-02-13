using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyScheduler.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260213123000_ShortenNormalizedColumnsForIndexSafety")]
    public partial class ShortenNormalizedColumnsForIndexSafety : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_StartAt_TitleNormalized_Id'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    DROP INDEX [IX_Schedules_StartAt_TitleNormalized_Id] ON [dbo].[Schedules];
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_StartAt_LocationNormalized_Id'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    DROP INDEX [IX_Schedules_StartAt_LocationNormalized_Id] ON [dbo].[Schedules];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Schedules', N'LocationNormalized') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Schedules] DROP COLUMN [LocationNormalized];
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Schedules]
ADD [LocationNormalized] AS
(
    CASE
        WHEN [Location] IS NULL THEN NULL
        ELSE CONVERT(nvarchar(256), UPPER(REPLACE(REPLACE(REPLACE(REPLACE([Location], N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N'')))
    END
) PERSISTED;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Schedules', N'TitleNormalized') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Schedules] DROP COLUMN [TitleNormalized];
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Schedules]
ADD [TitleNormalized] AS
(
    CONVERT(nvarchar(256), UPPER(REPLACE(REPLACE(REPLACE(REPLACE([Title], N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N'')))
) PERSISTED;
");

            migrationBuilder.Sql(@"
CREATE INDEX [IX_Schedules_StartAt_TitleNormalized_Id]
ON [dbo].[Schedules] ([StartAt], [TitleNormalized], [Id]);
");

            migrationBuilder.Sql(@"
CREATE INDEX [IX_Schedules_StartAt_LocationNormalized_Id]
ON [dbo].[Schedules] ([StartAt], [LocationNormalized], [Id]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_StartAt_TitleNormalized_Id'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    DROP INDEX [IX_Schedules_StartAt_TitleNormalized_Id] ON [dbo].[Schedules];
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_StartAt_LocationNormalized_Id'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    DROP INDEX [IX_Schedules_StartAt_LocationNormalized_Id] ON [dbo].[Schedules];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Schedules', N'LocationNormalized') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Schedules] DROP COLUMN [LocationNormalized];
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Schedules]
ADD [LocationNormalized] AS
(
    CASE
        WHEN [Location] IS NULL THEN NULL
        ELSE CONVERT(nvarchar(450), UPPER(REPLACE(REPLACE(REPLACE(REPLACE([Location], N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N'')))
    END
) PERSISTED;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.Schedules', N'TitleNormalized') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Schedules] DROP COLUMN [TitleNormalized];
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Schedules]
ADD [TitleNormalized] AS
(
    CONVERT(nvarchar(450), UPPER(REPLACE(REPLACE(REPLACE(REPLACE([Title], N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N'')))
) PERSISTED;
");

            migrationBuilder.Sql(@"
CREATE INDEX [IX_Schedules_StartAt_TitleNormalized_Id]
ON [dbo].[Schedules] ([StartAt], [TitleNormalized], [Id]);
");

            migrationBuilder.Sql(@"
CREATE INDEX [IX_Schedules_StartAt_LocationNormalized_Id]
ON [dbo].[Schedules] ([StartAt], [LocationNormalized], [Id]);
");
        }
    }
}
