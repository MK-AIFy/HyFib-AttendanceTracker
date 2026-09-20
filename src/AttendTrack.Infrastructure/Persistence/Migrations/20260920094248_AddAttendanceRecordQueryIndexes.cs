using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecordQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_OpenCheckIns",
                table: "attendance_records",
                column: "CheckInTime",
                filter: "\"CheckOutTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_WorkDate",
                table: "attendance_records",
                column: "WorkDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attendance_records_OpenCheckIns",
                table: "attendance_records");

            migrationBuilder.DropIndex(
                name: "IX_attendance_records_WorkDate",
                table: "attendance_records");
        }
    }
}
