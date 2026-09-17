using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CheckOutTime = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Present"),
                    CheckInSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CheckOutSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    VerifyMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FaceCaptureImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HikvisionSerialNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    break_duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kiosk_pin_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BadgeRfidCard = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HikvisionUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsBiometricEnrolled = table.Column<bool>(type: "boolean", nullable: false),
                    FacePhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hikvision_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "DS-K1T320MFWX"),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                    AdminUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdminPasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AdminPasswordProtected = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastEventReceivedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    LastPollAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EnrolledEmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hikvision_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    GracePeriodMinutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeThresholdMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsNightShift = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BreakRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StartSource = table.Column<int>(type: "integer", nullable: false),
                    EndSource = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreakRecords_attendance_records_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "attendance_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hikvision_event_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceSerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeviceLocalTime = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttendanceStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VerifyMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CardNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FaceCaptureStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hikvision_event_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hikvision_event_logs_attendance_records_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "attendance_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "hourly_slots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    hour_slot = table.Column<int>(type: "integer", nullable: false),
                    minutes_worked = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsBreak = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsOvertime = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hourly_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hourly_slots_attendance_records_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "attendance_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_employee_id_WorkDate",
                table: "attendance_records",
                columns: new[] { "employee_id", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorId",
                table: "audit_logs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OccurredAt",
                table: "audit_logs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_BreakRecords_AttendanceRecordId",
                table: "BreakRecords",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakRecords_EmployeeId",
                table: "BreakRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakRecords_EmployeeId_StartTime",
                table: "BreakRecords",
                columns: new[] { "EmployeeId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_employees_Email",
                table: "employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_EmployeeCode",
                table: "employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hikvision_devices_IpAddress",
                table: "hikvision_devices",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hikvision_devices_SerialNumber",
                table: "hikvision_devices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hikvision_event_logs_AttendanceRecordId",
                table: "hikvision_event_logs",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_hikvision_event_logs_DeviceSerialNumber_DeviceLocalTime_Emp~",
                table: "hikvision_event_logs",
                columns: new[] { "DeviceSerialNumber", "DeviceLocalTime", "EmployeeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hikvision_event_logs_IsProcessed",
                table: "hikvision_event_logs",
                column: "IsProcessed",
                filter: "\"IsProcessed\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_hourly_slots_AttendanceRecordId_hour_slot",
                table: "hourly_slots",
                columns: new[] { "AttendanceRecordId", "hour_slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "BreakRecords");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "hikvision_devices");

            migrationBuilder.DropTable(
                name: "hikvision_event_logs");

            migrationBuilder.DropTable(
                name: "hourly_slots");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "attendance_records");
        }
    }
}
