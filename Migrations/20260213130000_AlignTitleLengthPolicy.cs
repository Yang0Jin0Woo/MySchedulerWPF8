using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyScheduler.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260213130000_AlignTitleLengthPolicy")]
    public partial class AlignTitleLengthPolicy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [dbo].[Schedules] WHERE LEN([Title]) > 256)
BEGIN
    THROW 50000, N'Title length exceeds 256 characters. Please clean data before migration.', 1;
END
");

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
IF COL_LENGTH(N'dbo.Schedules', N'TitleNormalized') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Schedules] DROP COLUMN [TitleNormalized];
END
");

            migrationBuilder.Sql(@"
DECLARE @dfTitle sysname;
SELECT @dfTitle = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Schedules')
  AND c.name = N'Title';
IF @dfTitle IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[Schedules] DROP CONSTRAINT [' + @dfTitle + N']');
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Schedules] ALTER COLUMN [Title] nvarchar(256) NOT NULL;
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
IF COL_LENGTH(N'dbo.Schedules', N'TitleNormalized') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Schedules] DROP COLUMN [TitleNormalized];
END
");

            migrationBuilder.Sql(@"
DECLARE @dfTitle sysname;
SELECT @dfTitle = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Schedules')
  AND c.name = N'Title';
IF @dfTitle IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[Schedules] DROP CONSTRAINT [' + @dfTitle + N']');
END
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Schedules] ALTER COLUMN [Title] nvarchar(450) NOT NULL;
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
        }
    }
}
