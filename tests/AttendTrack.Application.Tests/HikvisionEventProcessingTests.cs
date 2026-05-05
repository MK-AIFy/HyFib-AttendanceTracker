using AttendTrack.Application.Commands.Hikvision;
using AttendTrack.Application.Common;
using AttendTrack.Application.Options;
using AttendTrack.Application.Parsers;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

// Alias to avoid conflict with AttendTrack.Application.Options namespace
using OptionsHelper = Microsoft.Extensions.Options.Options;

namespace AttendTrack.Application.Tests;

public sealed class HikvisionEventProcessingTests
{
    // ── Sample XML payloads ────────────────────────────────────────────────────

    /// <summary>DS-K1T320MFWX checkIn event with IST dateTime offset (+05:30).</summary>
    private const string CheckInXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <ipAddress>192.168.1.50</ipAddress>
          <macAddress>AA:BB:CC:DD:EE:FF</macAddress>
          <channelID>1</channelID>
          <dateTime>2025-01-15T09:03:25+05:30</dateTime>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <employeeNoString>EMP-001</employeeNoString>
            <name>John Doe</name>
            <currentVerifyMode>faceAndFp</currentVerifyMode>
            <attendanceStatus>checkIn</attendanceStatus>
            <serialNo>33</serialNo>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;

    /// <summary>Event with Unix epoch timestamp instead of dateTime element.</summary>
    private const string EpochXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <macAddress>AA:BB:CC:DD:EE:FF</macAddress>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <employeeNoString>EMP-001</employeeNoString>
            <name>John Doe</name>
            <currentVerifyMode>face</currentVerifyMode>
            <attendanceStatus>checkIn</attendanceStatus>
            <time>1736918605</time>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;

    // ─── Test 1: Parser — DateTimeOffset path ─────────────────────────────────

    [Fact]
    public void ParseXml_WithDateTimeOffset_ReturnsCorrectUtc()
    {
        // Device sends IST: 2025-01-15T09:03:25+05:30
        // UTC equivalent:   09:03:25 − 05:30 = 03:33:25 UTC
        var result = HikvisionEventParser.Parse(CheckInXml);

        result.EventTimeUtc.Should().Be(new DateTime(2025, 1, 15, 3, 33, 25, DateTimeKind.Utc));
        result.EmployeeNoString.Should().Be("EMP-001");
        result.AttendanceStatus.Should().Be("checkIn");
        result.CurrentVerifyMode.Should().Be("faceAndFp");
        result.DeviceSerial.Should().Be("AA:BB:CC:DD:EE:FF");
    }

    // ─── Test 2: Parser — Unix epoch fallback ─────────────────────────────────

    [Fact]
    public void ParseXml_WithUnixEpoch_ReturnsCorrectUtc()
    {
        var expectedUtc = DateTimeOffset.FromUnixTimeSeconds(1736918605L).UtcDateTime;

        var result = HikvisionEventParser.Parse(EpochXml);

        result.EventTimeUtc.Should().Be(expectedUtc);
        result.EmployeeNoString.Should().Be("EMP-001");
    }

    // ─── Handler test helpers ──────────────────────────────────────────────────

    private static (
        ProcessHikvisionEventHandler Handler,
        Mock<IHikvisionDeviceRepository> DeviceRepo,
        Mock<IEmployeeRepository>        EmployeeRepo,
        Mock<IAttendanceRepository>      AttendanceRepo,
        Mock<IShiftRepository>           ShiftRepo,
        Mock<IUnitOfWork>                Uow)
    BuildHandler(string? faceCaptureBasePath = null)
    {
        var deviceRepo     = new Mock<IHikvisionDeviceRepository>();
        var employeeRepo   = new Mock<IEmployeeRepository>();
        var attendanceRepo = new Mock<IAttendanceRepository>();
        var shiftRepo      = new Mock<IShiftRepository>();
        var uow            = new Mock<IUnitOfWork>();
        var notifier       = new Mock<IAttendanceNotifier>();
        var logger         = new Mock<ILogger<ProcessHikvisionEventHandler>>();
        var options        = OptionsHelper.Create(new HikvisionOptions
        {
            FaceCaptureBasePath = faceCaptureBasePath ?? Path.GetTempPath()
        });

        // Non-critical defaults
        deviceRepo.Setup(r => r.GetBySerialAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HikvisionDevice?)null);
        deviceRepo.Setup(r => r.AddEventLogAsync(It.IsAny<HikvisionEventLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        deviceRepo.Setup(r => r.UpdateLastEventAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new ProcessHikvisionEventHandler(
            deviceRepo.Object,
            employeeRepo.Object,
            attendanceRepo.Object,
            shiftRepo.Object,
            uow.Object,
            options,
            notifier.Object,
            logger.Object);

        return (handler, deviceRepo, employeeRepo, attendanceRepo, shiftRepo, uow);
    }

    private static Employee MakeEmployee(string code = "EMP-001")
    {
        var empId     = new EmployeeId(Guid.NewGuid());
        var deptId    = Guid.NewGuid();
        var shiftId   = Guid.NewGuid();
        return Employee.Create(
            id:            empId,
            employeeCode:  code,
            fullName:      "John Doe",
            email:         $"{code}@test.com",
            phone:         "9999999999",
            kioskPin:      PinHash.From("$2a$12$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA."),
            departmentId:  deptId,
            defaultShiftId: shiftId,
            role:          UserRole.Employee,
            joinedAt:      DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)));
    }

    private static Shift MakeShift()
        => Shift.Create(
            id:        Guid.NewGuid(),
            name:      "Morning",
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(18, 0),
            gracePeriodMinutes: 15);

    // ─── Test 3: Happy-path checkIn creates record with PunchSource.Hikvision ─

    [Fact]
    public async Task Handle_CheckInEvent_CreatesRecord_PunchSourceHikvision()
    {
        var (handler, _, employeeRepo, attendanceRepo, shiftRepo, _) = BuildHandler();
        var employee = MakeEmployee();
        var shift    = MakeShift();

        employeeRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        shiftRepo.Setup(r => r.GetByIdAsync(employee.DefaultShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        attendanceRepo.Setup(r => r.GetByEmployeeAndDateAsync(
            It.IsAny<EmployeeId>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        attendanceRepo.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd    = new ProcessHikvisionEventCommand(CheckInXml, null, DateTime.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.AttendanceRecordId.Should().NotBeNull();
        result.EmployeeCode.Should().Be("EMP-001");
        result.AttendanceStatus.Should().Be("checkIn");
        result.VerifyMode.Should().Be("faceAndFp");
        result.IsUnrecognized.Should().BeFalse();

        attendanceRepo.Verify(r =>
            r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Test 4: Duplicate event is handled without throwing ──────────────────

    [Fact]
    public async Task Handle_DuplicateEvent_IsIdempotent_NoSecondRecord()
    {
        var (handler, _, employeeRepo, attendanceRepo, shiftRepo, _) = BuildHandler();
        var employee = MakeEmployee();
        var shift    = MakeShift();

        // Simulate existing check-in for today
        var existingRecord = AttendanceRecord.CheckIn(
            employeeId:     employee.Id,
            shift:          shift,
            workDate:       DateOnly.FromDateTime(DateTime.UtcNow),
            checkInTimeUtc: DateTime.UtcNow.AddHours(-1),
            source:         PunchSource.Hikvision);

        employeeRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        shiftRepo.Setup(r => r.GetByIdAsync(employee.DefaultShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        // Return existing record → handler will throw AlreadyCheckedInException internally
        attendanceRepo.Setup(r => r.GetByEmployeeAndDateAsync(
            It.IsAny<EmployeeId>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRecord);

        var cmd = new ProcessHikvisionEventCommand(CheckInXml, null, DateTime.UtcNow);

        // Handler must NOT propagate the exception
        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().NotThrowAsync();

        // AddAsync must never be called for the duplicate
        attendanceRepo.Verify(r =>
            r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Test 5: Unknown employee — no exception, result is unrecognized ──────

    [Fact]
    public async Task Handle_UnknownEmployee_LogsError_NoException()
    {
        var (handler, _, employeeRepo, _, _, _) = BuildHandler();

        employeeRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var cmd    = new ProcessHikvisionEventCommand(CheckInXml, null, DateTime.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsUnrecognized.Should().BeTrue();
        result.AttendanceRecordId.Should().BeNull();
    }

    // ─── Test 6: JPEG face capture is persisted to disk ───────────────────────

    [Fact]
    public async Task Handle_FaceCaptureProvided_SavedToDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"attendtrack-test-{Guid.NewGuid()}");

        try
        {
            var (handler, _, employeeRepo, attendanceRepo, shiftRepo, _) = BuildHandler(tempDir);
            var employee  = MakeEmployee();
            var shift     = MakeShift();
            var faceBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG SOI marker

            employeeRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(employee);
            shiftRepo.Setup(r => r.GetByIdAsync(employee.DefaultShiftId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shift);
            attendanceRepo.Setup(r => r.GetByEmployeeAndDateAsync(
                It.IsAny<EmployeeId>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AttendanceRecord?)null);
            attendanceRepo.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var cmd = new ProcessHikvisionEventCommand(CheckInXml, faceBytes, DateTime.UtcNow);
            await handler.Handle(cmd, CancellationToken.None);

            var writtenFiles = Directory.GetFiles(tempDir, "*.jpg", SearchOption.AllDirectories);
            writtenFiles.Should().HaveCount(1, "one JPEG capture should be written per event");

            var savedBytes = await File.ReadAllBytesAsync(writtenFiles[0]);
            savedBytes.Should().Equal(faceBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

