# AttendTrack Enterprise — Windows Server Deployment Guide

This document covers deploying AttendTrack to a Windows Server (2019/2022) host
using IIS + the ASP.NET Core 9 Hosting Bundle, with PostgreSQL 16 and
Memurai (Redis-compatible) as on-box dependencies. Zero recurring licensing cost.

---

## 1. Prerequisites

| Requirement | Minimum |
|---|---|
| OS | Windows Server 2019 or 2022 (Standard or Datacenter) |
| CPU / RAM | 2 vCPU / 4 GB (8 GB recommended for ≥ 200 employees) |
| Disk | 50 GB SSD (DB + face captures + logs + 30 days of backups) |
| Network | Static internal IP, reachable from Hikvision DS-K1T320MFWX |
| Privileges | Local Administrator for install + Task Scheduler |
| TLS cert | Public CA or internal AD CS (`*.attendtrack.local` works for LAN-only) |

The Hikvision device, the AttendTrack server, and any kiosk PC running the
Blazor PIN fallback must all live on the same routable subnet.

---

## 2. One-shot install

From an **elevated PowerShell** session in the repo root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\scripts\install.ps1 -PostgresPassword 'StrongP@ss!ChangeMe'
```

The script installs:

1. Chocolatey (package manager)
2. PostgreSQL 16 (`postgresql-x64-16` Windows service)
3. Memurai Developer (`Memurai` Windows service — Redis-compatible)
4. .NET 9 ASP.NET Core Hosting Bundle (registers `AspNetCoreModuleV2`)
5. IIS role + websockets + management console
6. App pool `AttendTrack` (No Managed Code, AlwaysRunning)
7. Site `AttendTrack` bound to port 443 with placeholder physical path `C:\AttendTrack\web`

Subdirectories created under `C:\AttendTrack`: `web`, `logs`, `face-captures`, `backups\daily`, `backups\weekly`.

> Re-run with `-SkipPostgres -SkipRedis` etc. to install only missing components.

---

## 3. Database bootstrap

```powershell
$env:PGPASSWORD = 'StrongP@ss!ChangeMe'
& 'C:\Program Files\PostgreSQL\16\bin\psql.exe' -U postgres -h localhost -c "CREATE USER attendtrack WITH PASSWORD 'attendtrack_db_pwd';"
& 'C:\Program Files\PostgreSQL\16\bin\psql.exe' -U postgres -h localhost -c "CREATE DATABASE attendtrack OWNER attendtrack;"
Remove-Item Env:PGPASSWORD
```

Migrations are applied automatically by `DbInitializer` on first app boot
(see `Infrastructure/Persistence/DbInitializer.cs`). Default admin
`ADMIN-001` / PIN `123456` is seeded if no SuperAdmin exists. **Change the
PIN immediately after first login.**

---

## 4. Publish + deploy the web app

From a developer workstation (or CI):

```powershell
dotnet publish src/AttendTrack.Web/AttendTrack.Web.csproj `
  -c Release -r win-x64 --self-contained false `
  -o .\publish
```

Copy the publish output to the server (e.g. via `robocopy` over SMB or a
secure file share):

```powershell
robocopy .\publish \\attendtrack-srv\C$\AttendTrack\web /MIR /XO
```

Copy the production settings template once and fill in real secrets:

```powershell
# On the server:
Copy-Item C:\AttendTrack\web\appsettings.Production.template.json `
          C:\AttendTrack\web\appsettings.Production.json
notepad C:\AttendTrack\web\appsettings.Production.json
```

Required substitutions:

- `ConnectionStrings:DefaultConnection` — replace `__REPLACE_ME__` with the
  `attendtrack_db_pwd` you chose above.
- `Jwt:SecretKey` — generate a random 32+ char value:
  `[Convert]::ToBase64String((1..48 | %{ [byte](Get-Random -Max 256) }))`
- `Cors:AllowedOrigins` — set to your real hostname.
- `Kiosk:AllowedIpRanges` — match your kiosk subnet.

Restart the app pool to pick up changes:

```powershell
Import-Module WebAdministration
Restart-WebAppPool -Name AttendTrack
```

---

## 5. TLS binding

In **IIS Manager** → site `AttendTrack` → Bindings → select `https / 443` →
Edit → choose your certificate (Server Certificates store).

For an internal host, generate one with AD CS or:

```powershell
New-SelfSignedCertificate -DnsName 'attendtrack.local' -CertStoreLocation Cert:\LocalMachine\My
```

…then import the public key on every kiosk + admin workstation as a Trusted
Root CA.

---

## 6. Scheduled tasks

Two recurring tasks must be registered. Run as `NT AUTHORITY\SYSTEM` so
they survive console logoff.

### 6a. Daily backup (02:30 local time)

```powershell
$action = New-ScheduledTaskAction `
  -Execute 'PowerShell.exe' `
  -Argument '-NoProfile -ExecutionPolicy Bypass -File C:\AttendTrack\scripts\backup.ps1 -PgPassword attendtrack_db_pwd'

$trigger = New-ScheduledTaskTrigger -Daily -At 2:30am

Register-ScheduledTask -TaskName 'AttendTrack-Backup' `
  -Action $action -Trigger $trigger `
  -User 'NT AUTHORITY\SYSTEM' -RunLevel Highest -Force
```

### 6b. Health check (every 5 minutes)

```powershell
$action = New-ScheduledTaskAction `
  -Execute 'PowerShell.exe' `
  -Argument '-NoProfile -ExecutionPolicy Bypass -File C:\AttendTrack\scripts\healthcheck.ps1 -HealthUrl https://localhost/health -IgnoreCertificateErrors'

$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
  -RepetitionInterval (New-TimeSpan -Minutes 5)

Register-ScheduledTask -TaskName 'AttendTrack-HealthCheck' `
  -Action $action -Trigger $trigger `
  -User 'NT AUTHORITY\SYSTEM' -RunLevel Highest -Force
```

For email alerts, append `-SmtpServer ...`, `-SmtpFrom`, `-SmtpTo` to the
healthcheck argument string.

---

## 7. Firewall rules

```powershell
New-NetFirewallRule -DisplayName 'AttendTrack HTTPS in' -Direction Inbound `
  -Protocol TCP -LocalPort 443 -Action Allow -Profile Domain,Private

# Hikvision device sends webhooks TO the server on 443 (above is sufficient).
# Outbound from server to device on 80 (ISAPI polling/enrollment) is allowed by default.
```

---

## 8. Verification

```powershell
# 1. Service health
Get-Service postgresql-x64-16, Memurai, W3SVC, WAS | Format-Table Name, Status

# 2. App pool
Get-WebAppPoolState AttendTrack

# 3. Health endpoint (matches what the scheduled task probes)
Invoke-WebRequest https://localhost/health -SkipCertificateCheck

# 4. Login page reachable
(Invoke-WebRequest https://localhost/login -SkipCertificateCheck).StatusCode  # expect 200
```

Sign in at `https://attendtrack.local/login` with `ADMIN-001` / `123456` →
**immediately rotate the PIN** under Employees → Edit Self.

---

## 9. Upgrade procedure

```powershell
# On the server (as Administrator):
Import-Module WebAdministration
Stop-WebAppPool -Name AttendTrack

# Backup first
& C:\AttendTrack\scripts\backup.ps1 -PgPassword attendtrack_db_pwd

# Replace files (preserves appsettings.Production.json + logs)
robocopy \\build-server\drop\publish C:\AttendTrack\web /MIR /XO /XF appsettings.Production.json

Start-WebAppPool -Name AttendTrack

# Watch the rolling log — migrations apply on first request
Get-Content C:\AttendTrack\logs\attendtrack-*.log -Tail 50 -Wait
```

---

## 10. Common issues

| Symptom | Fix |
|---|---|
| 502.5 ANCM Out-of-Process startup failure | Hosting Bundle missing — re-run `install.ps1 -SkipPostgres -SkipRedis`. Verify `aspnetcorev2.dll` registered: `appcmd list modules`. |
| 500.30 in-process startup failure | Check `C:\AttendTrack\logs\stdout*.log` (enable in `web.config` only when debugging). Usually a missing connection string in `appsettings.Production.json`. |
| Login redirects to `/login` even after success | Cookie domain mismatch. Ensure user reaches the app via the same hostname listed in `Cors:AllowedOrigins`. |
| `RedisConnectionException` floods the log | Memurai not running. `Get-Service Memurai`; `Start-Service Memurai`. App keeps working via `IDistributedCacheWrapper`'s `IMemoryCache` fallback in the meantime. |
| Hikvision device webhooks return 403 | Device IP not registered in `hikvision_devices`. Add via Admin → Devices → Register Device. |
| pg_dump exits non-zero in scheduled task | `PgDumpPath` differs from default. Pass `-PgDumpPath 'C:\Program Files\PostgreSQL\16\bin\pg_dump.exe'`. |
