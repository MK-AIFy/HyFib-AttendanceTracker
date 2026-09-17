using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixDeviceLocalTimeColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DeviceLocalTime",
                table: "hikvision_event_logs",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DeviceLocalTime",
                table: "hikvision_event_logs",
                type: "timestamptz",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");
        }
    }
}
