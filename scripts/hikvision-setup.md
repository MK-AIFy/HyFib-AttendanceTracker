# Hikvision DS-K1T320MFWX Setup Guide

**Device**: Hikvision DS-K1T320MFWX  
**Firmware**: V3.5.2 build 240701  
**Protocol**: ISAPI V2.0  

---

## Step 1: Physical Installation

1. Mount DS-K1T320MFWX at eye level (1.4m) at the admin office entrance.
2. Connect via **Ethernet (RJ45)** — USE ETHERNET, NOT WI-FI.  
   Ethernet is mandatory for reliable real-time event push.
3. Connect 12V DC power or use a PoE-enabled switch port.
4. Device boots — LCD shows time and `Authenticate via FP`.

---

## Step 2: Network Configuration via Web Interface

```
Browser → http://192.168.1.64   (factory default IP)
Login:    admin / (serial number as default password)

Navigate: Configuration > Network > TCP/IP
  Set: IP      = 192.168.1.50   (static)
       Subnet  = 255.255.255.0
       Gateway = 192.168.1.1
       DNS     = 192.168.1.1

Save → device reboots → reconnect to http://192.168.1.50
```

---

## Step 3: Configure Event Push (Webhook to AttendTrack)

```
Navigate: Configuration > Network > Advanced > HTTP Listening

Protocol:             HTTP
Parameter Format:     XML
Listening Host IP:    192.168.1.10       (AttendTrack server IP)
Listening Host Port:  443
URL:                  /api/hikvision/events
Authorization Type:   Basic
Username:             hikadmin
Password:             [strong password — register in AttendTrack device DB]

Events to enable:
  ☑  AccessControllerEvent
       → checkIn, checkOut, breakIn, breakOut

☑  Send Picture:  YES   (sends JPEG face capture with each event)
   Interval:      0     (send immediately on event — no delay)
```

> **Important**: After setting the password here, register this device in
> AttendTrack Admin UI → Devices → Register Device with the same IP and password.

---

## Step 4: Attendance Mode Configuration

```
Navigate: Configuration > Time & Attendance > General Settings
  Attendance Rule:  Enable
  Work Mode:        Attendance Mode

Navigate: Configuration > Time & Attendance > Attendance Status Settings
  Status Key 0 → Check In   (attendanceStatus: checkIn)
  Status Key 1 → Check Out  (attendanceStatus: checkOut)
  Status Key 2 → Break In   (attendanceStatus: breakIn)
  Status Key 3 → Break Out  (attendanceStatus: breakOut)

Recommended Verify Mode (office use):
  Navigate: Configuration > Access Control > Authentication
  Verify Mode: Face + Fingerprint (faceAndFp)
  
  Fallback chain:
    1. Face + Fingerprint  (most secure)
    2. Card                (RFID tap)
    3. PIN                 (keypad 6-digit — last resort)
  
  Grace period for verify failure: 3 attempts, then deny
```

---

## Step 5: Employee Enrollment via AttendTrack Admin UI

**Prerequisites**: Employee must be created in AttendTrack first (Admin → Employees → Add).

### Enrollment Process

```
1. Admin → AttendTrack → Devices → DS-K1T320MFWX → Enroll Employees
2. Search employee by name or code
3. Click [Enroll] next to the employee
4. AttendTrack calls ISAPI:
     PUT /ISAPI/AccessControl/UserInfo/SetUp
   to create the user on the device

5. Employee at the device terminal:
   a. FACE:        Stand 0.3–1m from camera
                   Device captures 5 angles automatically
                   Follow LCD prompts (look straight, turn left/right)
   
   b. FINGERPRINT: Press right index finger on scanner
                   Lift and re-press 3 times as prompted
                   Green ring = successful enrollment
   
   c. RFID CARD:   Tap card on device when prompted (optional)

6. AttendTrack updates: Employee.IsBiometricEnrolled = true
7. Dashboard badge changes from ❌ to ✅
```

> **DPDP Act 2023 Compliance**: Face templates and fingerprint templates
> are stored **ONLY on the device**. They never leave the device.
> Only JPEG face captures (not templates) are sent with attendance events
> and stored by AttendTrack. Face capture photos are auto-deleted after 30 days.

---

## Step 6: Verify Integration End-to-End

### Manual Test (at device)
```
1. Employee scans at device → LCD shows 'Welcome, [Name]'
2. Device sends HTTP POST to /api/hikvision/events (< 500ms)
3. AttendTrack processes → creates AttendanceRecord
4. Admin dashboard updates in real-time (SignalR)
```

### Test with curl (from AttendTrack server)
```bash
curl -X POST https://localhost/api/hikvision/events \
  -H 'Authorization: Basic hikadmin:YOUR_PASSWORD_HERE' \
  -H 'Content-Type: application/xml' \
  -d '<?xml version="1.0" encoding="UTF-8"?>
<EventNotificationAlert version="2.0">
  <ipAddress>192.168.1.50</ipAddress>
  <dateTime>2025-01-15T09:03:25+05:30</dateTime>
  <eventType>AccessControllerEvent</eventType>
  <AccessControllerEvent>
    <employeeNoString>EMP-001</employeeNoString>
    <name>Test Employee</name>
    <currentVerifyMode>faceAndFp</currentVerifyMode>
    <attendanceStatus>checkIn</attendanceStatus>
  </AccessControllerEvent>
</EventNotificationAlert>'
```
Expected response: `{"status":"ok","recordId":"..."}`

### Verify Nginx IP restriction
```bash
# From non-device IP (should return 403)
curl -sk -o /dev/null -w "%{http_code}" https://attendtrack.local/api/hikvision/events
# Expected: 403
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Device LCD shows `Network Error` | Wrong AttendTrack server IP/port | Check HTTP Listening config |
| No events in AttendTrack | Webhook not reaching server | Check Nginx logs, firewall |
| `403 Forbidden` in device logs | IP not whitelisted in Nginx | Add device IP to nginx.conf `allow` |
| `401 Unauthorized` | Wrong Basic auth password | Re-register device in AttendTrack UI |
| Employee not found in events | EmployeeCode mismatch | Ensure `employeeNoString` matches `Employee.EmployeeCode` |
| Face recognition fails | Poor lighting / obstruction | Clean IR ring, check 1.4m mount height |
| Polling only (no webhooks) | Firewall blocking inbound from device | Open port 443 for device IP |

---

## Maintenance

### Daily Backup
```bash
# Runs automatically via cron:
# 0 2 * * * /scripts/backup.sh
```

### Health Check
```bash
# Runs every 2 minutes via cron:
# */2 * * * * /scripts/healthcheck.sh
```

### View Device Event Log in AttendTrack
```
Admin → Devices → [Device Name] → Event Log
  Filter by date range, employee, or auth method
  Export as CSV for audit
```

---

*Device: Hikvision DS-K1T320MFWX | ISAPI V2.0 | AttendTrack Enterprise v2.0.0*
*MAS Data Center, Chennai, Tamil Nadu, India*

