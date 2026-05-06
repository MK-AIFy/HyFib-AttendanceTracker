using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3IndexesAndConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BreakRecords_EmployeeId",
                table: "BreakRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakRecords_EmployeeId_StartTime",
                table: "BreakRecords",
                columns: new[] { "EmployeeId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BreakRecords_EmployeeId",
                table: "BreakRecords");

            migrationBuilder.DropIndex(
                name: "IX_BreakRecords_EmployeeId_StartTime",
                table: "BreakRecords");
        }
    }
}
