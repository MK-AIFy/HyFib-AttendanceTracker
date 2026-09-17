using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDepartmentShiftForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_employees_DefaultShiftId",
                table: "employees",
                column: "DefaultShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_employees_DepartmentId",
                table: "employees",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_employees_Departments_DepartmentId",
                table: "employees",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_Shifts_DefaultShiftId",
                table: "employees",
                column: "DefaultShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employees_Departments_DepartmentId",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_Shifts_DefaultShiftId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_DefaultShiftId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_DepartmentId",
                table: "employees");
        }
    }
}
