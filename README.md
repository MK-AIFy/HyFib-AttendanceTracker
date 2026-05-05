# AttendTrack Enterprise

**Production-grade, zero-licensing-cost, self-hosted attendance tracking system**  
integrated with **Hikvision DS-K1T320MFWX** (Face + Fingerprint + Card + PIN kiosk).

> Stack: .NET 8 · Blazor Server · PostgreSQL 16 · Redis 7 · Docker  
> Author: MK-AIFy / MAS Data Center, Chennai, Tamil Nadu, India  
> DPDP Act 2023 compliant · Zero recurring cost · Biometric-grade security

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Technology Stack](#technology-stack)
- [Solution Structure](#solution-structure)
- [Hikvision Device Integration](#hikvision-device-integration)
- [Getting Started](#getting-started)
- [Running Tests](#running-tests)
- [Docker Deployment](#docker-deployment)
- [Scripts](#scripts)
- [Security](#security)
- [DPDP Compliance](#dpdp-compliance)
- [Cost Summary](#cost-summary)

---

## Overview

AttendTrack Enterprise is a fully self-hosted employee attendance management system purpose-built for the **Hikvision DS-K1T320MFWX** biometric terminal. Employees authenticate via face recognition, fingerprint, RFID card, or PIN — attendance records are created automatically in real time via Hikvision's ISAPI event-push webhook.

A Blazor Server admin dashboard provides live attendance monitoring, hourly heat maps, shift violation reports, and employee enrollment management. A PIN-based kiosk fallback is available for admin-office use when the biometric terminal is offline.

---

## Architecture

### 3-Layer Integration Model

```
LAYER 1 — PRIMARY: ISAPI Event Push (Webhook)
  Hikvision Device ──POST /api/hikvision/events──▶ AttendTrack Server
  Trigger: Biometric match (face/FP/card/PIN) in real-time
  Latency: < 500ms | Reliability: 99.9% on stable LAN

LAYER 2 — FALLBACK: ISAPI Polling (Background Job)
  AttendTrack ──GET /ISAPI/AccessControl/AcsEvent──▶ Hikvision Device
  Runs every 2 minutes IF webhook gap detected (> 5 minutes silent)

LAYER 3 — ADMIN FALLBACK: Blazor PIN Kiosk
  EmployeeCode + 6-digit PIN via browser on admin office PC (IP-locked)
```

### ISAPI Event Push Data Flow

```
Employee presents biometric at DS-K1T320MFWX
  → Device authenticates (face/FP/card/PIN)
  → HTTP POST to /api/hikvision/events  (XML + optional JPEG)
  → HikvisionWebhookAuthMiddleware  (validates device IP + Basic auth)
  → HikvisionEventController        (parses multipart or raw XML body)
  → ProcessHikvisionEventCommand    (MediatR)
  → Archives to HikvisionEventLog   (dedup: device serial + time + employee)
  → Resolves employeeNoString → Employee record
  → Creates / updates AttendanceRecord  (PunchSource.Hikvision)
  → SignalR broadcast → Admin Dashboard live update
  → Returns HTTP 200 to device       (device retries on any non-200)
```

### Clean Architecture Layers

```
Domain        ──  Entities, Value Objects, Domain Events, Exceptions, Interfaces
Application   ──  CQRS Commands/Queries (MediatR), Pipeline Behaviours, DTOs
Infrastructure──  EF Core + PostgreSQL, Redis cache, Background Services,
                  Hikvision ISAPI client, Security middleware
Web           ──  Blazor Server pages, ASP.NET Core API controllers, SignalR hub
```

---

## Technology Stack

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
| HTTP Client | Refit | 7.x |
| PDF Export | QuestPDF Community (free) | Latest |
| XML Parsing | System.Xml.Linq | Built-in |
| Health Checks | AspNetCore.Diagnostics.HealthChecks | 8.x |
| Rate Limiting | ASP.NET Core Rate Limiting | 8.0 (built-in) |
| **Biometric Device** | **Hikvision DS-K1T320MFWX** | **ISAPI V2.0** |
| Primary DB | PostgreSQL 16 | Docker |
| Cache | Redis 7 | Docker |
| Reverse Proxy | Nginx Alpine | Latest |
| SSL | Let's Encrypt Certbot | Free |

---

## Solution Structure

```
AttendTrack/
├── src/
│   ├── AttendTrack.Domain/           # Zero dependencies — pure business logic
│   │   ├── Entities/                 # Employee, AttendanceRecord, HikvisionDevice, …
│   │   ├── ValueObjects/             # EmployeeId, WorkDuration, PinHash, …
│   │   ├── Enums/                    # PunchSource, AttendanceStatus, ShiftViolationType
│   │   ├── Events/                   # Domain events (checked in, shift violation, …)
│   │   ├── Exceptions/               # Domain exceptions
│   │   └── Interfaces/               # Repository + service contracts
│   │
│   ├── AttendTrack.Application/      # Use cases (CQRS)
│   │   ├── Commands/                 # CheckIn, CheckOut, ProcessHikvisionEvent, Enroll, …
│   │   ├── Queries/                  # Live attendance, hourly breakdown, reports, …
│   │   ├── Behaviours/               # Validation, Logging, Audit, Concurrency pipeline
│   │   └── DTOs/                     # AttendanceDto, HikvisionEventDto, …
│   │
│   ├── AttendTrack.Infrastructure/   # EF Core, Redis, background services, security
│   │   ├── Persistence/              # DbContext, entity configs, repositories, migrations
│   │   ├── Services/                 # HikvisionIsapiService, MissedPunchDetector, …
│   │   ├── Security/                 # PinHasher, IstTimeHelper, whitelist middleware
│   │   └── HealthChecks/             # PostgreSQL, Redis, Disk, Hikvision device checks
│   │
│   └── AttendTrack.Web/              # ASP.NET Core + Blazor Server
│       ├── Controllers/              # HikvisionEventController (webhook), Auth, Reports, …
│       ├── Hubs/                     # AttendanceHub (SignalR)
│       ├── Pages/                    # Admin dashboard, live attendance, kiosk, …
│       └── Middleware/               # GlobalException, SecurityHeaders, RequestAudit
│
├── tests/
│   ├── AttendTrack.Domain.Tests/     # 26 unit tests — entities + domain logic
│   ├── AttendTrack.Application.Tests/# 7 unit tests — Hikvision event processing
│   └── AttendTrack.Integration.Tests/# 5 integration tests (TestContainers + PostgreSQL)
│
├── docker/
│   ├── docker-compose.prod.yml
│   ├── docker-compose.dev.yml
│   └── nginx/nginx.conf
│
├── scripts/
│   ├── backup.sh                     # Daily pg_dump + face-captures backup
│   ├── restore.sh                    # Restore with confirmation guard
│   ├── healthcheck.sh                # Health polling + email alert
│   └── hikvision-setup.md            # Full device configuration guide
│
├── AttendTrack.sln
├── global.json                       # Pinned to .NET 8.0
└── CLAUDE.md                         # Master agent prompt + architecture spec
```

---

## Hikvision Device Integration

### Device: DS-K1T320MFWX

| Spec | Value |
|---|---|
| Firmware | V3.5.2 build 240701 |
| Authentication | Face · Fingerprint · RFID Card · PIN |
| Protocol | ISAPI V2.0 (HTTP REST) |
| Face capacity | 3,000 faces |
| Fingerprint capacity | 3,000 templates |
| Event log | 100,000 records local |
| Recommended mode | `faceAndFp` (Face + Fingerprint dual-factor) |

### Supported `attendanceStatus` values

| Device value | Action |
|---|---|
| `checkIn` | Employee check-in |
| `checkOut` | Employee check-out |
| `breakIn` | Break start |
| `breakOut` | Break end |

### Sample Event Payload (XML)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<EventNotificationAlert version="2.0">
  <ipAddress>192.168.1.50</ipAddress>
  <dateTime>2025-01-15T09:03:25+05:30</dateTime>
  <eventType>AccessControllerEvent</eventType>
  <AccessControllerEvent>
    <employeeNoString>EMP-001</employeeNoString>
    <name>John Doe</name>
    <currentVerifyMode>faceAndFp</currentVerifyMode>
    <attendanceStatus>checkIn</attendanceStatus>
  </AccessControllerEvent>
</EventNotificationAlert>
```

For full device setup instructions, see [scripts/hikvision-setup.md](scripts/hikvision-setup.md).

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker Desktop (PostgreSQL + Redis containers)
- Hikvision DS-K1T320MFWX on local network (or use test curl commands)

### Development Setup

```bash
# 1. Clone the repository
git clone https://github.com/MK-AIFy/HyFib-AttendanceTracker.git
cd HyFib-AttendanceTracker

# 2. Start infrastructure containers
docker compose -f docker/docker-compose.dev.yml up -d

# 3. Apply database migrations
dotnet ef database update \
  --project src/AttendTrack.Infrastructure \
  --startup-project src/AttendTrack.Web

# 4. Run the application
dotnet run --project src/AttendTrack.Web

# 5. Open admin dashboard
open https://localhost:7001/admin/dashboard
```

### Environment Variables (Development)

```env
ConnectionStrings__DefaultConnection=Host=localhost;Database=attendtrack;Username=attendtrack;Password=devpass
ConnectionStrings__Redis=localhost:6379
Hikvision__WebhookSecret=your-secret-here
Hikvision__FaceCaptureStoragePath=/app/face-captures
Jwt__SecretKey=your-jwt-secret
Kiosk__AllowedIpRanges=127.0.0.1,192.168.1.0/24
```

### Test the Webhook Locally

```bash
curl -X POST https://localhost:7001/api/hikvision/events \
  -H 'Authorization: Basic hikadmin:YOUR_PASSWORD' \
  -H 'Content-Type: application/xml' \
  -d @tests/AttendTrack.Application.Tests/Fixtures/hikvision_checkin_sample.xml
# Expected: {"status":"ok","recordId":"..."}
```

---

## Running Tests

### Unit Tests (no Docker required)

```bash
# Domain tests — 26 tests
dotnet test tests/AttendTrack.Domain.Tests

# Application tests — 7 tests
dotnet test tests/AttendTrack.Application.Tests

# All unit tests
dotnet test tests/AttendTrack.Domain.Tests tests/AttendTrack.Application.Tests
```

### Integration Tests (requires Docker Desktop)

Integration tests use **TestContainers** to spin up a real PostgreSQL 16 container.

```bash
# Ensure Docker Desktop is running, then:
dotnet test tests/AttendTrack.Integration.Tests
```

Integration test coverage (`HikvisionWebhookTests`):

| Test | Scenario |
|---|---|
| `Post_FromRegisteredDeviceIp_Returns200` | Valid device IP + auth → 200 |
| `Post_FromUnknownIp_Returns403` | Unregistered IP → 403 |
| `Post_MultipartXmlAndJpeg_EventLoggedAndProcessed` | Multipart (XML + JPEG) → event logged |
| `Post_DuplicateEvent_Returns200Idempotent_NoSecondRecord` | Same event twice → 200, no duplicate record |
| `Post_InvalidXml_Returns400` | Malformed XML → 400 |

### Test Results Summary

```
AttendTrack.Domain.Tests       →  26 passed ✅
AttendTrack.Application.Tests  →   7 passed ✅
AttendTrack.Integration.Tests  →   5 tests  ⚠️  (requires Docker Desktop)
```

---

## Docker Deployment

### Production

```bash
# Build and start all services
docker compose -f docker/docker-compose.prod.yml up -d --build

# Services started:
#   attendtrack-web   — ASP.NET Core + Blazor (port 8080)
#   postgres          — PostgreSQL 16
#   redis             — Redis 7
#   nginx             — Reverse proxy (ports 80/443, SSL via Let's Encrypt)
```

### Nginx Configuration Highlights

- SSL termination with Let's Encrypt
- `/api/hikvision/events` restricted to Hikvision device IPs only
- WebSocket proxy for `/hubs/*` (SignalR)
- Security headers: HSTS, CSP, X-Frame-Options DENY, X-Content-Type-Options nosniff
- Strips all `X-Forwarded-For` headers from external clients (OWASP protection)

---

## Scripts

| Script | Purpose | Schedule |
|---|---|---|
| `scripts/backup.sh` | PostgreSQL dump + face-captures gzip to `/backups/` | Daily 2 AM IST (cron) |
| `scripts/restore.sh` | Restore with interactive confirmation guard | Manual |
| `scripts/healthcheck.sh` | GET /health + device ping, email admin on failure | Every 2 minutes (cron) |
| `scripts/hikvision-setup.md` | Full Hikvision device configuration guide | Reference |

### Backup Schedule

- **Daily** backups retained for 30 days (`/backups/daily/`)
- **Weekly** backups retained for 12 weeks (`/backups/weekly/`)

---

## Security

### Authentication & Authorization

- **Biometric events**: Hikvision ISAPI Basic auth (device IP whitelisting + hashed credentials)
- **Admin UI**: ASP.NET Core Identity with role-based authorization (SuperAdmin, Admin, HRManager, Manager, Employee)
- **PIN Kiosk**: IP-locked to whitelisted office IPs; BCrypt work factor 12; 3-attempt lockout
- **JWT tokens**: Short-lived access tokens for API clients

### Security Controls

| Control | Implementation |
|---|---|
| IP whitelisting | `TcpIpWhitelistMiddleware` — reads only `RemoteIpAddress` (no X-Forwarded-For spoofing) |
| Device webhook auth | `HikvisionWebhookAuthMiddleware` — IP + Basic auth validation |
| Concurrency protection | PostgreSQL `UNIQUE(employee_id, work_date)` + EF Core xmin optimistic concurrency |
| Security headers | `SecurityHeadersMiddleware` — HSTS, CSP, X-Frame, nosniff |
| Rate limiting | ASP.NET Core built-in: 10 req/min on kiosk endpoints |
| Input validation | FluentValidation on all commands; server-side only |
| Password hashing | BCrypt, work factor 12 |

---

## DPDP Compliance

*(Digital Personal Data Protection Act 2023 — India)*

| Requirement | Implementation |
|---|---|
| Biometric data locality | Face/fingerprint **templates stored on device only** — never leave the Hikvision terminal |
| Face capture retention | JPEG captures auto-deleted after 30 days (`DataRetentionPurgeService`) |
| Audit trail | `AuditLog` entity captures all mutations with actor, old/new values (JSONB), IP |
| Right to deletion | `Employee.SoftDelete()` + `ExportEmployeeDataCommand` for DPDP data portability |
| Data residency | All data on-premises (self-hosted Docker, Chennai server) |
| 7-year retention | Hard purge of soft-deleted records older than 7 years (daily 2 AM IST) |

---

## Cost Summary

| Component | Cost/month |
|---|---|
| PostgreSQL 16 | ₹ 0 (self-hosted Docker) |
| Redis 7 | ₹ 0 (self-hosted Docker) |
| ASP.NET Core 8 + Blazor | ₹ 0 (MIT license) |
| All NuGet packages | ₹ 0 (MIT / Apache 2.0) |
| QuestPDF Community | ₹ 0 (free tier) |
| Let's Encrypt SSL | ₹ 0 |
| **Total recurring** | **₹ 0 / month** |

One-time hardware: Hikvision DS-K1T320MFWX ≈ ₹ 8,000–12,000

---

## Implemented Features (v2.0.0)

All 18 identified gaps have been resolved:

| # | Feature | Status |
|---|---|---|
| 1 | Auto-checkout safety net (missed punch detection) | ✅ |
| 2 | Concurrency protection (UNIQUE + xmin) | ✅ |
| 3 | Kiosk IP whitelist (TCP RemoteIpAddress only) | ✅ |
| 4 | Hourly granularity (HourlySlot + heat map) | ✅ |
| 5 | IST timezone handling (IstTimeHelper + TIMESTAMPTZ) | ✅ |
| 6 | DPDP compliance (AuditLog, soft-delete, data export) | ✅ |
| 7 | Redis graceful degradation (IMemoryCache fallback) | ✅ |
| 8 | Shift violation detection | ✅ |
| 9 | Backup strategy (daily/weekly pg_dump + face captures) | ✅ |
| 10 | Health checks (PostgreSQL, Redis, disk, Hikvision device) | ✅ |
| 11 | Rate limiting (10 req/min kiosk endpoints) | ✅ |
| 12 | Kiosk heartbeat (`/api/kiosk/heartbeat`) | ✅ |
| 13 | Security headers middleware | ✅ |
| 14 | Disaster recovery (restore.sh with confirmation guard) | ✅ |
| 15 | Data retention purge (7-year DPDP, daily 2 AM IST) | ✅ |
| 16 | Hikvision ISAPI event push (primary attendance source) | ✅ |
| 17 | Employee enrollment UI (Blazor + ISAPI) | ✅ |
| 18 | Hikvision device health check | ✅ |

---

*AttendTrack Enterprise v2.0.0 — MK-AIFy / MAS Data Center, Chennai, Tamil Nadu, India*  
*Zero recurring cost · DPDP Act 2023 compliant · Biometric-grade security*
