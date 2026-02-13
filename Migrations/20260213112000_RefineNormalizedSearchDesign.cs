using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyScheduler.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260213112000_RefineNormalizedSearchDesign")]
    public partial class RefineNormalizedSearchDesign : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Schedules",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_LocationNormalized'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    DROP INDEX [IX_Schedules_LocationNormalized] ON [dbo].[Schedules];
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_TitleNormalized'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    DROP INDEX [IX_Schedules_TitleNormalized] ON [dbo].[Schedules];
END
");

            migrationBuilder.Sql(@"
DECLARE @dfLocationNormalized sysname;
SELECT @dfLocationNormalized = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Schedules')
  AND c.name = N'LocationNormalized';
IF @dfLocationNormalized IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[Schedules] DROP CONSTRAINT [' + @dfLocationNormalized + N']');
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
DECLARE @dfTitleNormalized sysname;
SELECT @dfTitleNormalized = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Schedules')
  AND c.name = N'TitleNormalized';
IF @dfTitleNormalized IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[Schedules] DROP CONSTRAINT [' + @dfTitleNormalized + N']');
END
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
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_StartAt_TitleNormalized_Id'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    CREATE INDEX [IX_Schedules_StartAt_TitleNormalized_Id]
    ON [dbo].[Schedules] ([StartAt], [TitleNormalized], [Id]);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_StartAt_LocationNormalized_Id'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    CREATE INDEX [IX_Schedules_StartAt_LocationNormalized_Id]
    ON [dbo].[Schedules] ([StartAt], [LocationNormalized], [Id]);
END
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
DECLARE @dfLocationNormalized sysname;
SELECT @dfLocationNormalized = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Schedules')
  AND c.name = N'LocationNormalized';
IF @dfLocationNormalized IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[Schedules] DROP CONSTRAINT [' + @dfLocationNormalized + N']');
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
    CONVERT(nvarchar(450), UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL([Location], N''))), N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''), N'-', N''), N'_', N''), N'/', N''), N'.', N'')))
) PERSISTED;
");

            migrationBuilder.Sql(@"
DECLARE @dfTitleNormalized sysname;
SELECT @dfTitleNormalized = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Schedules')
  AND c.name = N'TitleNormalized';
IF @dfTitleNormalized IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[Schedules] DROP CONSTRAINT [' + @dfTitleNormalized + N']');
END
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
    CONVERT(nvarchar(450), UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL([Title], N''))), N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''), N'-', N''), N'_', N''), N'/', N''), N'.', N'')))
) PERSISTED;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_LocationNormalized'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    CREATE INDEX [IX_Schedules_LocationNormalized] ON [dbo].[Schedules] ([LocationNormalized]);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Schedules_TitleNormalized'
      AND object_id = OBJECT_ID(N'dbo.Schedules'))
BEGIN
    CREATE INDEX [IX_Schedules_TitleNormalized] ON [dbo].[Schedules] ([TitleNormalized]);
END
");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Schedules",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
