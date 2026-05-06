# Hikvision DS-K1T320MFWX — Onboarding Runbook

End-to-end procedure for installing, configuring, and registering a
Hikvision **DS-K1T320MFWX** Face + Fingerprint + Card + PIN terminal with
AttendTrack Enterprise. Allow ~45 minutes per device for the first one,
~15 minutes for each subsequent device.

> Reference firmware tested: `V3.5.2 build 240701`. Older firmware may
> expose ISAPI endpoints on different paths — flag and update CLAUDE.md
> if you encounter divergence.

---

## 0. What you'll need

- The DS-K1T320MFWX, mounting bracket, 12 V DC PSU **or** PoE switch port
- Cat 5e/6 Ethernet cable (Wi-Fi is supported but **not recommended** for
  reliable webhook delivery)
- A Windows or Linux laptop on the same subnet as the device
- An admin login on AttendTrack (`SuperAdmin` or `Admin` role)
- One JPEG/PNG headshot per employee (well-lit, neutral background, ≤ 5 MB)

---

## 1. Physical install

1. Mount the device at **1.4 m** above floor level near the entrance the
   employee will use. Avoid backlight (windows behind the camera).
2. Connect Ethernet (preferred) **or** join Wi-Fi via the device's
   on-screen wizard.
3. Power on. The LCD displays a clock + the prompt
   "Authenticate via Fingerprint / Card / Face".

---

## 2. First-time network configuration

1. Find the device on the LAN. Default factory IP is `192.168.1.64`. If
   your DHCP scope isn't `192.168.1.0/24`, use Hikvision SADP Tool (free
   download from hikvision.com) to discover and re-IP it.
2. Browse to `http://<device-ip>` from your laptop.
3. First login forces a password change. Choose a **strong password ≥ 12
   chars** — store it in your password manager. This is the device admin
   password; it is also the credential AttendTrack uses for the webhook
   Basic auth (Step 4).
4. **Configuration → Network → Basic Settings → TCP/IP**
   - Mode: `Static`
   - IPv4: choose a free IP on your subnet (e.g. `192.168.1.50`)
   - Subnet mask + gateway: match your network
   - DNS: typically the gateway, or your internal DNS
   - Click **Save** → device reboots → reconnect on the new IP.
5. **Configuration → System → Time** → set timezone to
   `(GMT+05:30) Chennai, Kolkata, Mumbai, New Delhi` and enable **NTP**
   pointing at `time.windows.com` or your internal NTP. The device
   timestamp is propagated into `HikvisionEventLog.DeviceLocalTime`, so
   timezone correctness matters.

---

## 3. Configure attendance mode

**Configuration → Time and Attendance → Attendance Mode**

- Mode: `Attendance`
- Attendance Status: `Manual`
  Map status keys (top-row buttons on the keypad):
  - `0` → `checkIn`
  - `1` → `checkOut`
  - `2` → `breakIn`
  - `3` → `breakOut`
- Verify mode (recommended for office use): `Face & Fingerprint`. Drop to
  `Face` only if speed > security.
- Stranger mode: `Disable`.

---

## 4. Configure event push (webhook → AttendTrack)

This is the **primary** integration path. Without it, attendance only
arrives via the 2-minute polling fallback.

**Configuration → Network → Advanced Settings → HTTP Listening**

| Field | Value |
|---|---|
| Protocol | `HTTPS` (preferred) or `HTTP` for LAN-only labs |
| Format | `XML` |
| Listening Host IP | `192.168.1.10` *(your AttendTrack server IP)* |
| Listening Host Port | `443` (HTTPS) or `80` (HTTP) |
| URL | `/api/hikvision/events` |
| Authorization Type | `Basic` |
| Username | e.g. `hikadmin` |
| Password | strong password — record it |

**Enable events**: tick `AccessControllerEvent`. Set "Send picture with
event" to `Yes` (this delivers the JPEG face capture stored as
`AttendanceRecord.FaceCaptureImagePath`). Leave interval = `0` (send
immediately).

Click **Save** and wait 10 s for the device to reload its push config.

> If your AttendTrack TLS cert is self-signed, you may need to drop the
> webhook to HTTP/80 inside the LAN. The device firmware does not let you
> import a custom CA bundle.

---

## 5. Register the device in AttendTrack

Sign in at `https://attendtrack.local/login` as Admin → **Admin →
Devices → Register Device**.

| Field | Value |
|---|---|
| Device name | `Main Entrance` (any label) |
| Model | `DS-K1T320MFWX` |
| Serial number | from the device LCD `Configuration → System → System Settings → Device Information` |
| IP address | `192.168.1.50` |
| Port | `80` |
| Admin username | `admin` (the one you set in Step 2) |
| Admin password | the strong password from Step 2 |
| Location | `Admin Office - Main Entrance` |
| Firmware version | `V3.5.2 build 240701` |

Save. AttendTrack will:

1. Persist the device row to `hikvision_devices` (BCrypt-hash the password).
2. Allow webhook traffic from this IP (`HikvisionWebhookAuthMiddleware`).
3. Begin polling `GET /ISAPI/AccessControl/AcsEvent` every 2 min as a fallback.

---

## 6. Enroll employees

For each employee:

1. Admin → **Devices → Enroll Employees**.
2. In the enrollment panel:
   - **Employee code** — must match an existing `Employee.EmployeeCode`.
   - **Face photo** — pick a JPEG/PNG ≤ 5 MB.
   - Click **Enroll**.
3. AttendTrack calls the device ISAPI:
   - `PUT /ISAPI/AccessControl/UserInfo/SetUp` — creates the user with
     `employeeNo = EmployeeCode` and 36-year validity.
   - `PUT /ISAPI/Intelligent/FDLib/FaceDataRecord` — uploads the face
     template.
4. Walk the employee to the device and have them present their finger
   3 times to enroll the fingerprint locally
   (`Configuration → User → Add User → Fingerprint`). Optional: tap an
   RFID card on the reader to bind it to the user record.
5. AttendTrack flips `Employee.IsBiometricEnrolled = true` after the
   ISAPI calls succeed.

> **DPDP note**: Face *templates* and fingerprint *templates* never leave
> the device. AttendTrack only stores the JPEG photo used for enrollment
> (under `Hikvision:FaceCaptureStoragePath`) and the JPEG face captures
> attached to attendance events. Both are auto-purged after 30 days by
> `DataRetentionPurgeService`.

---

## 7. Smoke-test the integration

### 7a. Live event

Have an enrolled employee present their face/finger. Within 1 second:

- Device LCD: `Welcome, <Name>` + green tick
- AttendTrack admin dashboard `/admin/dashboard` → "Live Events" feed
  shows the event
- `attendance_records` row appears with:
  - `check_in_source = 'Hikvision'`
  - `verify_mode = 'faceAndFp'` (or as configured)
  - `face_capture_image_path` populated
  - `hikvision_serial_no` populated

### 7b. Manual webhook test (LAN only)

Replay a known-good payload from `tests/fixtures/` if present, or curl
directly from the server console:

```bash
curl -v -X POST https://localhost/api/hikvision/events \
  -u 'hikadmin:<password>' \
  -H 'Content-Type: application/xml' \
  --data-binary @sample-checkin.xml \
  --resolve attendtrack.local:443:127.0.0.1 -k
```

Expected: `HTTP/1.1 200`, response `{ "status":"ok","recordId":"…" }`.
**From a non-registered IP** the same request should return `403`.

### 7c. Polling fallback

Stop the device's HTTP listener (uncheck "Enable" in Step 4 temporarily),
present a face, then re-enable. Within 2 minutes the
`HikvisionPollingService` will fetch the missed event via
`GET /ISAPI/AccessControl/AcsEvent` and reconcile it.

### 7d. Health check

`GET /health` should report `Healthy` and include a `Hikvision-DS-K1T320MFWX`
sub-check (provided by `HikvisionDeviceHealthCheck`). If it shows
`Degraded`, check device IP reachability and credentials.

---

## 8. Day-2 operations

| Task | How |
|---|---|
| Replace a lost fingerprint | At the device: Configuration → User → select user → Fingerprint → Re-enroll. No AttendTrack action needed. |
| Replace a face photo | Admin → Devices → Enroll Employees → enter code + new photo → Enroll. Overwrites the device template. |
| Off-board employee | Admin → Employees → Deactivate. AttendTrack sets `IsActive=false`. **Manually** delete the user on the device: Configuration → User → select → Delete. (DPDP: also click "Forget data" in the employee profile to soft-delete; `DataRetentionPurgeService` hard-purges after 7 years.) |
| Firmware upgrade | Download from Hikvision portal → Configuration → System → Maintenance → Firmware Upgrade. Verify CLAUDE.md device spec still matches. |
| Move device to a new IP | Re-IP at the device first; **then** update the row in `hikvision_devices` (Admin UI → Edit Device). The webhook middleware filters by IP, so order matters. |

---

## 9. Troubleshooting matrix

| Symptom | Diagnostic | Fix |
|---|---|---|
| Webhook returns `401` | Check `Authorization` header on device matches the password stored on the device row | Re-enter password in **Admin → Devices → Edit**. AttendTrack BCrypt-verifies; passwords must round-trip exactly. |
| Webhook returns `403` | Server log shows `Webhook from unregistered IP` | The device's outbound IP differs from `hikvision_devices.IpAddress`. Update the row, or check NAT/proxy. The middleware reads only `RemoteIpAddress`, never `X-Forwarded-For`. |
| Webhook returns `429` | Device firmware re-trying aggressively | The `webhook` rate-limit policy allows 200 req/min/IP. Investigate the retry storm — typically a clock skew causing duplicate events. |
| Event arrives but no `AttendanceRecord` | `hikvision_event_logs.processing_error` populated | Common reasons: `EmployeeCode` not in `employees`; concurrent check-in race (idempotent — second event ignored); shift not assigned. Inspect the row. |
| `attendanceStatus` always `checkIn` | Device status keys not configured | Step 3 above — set status keys 0/1/2/3 to checkIn/checkOut/breakIn/breakOut. |
| JPEG missing on event | Step 4 didn't enable "Send picture" | Re-tick the option, save, wait 10 s. |
| Polling never recovers a missed event | `HikvisionEventLog` UNIQUE key prevented duplicate insert (good) but record creation failed | Look for `processing_error` on the event log row; if `EmployeeCode` mismatched, the polling loop won't retry by itself — fix the employee record and re-run **Admin → Devices → Sync Now**. |
| Face capture stored but face on event was wrong person | Device template overfit during enrollment | Re-enroll: delete face template on device, re-upload via Step 6. Use a higher-quality photo. |

---

## 10. Hardening checklist (post-pilot)

- [ ] All device admin passwords stored in a password manager + rotated
      annually.
- [ ] Webhook on **HTTPS** only (HTTP allowed only for proven-LAN labs).
- [ ] Server's TLS cert chain trusted by every device that pushes events.
- [ ] `Kiosk:AllowedIpRanges` in `appsettings.Production.json` matches
      the kiosk subnet (Blazor PIN fallback only).
- [ ] Outbound internet blocked on the device — it doesn't need it.
- [ ] Backups verified by a quarterly test restore (see
      `scripts/restore.sh` / `scripts/backup.ps1`).
- [ ] Quarterly review of `hikvision_event_logs WHERE is_processed=false`
      for anomalies.

---

## Appendix A — deferred from Phase 3

These were called out in CLAUDE.md Phase 3 as deferred. Re-evaluate
during the first quarterly review:

- **`Employee.DepartmentId` / `DefaultShiftId` foreign keys** — currently
  un-constrained because seed data may contain `Guid.Empty`. Once seed
  data is clean (after first full deployment), add an EF migration with
  `OnDelete: Restrict` for both relations.
- **`attendance_records.notes` length cap** — domain currently accepts
  arbitrary length. Bound to e.g. `varchar(2000)` in the same migration
  to defend against UI abuse / accidental log dumps in the column.
