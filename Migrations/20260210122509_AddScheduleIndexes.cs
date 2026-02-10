using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyScheduler.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Schedules_EndAt",
                table: "Schedules",
                column: "EndAt");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_StartAt",
                table: "Schedules",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_StartAt_EndAt",
                table: "Schedules",
                columns: new[] { "StartAt", "EndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Schedules_EndAt",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_StartAt",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_StartAt_EndAt",
                table: "Schedules");
        }
    }
}
