# CLAUDE.md — AttendTrack Enterprise
# Master Agent Prompt for Claude Code (VS Code)
# Version: 2.0.0 | Stack: .NET 8 · Blazor Server · PostgreSQL · Redis · Docker
# Kiosk: Hikvision DS-K1T320MFWX (Face + Fingerprint + Card + PIN)
# Author: MK-AIFy / MAS Data Center | Chennai, Tamil Nadu, India
# Revision: v2.0.0 — 18 gaps resolved + Hikvision ISAPI integration

---

## AGENT IDENTITY & MISSION

You are a **Silicon Valley-grade Principal Full Stack .NET Architect** and
**Enterprise Attendance Tracking Domain Expert** embedded in VS Code via Claude Code.
Your mission: build, maintain, evolve **AttendTrack Enterprise** — a production-grade,
zero-licensing-cost, self-hosted attendance system integrated with Hikvision DS-K1T320MFWX.

Every line of code must meet:
- **Security-first**: zero trust, server-side validation always
- **Clean Architecture**: domain purity, dependency inversion
- **Zero external cost**: no paid SaaS, no cloud, self-hosted only
- **DPDP Act 2023**: all biometric data on Indian servers, auditable, deletable
- **Biometric-ready**: Hikvision ISAPI event push as PRIMARY attendance source
- **Concurrency-safe**: optimistic concurrency + unique constraints
- **Resilient**: graceful degradation when Redis, network, or device unreachable

---

## HIKVISION DS-K1T320MFWX — DEVICE SPECIFICATION (AS DEPLOYED)

```
Model           : DS-K1T320MFWX
Device Name     : Access Controller
Firmware        : V3.5.2 build 240701
Encoding        : V1.7.0.674699 build 2024-03-14 build 240314
Web Version     : V5.1.27_R0401 build 231226
Serial No       : DS-K1T320MFWX20240701V030502ENFS3728396
Protocol        : ISAPI (Hikvision HTTP REST API v2.0)

Authentication Modes supported:
  1. Face Recognition     — 2MP IR camera + illuminated ring, up to 3,000 faces
  2. Fingerprint          — Optical scanner, up to 3,000 templates
  3. RFID Card            — Mifare 13.56MHz + EM 125KHz dual-frequency
  4. PIN (numeric keypad) — 6-digit code entry
  5. Face + Fingerprint   — Dual-factor biometric (RECOMMENDED for high security)
  6. Face + Card          — Biometric + contactless
  7. Fingerprint + Card   — Multi-factor backup mode

Hardware:
  Display   : 2.4-inch color LCD (shows face preview + auth prompt)
  IR Ring   : White illuminated ring for face recognition camera
  Keypad    : 16-key — numeric 0-9 + ESC, OK/MENU, UP/DOWN/LEFT/RIGHT arrow
  Network   : RJ45 100Mbps Ethernet + Wi-Fi 802.11 b/g/n
  Storage   : Local event log up to 100,000 records
  Power     : 12V DC or PoE IEEE 802.3af
  OS        : Embedded Linux
```

---

## INTEGRATION ARCHITECTURE (3-LAYER)

```
LAYER 1 — PRIMARY: ISAPI Event Push (Webhook)
  Hikvision Device → POST /api/hikvision/events → AttendTrack Server
  Trigger: Biometric match (face/FP/card/PIN) in real-time
  Payload: XML multipart + optional JPEG face capture
  Latency: < 500ms | Reliability: 99.9% when network is stable

LAYER 2 — FALLBACK: ISAPI Polling (Background Job)
  AttendTrack → GET /ISAPI/AccessControl/AcsEvent → Hikvision Device
  Runs every 2 minutes IF webhook gap detected (> 5 minutes silent)
  Syncs missed events during network blips or device restarts

LAYER 3 — ADMIN FALLBACK: Blazor PIN Kiosk
  Used when: Hikvision device offline, employee not yet enrolled
  Auth: EmployeeCode + 6-digit PIN via browser on admin office PC
  IP-locked: accessible only from whitelisted admin office IPs
```

### ISAPI Event Push Data Flow
```
Employee presents biometric at DS-K1T320MFWX
  → Device authenticates (face/FP/card/PIN)
  → Generates EventNotificationAlert XML payload
  → HTTP POST to https://attendtrack.local/api/hikvision/events
  → HikvisionWebhookAuthMiddleware validates TCP IP + Basic auth
  → HikvisionEventController receives multipart (XML + JPEG)
  → ProcessHikvisionEventCommand dispatched via MediatR
  → Handler: archives to HikvisionEventLog (dedup safety)
  → Handler: resolves employeeNoString → Employee record
  → Handler: creates/updates AttendanceRecord (PunchSource.Hikvision)
  → SignalR broadcast → Admin Dashboard live update
  → Returns HTTP 200 to device (device retries on non-200)
```

### attendanceStatus Values (DS-K1T320MFWX)
```
checkIn   → Employee check-in  → AttendTrack: CheckInCommand
checkOut  → Employee check-out → AttendTrack: CheckOutCommand
breakIn   → Break start        → AttendTrack: StartBreakCommand
breakOut  → Break end          → AttendTrack: EndBreakCommand
```

### currentVerifyMode Values
```
face          → Face recognition only
fp            → Fingerprint only
card          → RFID/NFC card only
pin           → Keypad PIN only
faceAndFp     → Face + Fingerprint (most secure — RECOMMENDED)
faceAndCard   → Face + Card
fpAndCard     → Fingerprint + Card
```

### ISAPI Event XML Payload Example (DS-K1T320MFWX)
```xml
<!-- Device sends multipart/form-data; boundary separates XML + JPEG -->
<?xml version="1.0" encoding="UTF-8"?>
<EventNotificationAlert version="2.0">
  <ipAddress>192.168.1.50</ipAddress>
  <macAddress>AA:BB:CC:DD:EE:FF</macAddress>
  <channelID>1</channelID>
  <dateTime>2025-01-15T09:03:25+05:30</dateTime>  <!-- Device local IST time -->
  <eventType>AccessControllerEvent</eventType>
  <AccessControllerEvent>
    <deviceName>Access Controller</deviceName>
    <majorEventType>5</majorEventType>
    <subEventType>75</subEventType>
    <cardNo></cardNo>
    <employeeNoString>EMP-001</employeeNoString>  <!-- Employee.EmployeeCode -->
    <name>John Doe</name>
    <currentVerifyMode>faceAndFp</currentVerifyMode>
    <attendanceStatus>checkIn</attendanceStatus>
    <serialNo>33</serialNo>
    <userType>normal</userType>
    <time>1736918605</time>  <!-- Unix epoch UTC (fallback) -->
    <mask>noMask</mask>
  </AccessControllerEvent>
</EventNotificationAlert>
```

---

## TECHNOLOGY STACK (AUTHORITATIVE)

| Component | Technology | Version |
|---|---|---|
| Web Framework | ASP.NET Core | 8.0 LTS |
| Frontend | Blazor Server | 8.0 |
| ORM | Entity Framework Core | 8.0 |
| CQRS Bus | MediatR | 12.x |
| Validation | FluentValidation | 11.x |
| Mapping | AutoMapper | 13.x |
| Auth | ASP.NET Core Identity | 8.0 |
| Real-time | SignalR | 8.0 |
| Background Jobs | .NET BackgroundService | 8.0 |
| Logging | Serilog | 4.x |
| Testing | xUnit + Moq + FluentAssertions | Latest |
| HTTP Client | Refit | 7.x (Hikvision ISAPI) |
| PDF Export | QuestPDF Community (free) | Latest |
| XML Parsing | System.Xml.Linq | Built-in (Hikvision XML) |
| Health Checks | AspNetCore.Diagnostics.HealthChecks | 8.x |
| Rate Limiting | ASP.NET Core Rate Limiting | 8.0 (built-in) |
| Biometric Device | Hikvision DS-K1T320MFWX | ISAPI V2.0 |
| Primary DB | PostgreSQL 16 | Docker |
| Cache | Redis 7 | Docker |
| Reverse Proxy | Nginx Alpine | Latest |
| SSL | Let's Encrypt Certbot | Free |

---

## SOLUTION STRUCTURE

```
AttendTrack/
├── src/
│   ├── AttendTrack.Domain/
│   │   ├── Entities/
│   │   │   ├── Employee.cs
│   │   │   ├── AttendanceRecord.cs
│   │   │   ├── BreakRecord.cs
│   │   │   ├── HourlySlot.cs              ★ [Gap 4]
│   │   │   ├── Shift.cs
│   │   │   ├── Department.cs
│   │   │   ├── HikvisionDevice.cs         ★ [Hikvision] Device registry
│   │   │   ├── HikvisionEventLog.cs       ★ [Hikvision] Raw event archive
│   │   │   └── AuditLog.cs                ★ [Gap 6] DPDP
│   │   ├── ValueObjects/
│   │   │   ├── EmployeeId.cs
│   │   │   ├── WorkDuration.cs
│   │   │   ├── TimeSlot.cs
│   │   │   └── PinHash.cs
│   │   ├── Enums/
│   │   │   ├── AttendanceStatus.cs    # Present, Absent, MissedCheckout
│   │   │   ├── PunchSource.cs         ★ Hikvision, BlazorKiosk, Manual
│   │   │   ├── AttendanceAction.cs    ★ CheckIn, CheckOut, BreakIn, BreakOut
│   │   │   ├── ShiftViolationType.cs  ★ [Gap 8]
│   │   │   └── UserRole.cs
│   │   ├── Events/
│   │   │   ├── EmployeeCheckedInEvent.cs
│   │   │   ├── EmployeeCheckedOutEvent.cs
│   │   │   ├── OvertimeThresholdReachedEvent.cs
│   │   │   ├── MissedPunchDetectedEvent.cs    ★ [Gap 1]
│   │   │   ├── ShiftViolationDetectedEvent.cs ★ [Gap 8]
│   │   │   └── HikvisionEventReceivedEvent.cs ★ [Hikvision]
│   │   ├── Exceptions/
│   │   │   ├── DomainException.cs
│   │   │   ├── AlreadyCheckedInException.cs
│   │   │   ├── AlreadyCheckedOutException.cs
│   │   │   ├── ConcurrentCheckInException.cs  ★ [Gap 2]
│   │   │   └── UnauthorizedKioskException.cs
│   │   └── Interfaces/
│   │       ├── Repositories/
│   │       │   ├── IEmployeeRepository.cs
│   │       │   ├── IAttendanceRepository.cs
│   │       │   ├── IHourlySlotRepository.cs
│   │       │   ├── IShiftRepository.cs
│   │       │   ├── IAuditRepository.cs
│   │       │   └── IHikvisionDeviceRepository.cs ★ [Hikvision]
│   │       ├── Services/
│   │       │   ├── IDataSubjectService.cs         ★ [Gap 6]
│   │       │   └── IHikvisionIsapiService.cs      ★ [Hikvision]
│   │       └── IUnitOfWork.cs
│   │
│   ├── AttendTrack.Application/
│   │   ├── Commands/
│   │   │   ├── Attendance/
│   │   │   │   ├── CheckInCommand.cs
│   │   │   │   ├── CheckOutCommand.cs
│   │   │   │   ├── StartBreakCommand.cs
│   │   │   │   ├── EndBreakCommand.cs
│   │   │   │   └── AutoCheckOutCommand.cs          ★ [Gap 1]
│   │   │   ├── Hikvision/
│   │   │   │   ├── ProcessHikvisionEventCommand.cs ★ PRIMARY entry point
│   │   │   │   ├── EnrollEmployeeToDeviceCommand.cs
│   │   │   │   ├── SyncDeviceEventsCommand.cs
│   │   │   │   └── RegisterHikvisionDeviceCommand.cs
│   │   │   ├── Employee/
│   │   │   │   ├── CreateEmployeeCommand.cs
│   │   │   │   ├── UpdateEmployeeCommand.cs
│   │   │   │   ├── DeactivateEmployeeCommand.cs
│   │   │   │   └── ExportEmployeeDataCommand.cs     ★ [Gap 6] DPDP
│   │   │   └── Shift/
│   │   │       ├── AssignShiftCommand.cs
│   │   │       └── CreateShiftCommand.cs
│   │   ├── Queries/
│   │   │   ├── Attendance/
│   │   │   │   ├── GetDailyAttendanceQuery.cs
│   │   │   │   ├── GetEmployeeAttendanceHistoryQuery.cs
│   │   │   │   ├── GetHourlyBreakdownQuery.cs      ★ [Gap 4]
│   │   │   │   └── GetLiveAttendanceQuery.cs
│   │   │   └── Reports/
│   │   │       ├── GetWeeklyReportQuery.cs
│   │   │       ├── GetMonthlyReportQuery.cs
│   │   │       ├── GetOvertimeReportQuery.cs
│   │   │       └── GetShiftViolationReportQuery.cs  ★ [Gap 8]
│   │   ├── Behaviours/
│   │   │   ├── ValidationBehaviour.cs
│   │   │   ├── LoggingBehaviour.cs
│   │   │   ├── AuditBehaviour.cs           ★ [Gap 6]
│   │   │   └── ConcurrencyBehaviour.cs     ★ [Gap 2]
│   │   ├── DTOs/
│   │   │   ├── AttendanceDto.cs
│   │   │   ├── HourlyBreakdownDto.cs
│   │   │   ├── DailyReportDto.cs
│   │   │   ├── EmployeeDto.cs
│   │   │   ├── ShiftViolationDto.cs        ★ [Gap 8]
│   │   │   └── HikvisionEventDto.cs        ★ [Hikvision]
│   │   └── Mappings/MappingProfile.cs
│   │
│   ├── AttendTrack.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AttendTrackDbContext.cs
│   │   │   ├── UnitOfWork.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── EmployeeConfiguration.cs
│   │   │   │   ├── AttendanceRecordConfiguration.cs  # UNIQUE + xmin [Gap 2]
│   │   │   │   ├── HourlySlotConfiguration.cs        ★ [Gap 4]
│   │   │   │   ├── AuditLogConfiguration.cs           ★ [Gap 6]
│   │   │   │   ├── HikvisionDeviceConfiguration.cs   ★
│   │   │   │   └── HikvisionEventLogConfiguration.cs ★
│   │   │   ├── Repositories/
│   │   │   │   ├── EmployeeRepository.cs
│   │   │   │   ├── AttendanceRepository.cs
│   │   │   │   ├── HourlySlotRepository.cs
│   │   │   │   ├── ShiftRepository.cs
│   │   │   │   └── HikvisionDeviceRepository.cs
│   │   │   └── Migrations/
│   │   ├── Services/
│   │   │   ├── HourlyTrackerService.cs       ★ [Gap 4] IHostedService
│   │   │   ├── MissedPunchDetectorService.cs ★ [Gap 1] IHostedService
│   │   │   ├── DataRetentionPurgeService.cs  ★ [Gap 6]
│   │   │   ├── HikvisionPollingService.cs    ★ Fallback poll
│   │   │   ├── HikvisionIsapiService.cs      ★ ISAPI HTTP client
│   │   │   ├── DistributedCacheWrapper.cs    ★ [Gap 7]
│   │   │   ├── DataSubjectService.cs         ★ [Gap 6]
│   │   │   └── ReportExportService.cs
│   │   ├── Security/
│   │   │   ├── JwtTokenService.cs
│   │   │   ├── PinHasher.cs
│   │   │   ├── TcpIpWhitelistMiddleware.cs      ★ [Gap 3]
│   │   │   ├── HikvisionWebhookAuthMiddleware.cs ★ HMAC validation
│   │   │   └── IstTimeHelper.cs                 ★ [Gap 5]
│   │   ├── HealthChecks/                        ★ [Gap 10]
│   │   │   ├── PostgresHealthCheck.cs
│   │   │   ├── RedisHealthCheck.cs
│   │   │   ├── DiskSpaceHealthCheck.cs
│   │   │   └── HikvisionDeviceHealthCheck.cs    ★
│   │   └── DependencyInjection.cs
│   │
│   └── AttendTrack.Web/
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── AttendanceController.cs
│       │   ├── KioskController.cs              # Blazor fallback
│       │   ├── HikvisionEventController.cs     ★ PRIMARY webhook receiver
│       │   ├── HikvisionAdminController.cs     ★ Device + enrollment mgmt
│       │   ├── ReportsController.cs
│       │   ├── DataSubjectController.cs        ★ [Gap 6]
│       │   └── HealthController.cs             ★ [Gap 10]
│       ├── Hubs/AttendanceHub.cs
│       ├── Pages/
│       │   ├── Kiosk/KioskTerminal.razor
│       │   ├── Admin/
│       │   │   ├── Dashboard.razor
│       │   │   ├── LiveAttendance.razor
│       │   │   ├── Employees.razor
│       │   │   ├── Shifts.razor
│       │   │   ├── Reports.razor
│       │   │   ├── HourlyHeatMap.razor        ★ [Gap 4]
│       │   │   ├── ShiftViolations.razor      ★ [Gap 8]
│       │   │   └── HikvisionDevices.razor     ★ Enrollment + monitoring
│       │   └── Employee/
│       │       ├── MyAttendance.razor
│       │       ├── MyTimesheets.razor
│       │       └── MyDataExport.razor          ★ [Gap 6] DPDP
│       ├── Middleware/
│       │   ├── GlobalExceptionMiddleware.cs
│       │   ├── TcpIpWhitelistMiddleware.cs
│       │   ├── RequestAuditMiddleware.cs
│       │   └── SecurityHeadersMiddleware.cs    ★ [Gap 13]
│       └── Program.cs
│
├── tests/
│   ├── AttendTrack.Domain.Tests/
│   ├── AttendTrack.Application.Tests/
│   │   └── HikvisionEventProcessingTests.cs
│   └── AttendTrack.Integration.Tests/
│       └── HikvisionWebhookTests.cs
│
├── docker/
│   ├── docker-compose.prod.yml
│   ├── docker-compose.dev.yml
│   └── nginx/nginx.conf
│
├── scripts/
│   ├── backup.sh             ★ [Gap 9]
│   ├── restore.sh
│   ├── healthcheck.sh        ★ [Gap 10]
│   └── hikvision-setup.md   ★ Device config guide
│
├── AttendTrack.sln
├── global.json
└── CLAUDE.md
```

---

## DOMAIN MODEL

### Employee (Aggregate Root)
```csharp
public sealed class Employee : AggregateRoot<EmployeeId>
{
    public string EmployeeCode { get; private set; }     // Maps to Hikvision employeeNoString
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public PinHash KioskPin { get; private set; }        // 6-digit PIN (bcrypt) — fallback
    public string? BadgeRfidCard { get; private set; }   // ★ Hikvision RFID card number
    public string? HikvisionUserId { get; private set; } // ★ User ID on device
    public bool IsBiometricEnrolled { get; private set; } // ★ Face/FP on device
    public string? FacePhotoPath { get; private set; }   // ★ Enrollment photo (local)
    public DepartmentId DepartmentId { get; private set; }
    public ShiftId DefaultShiftId { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }     // ★ [Gap 6] DPDP soft-delete
}
```

### AttendanceRecord (Aggregate)
```csharp
public sealed class AttendanceRecord : AggregateRoot<AttendanceRecordId>
{
    public EmployeeId EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }           // IST-derived, NOT UTC date
    public DateTime? CheckInTime { get; private set; }       // UTC TIMESTAMPTZ
    public DateTime? CheckOutTime { get; private set; }      // UTC TIMESTAMPTZ
    public AttendanceStatus Status { get; private set; }
    public PunchSource CheckInSource { get; private set; }   // ★ HIKVISION / BLAZOR_KIOSK
    public PunchSource? CheckOutSource { get; private set; } // ★
    public string? VerifyMode { get; private set; }          // ★ 'faceAndFp', 'card', 'pin'
    public string? FaceCaptureImagePath { get; private set; }// ★ JPEG from Hikvision event
    public string? HikvisionSerialNo { get; private set; }   // ★ Device serial for audit
    public string? DeviceId { get; private set; }
    public string? Notes { get; private set; }               // ★ [Gap 8] Violation notes
    public uint Version { get; private set; }                // ★ [Gap 2] xmin concurrency
    public TimeSpan BreakDuration { get; private set; }
    public IReadOnlyCollection<BreakRecord> Breaks { get; }
    public IReadOnlyCollection<HourlySlot> HourlySlots { get; } // ★ [Gap 4]

    // Domain Methods
    public static AttendanceRecord CheckIn(EmployeeId empId, Shift shift,
        PunchSource source, string? verifyMode, string? deviceSerial,
        string? faceCaptureImagePath);  // Detects late arrival → raises ShiftViolationEvent
    public void CheckOut(Shift shift, PunchSource source, string? verifyMode);
    public void ForceAutoCheckOut(DateTime autoTime);  // ★ [Gap 1] MISSED_CHECKOUT
    public void OverrideCheckInTime(DateTime deviceEventTimeUtc); // Device time authoritative
    public BreakRecord StartBreak(BreakType type, PunchSource source);
    public void EndBreak(BreakRecordId breakId, PunchSource source);
}
```

### ★ HikvisionDevice (Entity)
```csharp
public sealed class HikvisionDevice
{
    public Guid Id { get; private set; }
    public string DeviceName { get; private set; }        // 'Access Controller'
    public string Model { get; private set; }             // 'DS-K1T320MFWX'
    public string SerialNumber { get; private set; }      // Full serial from device
    public string IpAddress { get; private set; }         // 192.168.1.50
    public int Port { get; private set; }                 // 80
    public string AdminUsername { get; private set; }     // For ISAPI calls
    public string AdminPasswordHash { get; private set; } // BCrypt hashed
    public string Location { get; private set; }          // 'Admin Office - Main Entrance'
    public bool IsActive { get; private set; }
    public DateTime? LastEventReceivedAt { get; private set; } // For polling gap detection
    public DateTime? LastPollAt { get; private set; }
    public string FirmwareVersion { get; private set; }   // 'V3.5.2 build 240701'
    public int EnrolledEmployeeCount { get; private set; }
}
```

### ★ HikvisionEventLog (Entity — Idempotency + Audit)
```csharp
public sealed class HikvisionEventLog
{
    public Guid Id { get; private set; }
    public string DeviceSerialNumber { get; private set; }
    public string EmployeeCode { get; private set; }       // employeeNoString from device
    public string EmployeeName { get; private set; }
    public string AttendanceStatus { get; private set; }   // checkIn/checkOut/breakIn/breakOut
    public string VerifyMode { get; private set; }         // faceAndFp, card, etc.
    public string? CardNo { get; private set; }
    public DateTime DeviceLocalTime { get; private set; }  // Parsed IST time from device
    public DateTime ReceivedAtUtc { get; private set; }    // Server received time
    public string? FaceCaptureStoragePath { get; private set; }  // Stored JPEG path
    public string RawPayload { get; private set; }         // Full XML (replay/audit)
    public bool IsProcessed { get; private set; }
    public string? ProcessingError { get; private set; }   // Null on success
    public Guid? AttendanceRecordId { get; private set; }  // Link to created record

    // UNIQUE(DeviceSerialNumber, DeviceLocalTime, EmployeeCode) — dedup key
}
```

---

## ★ HIKVISION WEBHOOK CONTROLLER

```csharp
[ApiController, Route("api/hikvision")]
public sealed class HikvisionEventController : ControllerBase
{
    [HttpPost("events")]
    [DisableRequestSizeLimit]      // Multipart with JPEG can be large
    [AllowAnonymous]               // IP + HMAC auth done in middleware
    public async Task<IActionResult> ReceiveEvent(CancellationToken ct)
    {
        string? xmlPayload = null;
        byte[]? faceCapture = null;

        if (Request.HasFormContentType)
        {
            // Multipart: XML event + optional JPEG face capture
            foreach (var formFile in Request.Form.Files)
            {
                if (formFile.ContentType == "application/xml")
                {
                    using var reader = new StreamReader(formFile.OpenReadStream());
                    xmlPayload = await reader.ReadToEndAsync(ct);
                }
                else if (formFile.ContentType == "image/jpeg")
                {
                    using var ms = new MemoryStream();
                    await formFile.CopyToAsync(ms, ct);
                    faceCapture = ms.ToArray();
                }
            }
        }
        else
        {
            // Some firmware sends raw XML body (not multipart)
            using var reader = new StreamReader(Request.Body);
            xmlPayload = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(xmlPayload))
            return BadRequest(new { error = "Empty event payload" });

        var result = await _mediator.Send(new ProcessHikvisionEventCommand(
            RawXml: xmlPayload,
            FaceCaptureBytes: faceCapture,
            ReceivedAtUtc: DateTime.UtcNow), ct);

        // ★ Always return 200 — device retries on any non-200
        return Ok(new { status = "ok", recordId = result.AttendanceRecordId });
    }
}
```

## ★ HIKVISION EVENT HANDLER

```csharp
public sealed class ProcessHikvisionEventHandler
    : IRequestHandler<ProcessHikvisionEventCommand, HikvisionProcessResult>
{
    public async Task<HikvisionProcessResult> Handle(
        ProcessHikvisionEventCommand cmd, CancellationToken ct)
    {
        // 1. Parse XML payload
        var parsed = HikvisionEventParser.Parse(cmd.RawXml);

        // 2. Archive raw event FIRST — idempotency + audit
        var eventLog = HikvisionEventLog.Create(parsed, cmd.ReceivedAtUtc, cmd.RawXml);
        await _deviceRepo.AddEventLogAsync(eventLog, ct);
        await _uow.SaveChangesAsync(ct); // Saves even if subsequent steps fail

        // 3. Resolve employee by EmployeeCode
        var employee = await _employeeRepo.GetByCodeAsync(parsed.EmployeeNoString, ct);
        if (employee == null)
        {
            eventLog.MarkFailed($"Employee not found: {parsed.EmployeeNoString}");
            await _uow.SaveChangesAsync(ct);
            return HikvisionProcessResult.Unrecognized(parsed.EmployeeNoString);
        }

        // 4. Save face capture JPEG if present
        string? facePath = null;
        if (cmd.FaceCaptureBytes?.Length > 0)
            facePath = await SaveFaceCaptureAsync(employee.EmployeeCode,
                parsed.DeviceLocalTime, cmd.FaceCaptureBytes, ct);

        // 5. Load shift, route to attendance command
        var shift = await _shiftRepo.GetByIdAsync(employee.DefaultShiftId, ct);
        AttendanceRecord? record = null;

        try
        {
            record = parsed.AttendanceStatus switch
            {
                "checkIn"  => await ProcessCheckIn(employee, shift, parsed, facePath, ct),
                "checkOut" => await ProcessCheckOut(employee, shift, parsed, ct),
                "breakIn"  => await ProcessBreakStart(employee, parsed, ct),
                "breakOut" => await ProcessBreakEnd(employee, parsed, ct),
                _ => null
            };
        }
        catch (AlreadyCheckedInException)
        {
            eventLog.MarkFailed("Duplicate check-in — idempotent ignore");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            eventLog.MarkFailed("Concurrent race — ignored");
        }

        if (record != null) eventLog.MarkProcessed(record.Id);
        await _uow.SaveChangesAsync(ct);
        return new HikvisionProcessResult(parsed.EmployeeNoString,
            parsed.AttendanceStatus, parsed.CurrentVerifyMode, record?.Id);
    }
}
```

## ★ HIKVISION XML PARSER

```csharp
public static class HikvisionEventParser
{
    public static HikvisionEventParsed Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var root = doc.Root;
        var ace = root?.Element(ns + "AccessControllerEvent")
               ?? root?.Element("AccessControllerEvent");

        if (ace == null)
            throw new InvalidOperationException("No AccessControllerEvent in payload");

        // Parse device timestamp: '2025-01-15T09:03:25+05:30'
        DateTime eventTimeUtc = DateTime.UtcNow;
        var dateTimeStr = root?.Element("dateTime")?.Value
                       ?? root?.Element(ns + "dateTime")?.Value;

        if (!string.IsNullOrWhiteSpace(dateTimeStr))
        {
            eventTimeUtc = DateTimeOffset.Parse(dateTimeStr).UtcDateTime;
        }
        else if (ace.Element("time")?.Value is string epochStr
            && long.TryParse(epochStr, out long epoch))
        {
            eventTimeUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        return new HikvisionEventParsed(
            DeviceSerial:        root?.Element("macAddress")?.Value ?? "UNKNOWN",
            EmployeeNoString:    ace.Element("employeeNoString")?.Value ?? "",
            EmployeeName:        ace.Element("name")?.Value ?? "",
            AttendanceStatus:    ace.Element("attendanceStatus")?.Value ?? "",
            CurrentVerifyMode:   ace.Element("currentVerifyMode")?.Value ?? "",
            CardNo:              ace.Element("cardNo")?.Value,
            EventTimeUtc:        eventTimeUtc,
            DeviceLocalTime:     IstTimeHelper.ToIstDateTime(eventTimeUtc)
        );
    }
}
```

## ★ HIKVISION ISAPI SERVICE (Employee Enrollment)

```csharp
public sealed class HikvisionIsapiService : IHikvisionIsapiService
{
    // Step 1: Create user on device
    // Step 2: Upload face photo via ISAPI FaceDataRecord
    // Biometric templates stored ONLY on device — never leave device (DPDP compliant)
    public async Task EnrollEmployeeAsync(HikvisionDevice device,
        Employee employee, byte[] facePhotoBytes, CancellationToken ct)
    {
        var userXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <UserInfoList version="2.0">
              <UserInfo>
                <employeeNo>{employee.EmployeeCode}</employeeNo>
                <name>{employee.FullName}</name>
                <userType>normal</userType>
                <Valid>
                  <enable>true</enable>
                  <beginTime>2000-01-01T00:00:00</beginTime>
                  <endTime>2037-12-31T23:59:59</endTime>
                </Valid>
                <doorRight>1</doorRight>
              </UserInfo>
            </UserInfoList>""";

        await _http.PutAsync(
            $"http://{device.IpAddress}/ISAPI/AccessControl/UserInfo/SetUp",
            new StringContent(userXml, Encoding.UTF8, "application/xml"), ct);

        // Upload face photo for face recognition enrollment
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(
            $"""<FaceDataRecord version="2.0"><employeeNo>{employee.EmployeeCode}</employeeNo></FaceDataRecord>""",
            Encoding.UTF8, "application/xml"), "FaceDataRecord");
        content.Add(new ByteArrayContent(facePhotoBytes), "face", "face.jpg");
        await _http.PutAsync(
            $"http://{device.IpAddress}/ISAPI/Intelligent/FDLib/FaceDataRecord",
            content, ct);
    }

    // Polling fallback: get recent events from device
    public async Task<IEnumerable<HikvisionEventParsed>> GetRecentEventsAsync(
        HikvisionDevice device, DateTime since, CancellationToken ct)
    {
        var searchXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <AcsEventCond version="2.0">
              <searchID>1</searchID>
              <maxResults>200</maxResults>
              <startTime>{since.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}</startTime>
              <endTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</endTime>
            </AcsEventCond>""";
        var response = await _http.PostAsync(
            $"http://{device.IpAddress}/ISAPI/AccessControl/AcsEvent?format=json",
            new StringContent(searchXml, Encoding.UTF8, "application/xml"), ct);
        var xml = await response.Content.ReadAsStringAsync(ct);
        return ParseEventListFromResponse(xml);
    }
}
```

---

## SECURITY ARCHITECTURE

### ★ Hikvision Webhook Authentication Middleware
```csharp
public sealed class HikvisionWebhookAuthMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/hikvision/events"))
        { await _next(context); return; }

        // 1. Validate TCP IP is a registered Hikvision device IP
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        var device = await GetDeviceByIpAsync(remoteIp);
        if (device == null)
        {
            _logger.LogWarning("Webhook from unregistered IP: {IP}", remoteIp);
            context.Response.StatusCode = 403; return;
        }

        // 2. Validate HTTP Basic auth (device sends admin credentials)
        if (!ValidateBasicAuth(context.Request.Headers["Authorization"], device))
        {
            context.Response.StatusCode = 401; return;
        }

        await _next(context);
    }
}
```

### ★ [Gap 3] TCP IP Whitelist (NEVER reads X-Forwarded-For)
```csharp
var remoteIp = context.Connection.RemoteIpAddress;
if (remoteIp.IsIPv4MappedToIPv6) remoteIp = remoteIp.MapToIPv4();
// NEVER: context.Request.Headers["X-Forwarded-For"]
```

### ★ [Gap 5] IstTimeHelper (Mandatory for all IST conversions)
```csharp
public static class IstTimeHelper
{
    private static readonly TimeZoneInfo Ist =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    public static string ToIst(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, Ist).ToString("dd MMM yyyy hh:mm tt");
    public static DateTime ToIstDateTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, Ist);
    public static DateTime ToUtc(DateTime ist) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ist, DateTimeKind.Unspecified), Ist);
    public static DateTime NowIst => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist);
    public static DateOnly TodayIst => DateOnly.FromDateTime(NowIst);
}
```

### ★ [Gap 7] DistributedCacheWrapper (Redis graceful degradation)
```csharp
public sealed class DistributedCacheWrapper : IDistributedCacheWrapper
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try { return DeserializeFromRedis(await _redis.GetAsync(key, ct)); }
        catch (Exception ex) when (ex is RedisConnectionException or TimeoutException)
        {
            _logger.LogWarning(ex, "Redis GET failed for {Key}, using IMemoryCache", key);
            return _memoryCache.TryGetValue(key, out T? val) ? val : default;
        }
    }
}
```

### Middleware Pipeline (ORDER IS CRITICAL)
```csharp
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();       // HSTS, CSP, X-Frame [Gap 13]
app.UseRateLimiter();                                // 10 req/min kiosk [Gap 11]
app.UseMiddleware<HikvisionWebhookAuthMiddleware>(); // IP + Basic auth [Hikvision]
app.UseMiddleware<TcpIpWhitelistMiddleware>();        // TCP IP only [Gap 3]
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestAuditMiddleware>();          // DPDP audit [Gap 6]
app.UseMiddleware<GlobalExceptionMiddleware>();
```

---

## DATABASE SCHEMA (POSTGRESQL)

```sql
-- ALL timestamps are TIMESTAMPTZ (UTC). Display in IST via IstTimeHelper.

employees (
    id UUID PK, employee_code VARCHAR(20) UNIQUE NOT NULL,
    full_name VARCHAR(200) NOT NULL, email VARCHAR(200) UNIQUE NOT NULL,
    kiosk_pin_hash VARCHAR(256) NOT NULL,
    badge_rfid_card VARCHAR(100),          -- ★ Hikvision RFID card
    hikvision_user_id VARCHAR(50),         -- ★ Device user ID
    is_biometric_enrolled BOOLEAN DEFAULT FALSE,
    face_photo_path VARCHAR(500),          -- ★ Enrollment photo path
    department_id UUID FK, default_shift_id UUID FK,
    role VARCHAR(50), is_active BOOLEAN DEFAULT TRUE,
    joined_at DATE NOT NULL,
    deleted_at TIMESTAMPTZ,                -- ★ [Gap 6] DPDP soft-delete
    created_at TIMESTAMPTZ DEFAULT NOW(), updated_at TIMESTAMPTZ DEFAULT NOW()
);

attendance_records (
    id UUID PK, employee_id UUID FK NOT NULL, work_date DATE NOT NULL,
    check_in_time TIMESTAMPTZ, check_out_time TIMESTAMPTZ,
    status VARCHAR(30) NOT NULL DEFAULT 'PRESENT',
    check_in_source VARCHAR(30) DEFAULT 'HIKVISION',   -- ★
    check_out_source VARCHAR(30),
    verify_mode VARCHAR(50),               -- ★ faceAndFp, card, pin, face, fp
    face_capture_image_path VARCHAR(500),  -- ★ JPEG path
    hikvision_serial_no VARCHAR(100),      -- ★ Device serial
    break_duration_minutes INTEGER DEFAULT 0,
    device_id VARCHAR(100), ip_address VARCHAR(45), notes TEXT,
    xmin xid,                              -- ★ [Gap 2] Optimistic concurrency
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(employee_id, work_date)         -- ★ [Gap 2] Prevents duplicate check-in
);

-- ★ Hikvision raw event archive (idempotency + replay + full audit trail)
hikvision_event_logs (
    id UUID PK,
    device_serial_no VARCHAR(100) NOT NULL,
    employee_code VARCHAR(20) NOT NULL,
    employee_name VARCHAR(200),
    attendance_status VARCHAR(30) NOT NULL,   -- checkIn/checkOut/breakIn/breakOut
    verify_mode VARCHAR(50),
    card_no VARCHAR(100),
    device_local_time TIMESTAMPTZ NOT NULL,   -- Parsed from device dateTime (IST)
    received_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    face_capture_storage_path VARCHAR(500),   -- Stored JPEG path
    raw_payload TEXT NOT NULL,               -- Full XML for replay
    is_processed BOOLEAN DEFAULT FALSE,
    processing_error TEXT,
    attendance_record_id UUID FK attendance_records(id),
    UNIQUE(device_serial_no, device_local_time, employee_code)  -- Dedup key
);
CREATE INDEX idx_hik_events_unprocessed ON hikvision_event_logs(is_processed)
    WHERE is_processed = FALSE;

-- ★ Hikvision device registry
hikvision_devices (
    id UUID PK,
    device_name VARCHAR(100) NOT NULL,
    model VARCHAR(100) DEFAULT 'DS-K1T320MFWX',
    serial_number VARCHAR(100) UNIQUE NOT NULL,
    ip_address VARCHAR(45) UNIQUE NOT NULL,
    port INTEGER DEFAULT 80,
    admin_username VARCHAR(100) NOT NULL,
    admin_password_hash VARCHAR(256) NOT NULL,
    location VARCHAR(200),
    is_active BOOLEAN DEFAULT TRUE,
    last_event_received_at TIMESTAMPTZ,       -- For polling gap detection
    last_poll_at TIMESTAMPTZ,
    firmware_version VARCHAR(100),            -- 'V3.5.2 build 240701'
    enrolled_employee_count INTEGER DEFAULT 0,
    registered_at TIMESTAMPTZ DEFAULT NOW()
);

hourly_slots (
    id UUID PK, attendance_id UUID FK ON DELETE CASCADE,
    employee_id UUID FK, work_date DATE NOT NULL,
    hour_slot SMALLINT NOT NULL CHECK (hour_slot BETWEEN 0 AND 23),
    minutes_worked SMALLINT NOT NULL DEFAULT 0,
    is_break BOOLEAN DEFAULT FALSE, is_overtime BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(), updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(attendance_id, hour_slot)
);

audit_logs (
    id UUID PK, actor_id UUID, actor_role VARCHAR(50),
    action VARCHAR(100) NOT NULL, entity_type VARCHAR(100), entity_id UUID,
    old_values JSONB, new_values JSONB, ip_address VARCHAR(45),
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## HIKVISION DEVICE SETUP GUIDE

### Step 1: Physical Installation
```
1. Mount DS-K1T320MFWX at eye level (1.4m) at admin office entrance
2. Connect via Ethernet (RJ45) — USE ETHERNET, NOT WI-FI
   Ethernet is mandatory for reliable real-time event push
3. Connect 12V DC power or PoE-enabled switch port
4. Device boots — LCD shows time and 'Authenticate via FP'
```

### Step 2: Network via Web Interface
```
Browser → http://192.168.1.64 (factory default IP)
Login: admin / (serial number as default password)
Navigate: Configuration > Network > TCP/IP
  Set: IP=192.168.1.50 (static), Subnet=255.255.255.0, GW=192.168.1.1
Save → device reboots → reconnect to http://192.168.1.50
```

### Step 3: Configure Event Push (Webhook to AttendTrack)
```
Navigate: Configuration > Network > Advanced > HTTP Listening

Protocol:           HTTP
Parameter Format:   XML
Listening Host IP:  192.168.1.10      (AttendTrack server IP)
Listening Host Port: 443
URL:                /api/hikvision/events
Authorization Type: Basic
Username:           hikadmin
Password:           [strong password — register in AttendTrack device DB]

Enable Event: AccessControllerEvent
  → checkIn, checkOut, breakIn, breakOut
Enable Send Picture: YES (sends JPEG face capture)
Interval: 0 (send immediately on event)
```

### Step 4: Attendance Mode Configuration
```
Navigate: Configuration > Time & Attendance > General Settings
  Attendance Rule: Enable
  Work Mode: Attendance Mode

Attendance Status Settings:
  Check In  → Status Key: 0 (or Auto-detect by system state)
  Check Out → Status Key: 1
  Break In  → Status Key: 2
  Break Out → Status Key: 3

Verify Mode (recommend for office): Face + Fingerprint
Grace period for verify failure: 3 attempts, then deny
```

### Step 5: Employee Enrollment via AttendTrack Admin UI
```
1. Admin → AttendTrack → Devices → DS-K1T320MFWX → Enroll Employees
2. Select employee → [Enroll on Device]
3. AttendTrack calls ISAPI: PUT /ISAPI/AccessControl/UserInfo/SetUp
4. Employee at device:
   a. Face: device captures 5 angles automatically
   b. Fingerprint: press finger 3 times on scanner (right index recommended)
5. RFID card (optional): tap card on device, linked to employee
6. AttendTrack updates: Employee.IsBiometricEnrolled = true

DPDP NOTE: Face templates + fingerprint templates stored ONLY on device.
They NEVER leave the device. Only JPEG face captures (not templates)
are sent with attendance events and stored by AttendTrack.
Face capture photos auto-deleted after 30 days (DPDP compliance).
```

### Step 6: Verify Integration
```
1. Employee scans at device → LCD shows 'Welcome, [Name]'
2. Device sends HTTP POST to /api/hikvision/events (< 500ms)
3. AttendTrack processes → creates AttendanceRecord
4. Admin dashboard updates in real-time (SignalR)
5. Test curl (from server):
   curl -X POST https://localhost/api/hikvision/events \
     -H 'Authorization: Basic hikadmin:password' \
     -H 'Content-Type: application/xml' \
     -d @tests/fixtures/hikvision_checkin_sample.xml
```

---

## ADMIN UI — HIKVISION DEVICE MANAGEMENT PAGE

```
HikvisionDevices.razor layout:

┌─────────────────────────────────────────────────────────────┐
│  Biometric Devices                      [+ Register Device] │
├─────────────────────────────────────────────────────────────┤
│  ● ONLINE  DS-K1T320MFWX — Admin Office  Last: 30 sec ago  │
│  IP: 192.168.1.50  FW: V3.5.2  Enrolled: 47  Today: 89 ev  │
│  [Sync Now]  [Enroll Employees]  [Event Log]  [Settings]   │
├─────────────────────────────────────────────────────────────┤
│  Enrollment Panel:                                          │
│  [Search employee...] → John Doe EMP-001 IT  ✅ Enrolled   │
│                          Jane Smith EMP-002  ❌ [Enroll]   │
├─────────────────────────────────────────────────────────────┤
│  Recent Events (live via SignalR):                          │
│  09:03 AM  EMP-001  John Doe  checkIn  faceAndFp  ✅       │
│  09:05 AM  EMP-002  Jane      checkIn  face        ✅       │
│  10:00 AM  UNKNOWN  [card]    checkIn  card        ⚠️ NoMatch│
└─────────────────────────────────────────────────────────────┘
```

---

## GAP RESOLUTION MASTER TABLE (ALL 18)

| # | Gap | Resolution | Location |
|---|-----|------------|----------|
| 1 | Missing Auto-Checkout Safety Net | MissedPunchDetectorService every 15 min | Infrastructure/Services/ |
| 2 | No Concurrency Protection | UNIQUE + UseXminAsConcurrencyToken | Persistence/Configurations/ |
| 3 | Kiosk IP Spoofing Risk | TcpIpWhitelistMiddleware (RemoteIpAddress) | Web/Middleware/ |
| 4 | No Hourly Granularity | HourlySlot + HourlyTrackerService | Domain/ + Infrastructure/ |
| 5 | Timezone Inconsistency | IstTimeHelper + TIMESTAMPTZ everywhere | Infrastructure/Security/ |
| 6 | No DPDP Compliance | AuditLog + DataSubjectService + soft-delete | Domain/ + Infrastructure/ |
| 7 | Redis Down = App Down | DistributedCacheWrapper + IMemoryCache fallback | Infrastructure/Services/ |
| 8 | No Shift Violation Detection | ShiftViolationDetectedEvent in domain | Domain/Events/ |
| 9 | No Backup Strategy | backup.sh + restore.sh daily/weekly | scripts/ |
| 10 | No Health Checks | ASP.NET HealthChecks + healthcheck.sh cron | Infrastructure/HealthChecks/ |
| 11 | No Rate Limiting | ASP.NET 8 fixed-window (10 req/min kiosk) | Program.cs |
| 12 | No Kiosk Heartbeat | /api/kiosk/heartbeat + KioskOfflineAlert | Web/Controllers/ |
| 13 | No Security Headers | SecurityHeadersMiddleware + Nginx | Web/Middleware/ |
| 14 | No Disaster Recovery | restore.sh with confirmation guard | scripts/ |
| 15 | No Data Retention Purge | DataRetentionPurgeService (7-year DPDP) | Infrastructure/Services/ |
| 16 | No Biometric Device | Hikvision ISAPI event push + polling | Infrastructure/Services/ |
| 17 | No Enrollment UI | HikvisionDevices.razor + ISAPI enrollment | Web/Pages/Admin/ |
| 18 | No Device Health | HikvisionDeviceHealthCheck + dashboard | Infrastructure/HealthChecks/ |

---

## IMPLEMENTATION PROMPTS FOR CLAUDE CODE (VS CODE)

**Model**: claude-sonnet-4-6 (or claude-sonnet-4-5)
**Setting**: Enable 'Include project files' so Claude reads CLAUDE.md on every prompt
**Usage**: Copy each prompt verbatim into Claude Code chat (Ctrl+Shift+P → Claude: Open Chat)

---

### PROMPT 1 — SCAFFOLD (Day 1)
```
You are implementing AttendTrack Enterprise as defined in CLAUDE.md.
Create the complete .NET 8 solution scaffold:

1. AttendTrack.sln with 4 .csproj projects:
   Domain (no dependencies), Application (→Domain),
   Infrastructure (→Application+Domain), Web (→Application+Infrastructure)

2. global.json pinning .NET 8.0

3. All NuGet packages from TECHNOLOGY STACK in CLAUDE.md.
   Infrastructure specifically needs:
   - Npgsql.EntityFrameworkCore.PostgreSQL
   - StackExchange.Redis
   - BCrypt.Net-Next (for PinHasher)
   - System.Xml.Linq (Hikvision XML parsing)
   - AspNetCore.HealthChecks.NpgSql
   - AspNetCore.HealthChecks.Redis
   - QuestPDF (Community license)

4. Complete directory structure per SOLUTION STRUCTURE in CLAUDE.md.
   Create every folder and empty placeholder .cs file.

5. .editorconfig enforcing: sealed classes, record DTOs, private set
6. .gitignore: exclude /backups, /face-captures, .env, *.user

Verify: dotnet build exits 0. No business logic yet.
```

---

### PROMPT 2 — DOMAIN LAYER (Day 1-2)
```
Implement the complete Domain layer for AttendTrack per CLAUDE.md DOMAIN MODEL.
Zero EF Core or infrastructure dependencies.

Entities (all sealed, private setters, mutations via domain methods only):
1. Employee: EmployeeCode, IsBiometricEnrolled, BadgeRfidCard,
   HikvisionUserId, FacePhotoPath, DeletedAt (DPDP), SoftDelete() method
2. AttendanceRecord: CheckInSource (PunchSource), VerifyMode, FaceCaptureImagePath,
   HikvisionSerialNo, Version (xmin), ForceAutoCheckOut(), OverrideCheckInTime()
   CheckIn() detects late arrival, raises ShiftViolationDetectedEvent
   CheckOut() detects early departure, raises ShiftViolationDetectedEvent
3. HourlySlot: HourSlotNumber (0-23), MinutesWorked (0-60), IsBreak, IsOvertime
4. HikvisionDevice: all fields from DOMAIN MODEL in CLAUDE.md
5. HikvisionEventLog: RawPayload, IsProcessed, ProcessingError, MarkProcessed(),
   MarkFailed(), UNIQUE key (DeviceSerialNumber, DeviceLocalTime, EmployeeCode)
6. AuditLog: OldValues (string JSONB), NewValues (string JSONB)
7. BreakRecord, Shift (with GracePeriodMinutes), Department

Enums: AttendanceStatus (Present, Absent, MissedCheckout, HalfDay)
       PunchSource (Hikvision, BlazorKiosk, Manual)
       AttendanceAction (CheckIn, CheckOut, BreakIn, BreakOut)
       ShiftViolationType (LateArrival, EarlyDeparture)
       UserRole (SuperAdmin, Admin, HRManager, Manager, Employee)

Domain Events: EmployeeCheckedInEvent, EmployeeCheckedOutEvent,
  OvertimeThresholdReachedEvent, MissedPunchDetectedEvent,
  ShiftViolationDetectedEvent, HikvisionEventReceivedEvent

Domain Exceptions: DomainException, AlreadyCheckedInException,
  AlreadyCheckedOutException, ConcurrentCheckInException,
  NotCheckedInException, UnauthorizedKioskException

Interfaces: IEmployeeRepository, IAttendanceRepository, IHourlySlotRepository,
  IShiftRepository, IAuditRepository, IHikvisionDeviceRepository,
  IDataSubjectService, IHikvisionIsapiService, IUnitOfWork

AggregateRoot<TId>: AddDomainEvent, ClearDomainEvents, list of events.
```

---

### PROMPT 3 — HIKVISION INTEGRATION (Day 2-3) ← CRITICAL PATH
```
Implement the complete Hikvision DS-K1T320MFWX integration per CLAUDE.md.
This is the PRIMARY attendance source. Every detail matters.

1. HikvisionEventParser (static class):
   - Parse XML EventNotificationAlert from DS-K1T320MFWX
   - Extract dateTime '2025-01-15T09:03:25+05:30' → UtcDateTime (DateTimeOffset.Parse)
   - Fallback: parse Unix epoch from <time> element
   - Extract: employeeNoString, name, attendanceStatus, currentVerifyMode, cardNo
   - Handle both xml-with-namespace and xml-without-namespace (firmware variations)
   - Returns HikvisionEventParsed record

2. ProcessHikvisionEventCommand + Handler:
   - Parses XML, archives to HikvisionEventLog FIRST (before any processing)
   - UNIQUE constraint on (device_serial_no, device_local_time, employee_code) = dedup
   - Resolves employee by employeeNoString → Employee.EmployeeCode
   - Saves JPEG face capture to /app/face-captures/{yyyyMMdd}/{code}_{HHmmss}.jpg
   - Routes: checkIn→CheckIn, checkOut→CheckOut, breakIn→StartBreak, breakOut→EndBreak
   - Uses DEVICE event time (not server received time) as AttendanceRecord timestamp
   - Handles AlreadyCheckedInException gracefully (marks event as duplicate, no error)
   - Handles DbUpdateException UniqueViolation (race condition, no error)
   - Returns HTTP 200 always (device retries on non-200)

3. HikvisionEventController per CLAUDE.md webhook controller section:
   - Handles multipart/form-data (XML + JPEG) AND raw XML body
   - POST /api/hikvision/events (AllowAnonymous — auth in middleware)
   - POST /api/hikvision/sync/{deviceId} (Admin only, manual sync)

4. HikvisionWebhookAuthMiddleware per CLAUDE.md security section:
   - Validates TCP RemoteIpAddress is a registered Hikvision device IP
   - Validates HTTP Basic auth against stored device password hash
   - Only applies to /api/hikvision/events path

5. HikvisionIsapiService per CLAUDE.md ISAPI service section:
   - EnrollEmployeeAsync: ISAPI UserInfo SetUp + FaceDataRecord upload
   - GetRecentEventsAsync: AcsEvent search (polling fallback)
   - PingDeviceAsync: GET /ISAPI/System/deviceInfo (health check)
   - Uses HttpClient with Basic auth (Digest for ISAPI calls)

6. HikvisionPollingService (IHostedService, runs every 2 minutes):
   - Only polls if LastEventReceivedAt > 5 minutes ago (webhook gap)
   - Deduplicates by EventLogExists before processing
   - Use IServiceScopeFactory for scoped dependencies

7. HikvisionDeviceHealthCheck:
   - Pings /ISAPI/System/deviceInfo for each active device
   - Returns HealthCheckResult.Healthy/Degraded/Unhealthy

8. Unit tests in HikvisionEventProcessingTests.cs:
   - ParseXml_WithDateTimeOffset_ReturnsCorrectUtc
   - ParseXml_WithUnixEpoch_ReturnsCorrectUtc
   - Handle_CheckInEvent_CreatesAttendanceRecord_WithPunchSourceHikvision
   - Handle_DuplicateEvent_IsIdempotent_NoSecondRecord
   - Handle_UnknownEmployee_LogsError_NoException
   - Handle_CheckOutEvent_UpdatesRecord
```

---

### PROMPT 4 — INFRASTRUCTURE LAYER (Day 3-4)
```
Implement the complete Infrastructure layer for AttendTrack per CLAUDE.md.

1. AttendTrackDbContext:
   - DbSets for all entities including HikvisionDevice, HikvisionEventLog
   - Global query filter: Employee WHERE DeletedAt IS NULL
   - EnableDynamicJson() for JSONB (AuditLog old_values/new_values)
   - SaveChangesAsync override: dispatches domain events after save via IMediator

2. Entity Configurations (IEntityTypeConfiguration<T>):
   - AttendanceRecordConfiguration:
     * HasIndex(e => new { e.EmployeeId, e.WorkDate }).IsUnique() [Gap 2]
     * UseXminAsConcurrencyToken() [Gap 2]
     * All DateTime props: HasColumnType("timestamptz")
     * HasMany(HourlySlots).OnDelete(Cascade)
   - HikvisionEventLogConfiguration:
     * HasIndex(DeviceSerialNo, DeviceLocalTime, EmployeeCode).IsUnique() — dedup
     * Partial index on IsProcessed WHERE false
   - AuditLogConfiguration: OldValues/NewValues as HasColumnType("jsonb")
   - EmployeeConfiguration: apply soft-delete query filter

3. Repositories (all async, CancellationToken, no leaking entities):
   - HikvisionDeviceRepository: GetActiveDevicesAsync, GetByIpAsync,
     EventLogExistsAsync(deviceSerial, eventTime, employeeCode),
     AddEventLogAsync, UpdateLastEventAsync, UpdateLastPollAsync

4. IstTimeHelper static class — exactly per CLAUDE.md [Gap 5]
5. TcpIpWhitelistMiddleware — RemoteIpAddress only [Gap 3]
6. DistributedCacheWrapper — Redis + IMemoryCache fallback [Gap 7]
7. PinHasher: BCrypt WorkFactor=12
8. HikvisionIsapiService: configure HttpClient with Digest auth handler,
   timeout 10 seconds (IsapiTimeoutSeconds from config)

9. Background Services (all use IServiceScopeFactory, catch all exceptions):
   - HourlyTrackerService: every 60s, upsert HourlySlots, broadcast SignalR
   - MissedPunchDetectorService: every 15 min, auto-checkout overdue records
   - DataRetentionPurgeService: daily 2 AM IST, hard-purge 7yr soft-deleted employees
   - HikvisionPollingService: every 2 min, fallback polling [Hikvision]

10. DependencyInjection.cs AddInfrastructure() method wires everything.
```

---

### PROMPT 5 — APPLICATION LAYER (Day 4-5)
```
Implement the Application layer for AttendTrack per CLAUDE.md.

MediatR Pipeline Behaviours (register in order):
1. ValidationBehaviour: FluentValidation, throws ValidationException on failure
2. LoggingBehaviour: Serilog with elapsed ms, command name
3. AuditBehaviour: Captures entity before/after as JSONB, writes AuditLog [Gap 6]
4. ConcurrencyBehaviour: Catches DbUpdateConcurrencyException → 409 [Gap 2]

Commands:
1. ProcessHikvisionEventCommand (done in Prompt 3)
2. CheckInCommand (Blazor kiosk fallback):
   - PunchSource = BlazorKiosk
   - Uses IstTimeHelper.TodayIst for WorkDate
   - Catches UniqueViolation → AlreadyCheckedInException
3. CheckOutCommand: PunchSource = BlazorKiosk
4. AutoCheckOutCommand: called by MissedPunchDetectorService
5. StartBreakCommand, EndBreakCommand
6. CreateEmployeeCommand: IsBiometricEnrolled=false initially
7. UpdateEmployeeCommand, DeactivateEmployeeCommand
8. EnrollEmployeeToDeviceCommand:
   - Calls IHikvisionIsapiService.EnrollEmployeeAsync
   - Updates Employee.IsBiometricEnrolled=true
9. SyncDeviceEventsCommand: triggers HikvisionPollingService for one device
10. ExportEmployeeDataCommand: DPDP JSON export [Gap 6]

Queries:
1. GetLiveAttendanceQuery: today's records with IST times and PunchSource
2. GetHourlyBreakdownQuery: HourlySlot data for heat-map [Gap 4]
3. GetDailyAttendanceQuery, GetEmployeeAttendanceHistoryQuery
4. GetWeeklyReportQuery, GetMonthlyReportQuery, GetOvertimeReportQuery
5. GetShiftViolationReportQuery [Gap 8]

All handlers: CancellationToken, ConfigureAwait(false).
```

---

### PROMPT 6 — BLAZOR ADMIN UI (Day 5-7)
```
Build all Blazor Server pages for AttendTrack per CLAUDE.md.

1. Dashboard.razor:
   - Summary cards: Present / Absent / Late / On Break
   - Biometric device status badge: ONLINE / OFFLINE (live)
   - Last 5 events feed (real-time SignalR)

2. LiveAttendance.razor:
   - Table: Name, CheckIn (IST), Hours, Status, Auth Method, Source icon
   - Source: biometric icon (face/FP) OR PIN keyboard icon
   - Real-time via SignalR

3. HikvisionDevices.razor (new):
   - Device list: ONLINE/OFFLINE, last event, enrolled count
   - Enrollment panel: search employees, [Enroll] → POST EnrollEmployeeToDeviceCommand
   - Recent event log table: time (IST), employee, status, verifyMode, JPEG thumbnail
   - [Sync Now] button → POST /api/hikvision/sync/{deviceId}

4. HourlyHeatMap.razor:
   - Grid: employees (rows) x hours 0-23 IST (columns)
   - Colors: green=working, red=absent, amber=break, blue=overtime
   - Tooltip: 'John 09:00-09:59: 58 min worked (faceAndFp)'

5. ShiftViolations.razor: violations table with CSV export

6. Employees.razor: CRUD + biometric enrollment badge (✅/❌) + [Enroll] button

7. KioskTerminal.razor (Blazor PIN Fallback):
   - Full-screen, IST clock, EmployeeCode + PIN inputs
   - Banner: 'Please use the Hikvision biometric terminal at the entrance'
   - 30-sec idle clear, 3-attempt lockout, SignalR live counter

All pages: [Authorize], IST times via IstTimeHelper, sealed code-behind.
```

---

### PROMPT 7 — REPORTS + DOCKER + DEPLOY (Day 7-9)
```
Implement reporting and deployment for AttendTrack.

1. ReportExportService (QuestPDF Community, CsvHelper):
   - Monthly PDF: per-employee days, hours, OT, late/early, auth method breakdown
     Include: '42 days via Face+FP, 3 days via PIN kiosk'
   - Weekly CSV: UTF-8 BOM (Excel compatible)
   - Device Event Log CSV: all Hikvision events for audit
   - All times in IST via IstTimeHelper

2. Docker:
   - Multi-stage Dockerfile: sdk:8.0 build → runtime:8.0-alpine
   - Run as non-root user 'appuser'
   - Volumes: face_captures, hikvision_events, app_logs, pg_backups
   - docker-compose.prod.yml per CLAUDE.md Docker section

3. Nginx nginx.conf (CRITICAL):
   - SSL termination (Let's Encrypt)
   - Strip all X-Forwarded-For from external clients [Gap 3]
   - Restrict /api/hikvision/events to device IPs only
   - WebSocket proxy for /hubs/* (SignalR)
   - Security headers: HSTS, CSP, X-Frame DENY, X-Content nosniff [Gap 13]

4. scripts/backup.sh [Gap 9]:
   - Daily pg_dump → /backups/daily/ (30-day rotation)
   - Weekly → /backups/weekly/ (12-week)
   - Backup face-captures directory (gzip tarball)

5. scripts/healthcheck.sh [Gap 10]:
   - GET /health → email admin on failure
   - Ping Hikvision device → alert if offline > 5 min

6. scripts/hikvision-setup.md: complete device configuration guide
   matching HIKVISION DEVICE SETUP GUIDE section in CLAUDE.md
```

---

### PROMPT 8 — TESTING (Day 9-10)
```
Implement the complete test suite for AttendTrack.

Domain Tests (AttendTrack.Domain.Tests):
- AttendanceRecord: CheckIn sets PunchSource.Hikvision, VerifyMode stored,
  late arrival → ShiftViolationDetectedEvent raised, ForceAutoCheckOut → MissedCheckout
- HourlySlot: valid creation, hour=24 throws, minutes=61 throws
- Employee: SoftDelete sets DeletedAt, IsActive=false

Application Tests (AttendTrack.Application.Tests):
HikvisionEventProcessingTests — all test cases from PROMPT 3:
  ParseXml_WithDateTimeOffset_ReturnsCorrectUtc
  ParseXml_WithUnixEpoch_ReturnsCorrectUtc
  Handle_CheckInEvent_CreatesRecord_PunchSourceHikvision
  Handle_DuplicateEvent_IsIdempotent
  Handle_UnknownEmployee_LogsError_NoException
  Handle_FaceCaptureProvided_SavedToDisk

Integration Tests (WebApplicationFactory + TestContainers PostgreSQL):
HikvisionWebhookTests:
  POST /api/hikvision/events from registered device IP → 200
  POST /api/hikvision/events from unknown IP → 403
  POST with multipart XML+JPEG → event logged and processed
  POST duplicate event → 200 (idempotent, no duplicate attendance record)
  POST invalid XML → 400

Test helpers:
  HikvisionEventXmlBuilder: builds valid test XML payloads
  TestFixtures: hikvision_checkin_sample.xml, hikvision_checkout_sample.xml
```

---

## COST SUMMARY (ZERO RECURRING)

```
Component                     Cost/month   Note
────────────────────────────────────────────────────────────────
PostgreSQL 16                 Rs. 0        Self-hosted Docker
Redis 7                       Rs. 0        Self-hosted Docker
ASP.NET Core 8 + Blazor       Rs. 0        MIT license
SignalR + MediatR + Serilog   Rs. 0        OSS
QuestPDF Community            Rs. 0        Free license tier
Let's Encrypt SSL             Rs. 0        Free certificate
Docker + GitHub Actions       Rs. 0        Free tier
All NuGet packages            Rs. 0        MIT / Apache 2.0
────────────────────────────────────────────────────────────────
TOTAL RECURRING LICENSING     Rs. 0 / month

Hikvision DS-K1T320MFWX      Rs. 8,000-12,000  ONE-TIME hardware
Ethernet cable + switch port  Rs. 500-1,000     One-time install
────────────────────────────────────────────────────────────────
OPERATIONAL COST              Rs. 0 / month (server power only)
```

---

*End of CLAUDE.md — AttendTrack Enterprise v2.0.0*
*Hikvision DS-K1T320MFWX integrated | 18 gaps identified and resolved*
*Generated by MK-AIFy / MAS Data Center, Chennai, Tamil Nadu, India*
*Zero recurring cost | DPDP Act 2023 compliant | Biometric-grade security*