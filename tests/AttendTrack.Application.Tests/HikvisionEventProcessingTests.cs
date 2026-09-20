using AttendTrack.Application.Commands.Hikvision;
using AttendTrack.Application.Common;
using AttendTrack.Application.Options;
using AttendTrack.Application.Parsers;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
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

    // ─── Test 7: Race condition on SaveChangesAsync is absorbed, not thrown ───

    [Fact]
    public async Task Handle_UniqueViolationOnSaveChanges_DoesNotThrow_ReturnsGracefully()
    {
        // Previously, SaveChangesAsync() was called *outside* the try/catch that handles
        // unique-constraint violations — the in-memory duplicate check
        // (GetByEmployeeAndDateAsync returning null) passes, so ProcessCheckInAsync adds
        // the record to the tracker, but the *second* SaveChangesAsync call (the real DB
        // write) is what a genuine race would fail on. This simulates exactly that: no
        // existing record found (so no AlreadyCheckedInException), but SaveChangesAsync
        // itself throws a 23505 unique-violation, as Postgres would for two near-
        // simultaneous check-ins for the same employee+day.
        var (handler, _, employeeRepo, attendanceRepo, shiftRepo, uow) = BuildHandler();
        var employee = MakeEmployee();
        var shift    = MakeShift();

        employeeRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        shiftRepo.Setup(r => r.GetByIdAsync(employee.DefaultShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift?)shift);
        attendanceRepo.Setup(r => r.GetByEmployeeAndDateAsync(
            It.IsAny<EmployeeId>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        attendanceRepo.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // First SaveChangesAsync call (archiving the event log, Step 2) must still
        // succeed; only the *second* call (Step 7, the real attendance write) races.
        var saveCount = 0;
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                saveCount++;
                if (saveCount == 2)
                    throw new InvalidOperationException(
                        "23505: duplicate key value violates unique constraint " +
                        "\"IX_attendance_records_employee_id_WorkDate\"");
                return 1;
            });

        var cmd = new ProcessHikvisionEventCommand(CheckInXml, null, DateTime.UtcNow);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().NotThrowAsync(
            "a genuine race on the unique constraint must be absorbed, matching the " +
            "documented \"always 200, device retries on non-200\" webhook contract");
    }

    // ─── Test 8: Oversized device-sourced fields are truncated, not rejected ──

    [Fact]
    public async Task Handle_OversizedEmployeeCode_TruncatesToColumnLimit_DoesNotThrow()
    {
        // HikvisionEventLog.EmployeeCode is capped at 20 chars (HikvisionEventLogConfiguration).
        // Before this fix, a device sending a longer employeeNoString (firmware quirk or
        // malformed/spoofed payload) would hit an uncaught Postgres 22001 "value too long"
        // from SaveChangesAsync, producing a 500 instead of the documented always-200
        // contract. This asserts the value actually persisted to the event log is
        // truncated to 20 chars rather than the handler throwing.
        var longCode = new string('X', 45); // well over the 20-char cap
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <EventNotificationAlert version="2.0">
              <macAddress>AA:BB:CC:DD:EE:FF</macAddress>
              <dateTime>2025-01-15T09:03:25+05:30</dateTime>
              <eventType>AccessControllerEvent</eventType>
              <AccessControllerEvent>
                <employeeNoString>{longCode}</employeeNoString>
                <name>John Doe</name>
                <currentVerifyMode>face</currentVerifyMode>
                <attendanceStatus>checkIn</attendanceStatus>
              </AccessControllerEvent>
            </EventNotificationAlert>
            """;

        var (handler, deviceRepo, employeeRepo, _, _, _) = BuildHandler();

        // Employee lookup by the *untruncated* code legitimately finds nobody — that's a
        // separate, already-handled path (Handle_UnknownEmployee_LogsError_NoException).
        // This test only cares that archiving the event log itself doesn't throw, and
        // that what gets archived is capped.
        employeeRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        HikvisionEventLog? captured = null;
        deviceRepo.Setup(r => r.AddEventLogAsync(It.IsAny<HikvisionEventLog>(), It.IsAny<CancellationToken>()))
            .Callback<HikvisionEventLog, CancellationToken>((log, _) => captured = log)
            .Returns(Task.CompletedTask);

        var cmd = new ProcessHikvisionEventCommand(xml, null, DateTime.UtcNow);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().NotThrowAsync();

        captured.Should().NotBeNull();
        captured!.EmployeeCode.Length.Should().Be(20, "must be truncated to the column's HasMaxLength(20)");
        captured.EmployeeCode.Should().Be(longCode[..20]);
    }

    // ─── Test 9: xmin concurrency conflict on checkOut is absorbed, not thrown ─

    [Fact]
    public async Task Handle_ConcurrencyConflictOnCheckOut_DoesNotThrow_ReturnsGracefully()
    {
        // Only the checkIn unique-violation race (Test 7) was previously caught here.
        // checkOut/breakIn/breakOut all load-then-Update an existing tracked record —
        // if another writer (the webhook and polling fallback delivering the same
        // event, or two near-simultaneous device events) already bumped its xmin
        // since this handler loaded it, SaveChangesAsync throws
        // DbUpdateConcurrencyException, which UnitOfWork wraps into ConcurrencyException.
        // This simulates exactly that on the real attendance write (the second
        // SaveChangesAsync call — the first, archiving the event log, must still
        // succeed) and asserts the handler absorbs it per the documented always-200
        // webhook contract, instead of letting it propagate as an unhandled 500.
        var checkOutXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <EventNotificationAlert version="2.0">
              <macAddress>AA:BB:CC:DD:EE:FF</macAddress>
              <dateTime>2025-01-15T18:05:00+05:30</dateTime>
              <eventType>AccessControllerEvent</eventType>
              <AccessControllerEvent>
                <employeeNoString>EMP-001</employeeNoString>
                <name>John Doe</name>
                <currentVerifyMode>face</currentVerifyMode>
                <attendanceStatus>checkOut</attendanceStatus>
              </AccessControllerEvent>
            </EventNotificationAlert>
            """;

        var (handler, _, employeeRepo, attendanceRepo, shiftRepo, uow) = BuildHandler();
        var employee = MakeEmployee();
        var shift    = MakeShift();

        var openRecord = AttendanceRecord.CheckIn(
            employeeId:     employee.Id,
            shift:          shift,
            workDate:       DateOnly.FromDateTime(DateTime.UtcNow),
            checkInTimeUtc: DateTime.UtcNow.AddHours(-9),
            source:         PunchSource.Hikvision);

        employeeRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        shiftRepo.Setup(r => r.GetByIdAsync(employee.DefaultShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        attendanceRepo.Setup(r => r.GetByEmployeeAndDateAsync(
            It.IsAny<EmployeeId>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(openRecord);

        var saveCount = 0;
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                saveCount++;
                if (saveCount == 2)
                    throw new ConcurrencyException(
                        "A concurrency conflict occurred. The record was modified by another process.");
                return 1;
            });

        var cmd = new ProcessHikvisionEventCommand(checkOutXml, null, DateTime.UtcNow);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().NotThrowAsync(
            "an xmin conflict on checkOut must be absorbed, matching the documented " +
            "\"always 200, device retries on non-200\" webhook contract");
    }

    // ─── Test 10: breakIn event uses the device's event time, not server-received time ─

    [Fact]
    public async Task Handle_BreakInEvent_UsesDeviceEventTime_NotServerReceivedTime()
    {
        // StartBreak used to hardcode DateTime.UtcNow internally regardless of the
        // parsed device event time. Most damaging via the ISAPI polling fallback,
        // which can process a backlog of events (e.g. after a webhook outage) in one
        // pass — a breakIn from hours ago would still get stamped with "now",
        // silently corrupting break duration. dateTime here is deliberately far from
        // "now" so the assertion can't pass by coincidence.
        var breakInXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <EventNotificationAlert version="2.0">
              <macAddress>AA:BB:CC:DD:EE:FF</macAddress>
              <dateTime>2020-06-01T13:00:00+05:30</dateTime>
              <eventType>AccessControllerEvent</eventType>
              <AccessControllerEvent>
                <employeeNoString>EMP-001</employeeNoString>
                <name>John Doe</name>
                <currentVerifyMode>face</currentVerifyMode>
                <attendanceStatus>breakIn</attendanceStatus>
              </AccessControllerEvent>
            </EventNotificationAlert>
            """;
        var expectedEventTimeUtc = new DateTime(2020, 6, 1, 7, 30, 0, DateTimeKind.Utc); // 13:00 IST - 5:30

        var (handler, _, employeeRepo, attendanceRepo, shiftRepo, _) = BuildHandler();
        var employee = MakeEmployee();
        var shift    = MakeShift();

        var openRecord = AttendanceRecord.CheckIn(
            employeeId:     employee.Id,
            shift:          shift,
            workDate:       DateOnly.FromDateTime(expectedEventTimeUtc),
            checkInTimeUtc: expectedEventTimeUtc.AddHours(-4),
            source:         PunchSource.Hikvision);

        employeeRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        shiftRepo.Setup(r => r.GetByIdAsync(employee.DefaultShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        attendanceRepo.Setup(r => r.GetByEmployeeAndDateAsync(
            It.IsAny<EmployeeId>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(openRecord);

        var cmd = new ProcessHikvisionEventCommand(breakInXml, null, DateTime.UtcNow);
        await handler.Handle(cmd, CancellationToken.None);

        openRecord.Breaks.Should().ContainSingle();
        openRecord.Breaks.Single().StartTime.Should().Be(expectedEventTimeUtc);
        openRecord.Breaks.Single().StartTime.Should().NotBeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(1));
    }

    // ─── Test 11: Notifier fires after commit, and a throwing subscriber doesn't
    //              prevent the event from being processed ──────────────────────

    [Fact]
    public async Task Handle_CheckInEvent_NotifiesAfterSaveChanges_AndSurvivesThrowingSubscriber()
    {
        // SignalAttendanceChanged() used to fire *before* the SaveChangesAsync call
        // that actually persists the attendance write — an admin dashboard reacting
        // to the ping and re-querying on a separate DbContext/connection would hit
        // the not-yet-committed row. It's also a plain multicast Action, so a
        // throwing subscriber used to propagate back through this call *before*
        // SaveChangesAsync even ran, losing an otherwise-valid check-in entirely.
        // This asserts both: the real attendance SaveChangesAsync call happens
        // before the notify, and a subscriber that throws doesn't stop the record
        // from being created/returned.
        var (handler, _, employeeRepo, attendanceRepo, shiftRepo, uow) = BuildHandler();
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

        var callOrder = new List<string>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { callOrder.Add("SaveChanges"); return 1; });

        var notifier = new Mock<IAttendanceNotifier>();
        notifier.Setup(n => n.SignalAttendanceChanged())
            .Callback(() =>
            {
                callOrder.Add("Notify");
                throw new InvalidOperationException("simulated disposed Blazor circuit");
            });

        var handlerWithThrowingNotifier = new ProcessHikvisionEventHandler(
            Mock.Of<IHikvisionDeviceRepository>(d =>
                d.AddEventLogAsync(It.IsAny<HikvisionEventLog>(), It.IsAny<CancellationToken>()) == Task.CompletedTask),
            employeeRepo.Object,
            attendanceRepo.Object,
            shiftRepo.Object,
            uow.Object,
            OptionsHelper.Create(new HikvisionOptions { FaceCaptureBasePath = Path.GetTempPath() }),
            notifier.Object,
            Mock.Of<ILogger<ProcessHikvisionEventHandler>>());

        var cmd = new ProcessHikvisionEventCommand(CheckInXml, null, DateTime.UtcNow);

        // A throwing subscriber propagating out of Handle would fail this test with
        // that exception directly — no try/catch needed here to prove "doesn't throw".
        var result = await handlerWithThrowingNotifier.Handle(cmd, CancellationToken.None);

        result.AttendanceRecordId.Should().NotBeNull(
            "the attendance record must still be created and returned even though the " +
            "post-save notification callback threw");

        // "SaveChanges" appears twice (Step 2 archive, Step 7 attendance write) — the
        // notify call must come after the LAST one, not before either.
        callOrder.Should().Contain("Notify");
        callOrder.IndexOf("Notify").Should().Be(
            callOrder.Count - 1, "notify must be the final call, after every SaveChangesAsync");
    }
}

