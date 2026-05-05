namespace AttendTrack.Application.DTOs;

/// <summary>Attendance record DTO with IST-formatted times for display.</summary>
public sealed record AttendanceDto(
    Guid    AttendanceRecordId,
    Guid    EmployeeId,
    string  EmployeeCode,
    string  EmployeeName,
    string  WorkDate,               // "15 Jan 2025" IST
    string? CheckInTimeIst,         // "09:03 AM"
    string? CheckOutTimeIst,        // "06:15 PM"
    double  TotalHoursWorked,
    string  Status,                 // Present / Absent / MissedCheckout / HalfDay
    string  CheckInSource,          // Hikvision / BlazorKiosk / Manual
    string? VerifyMode,             // faceAndFp / face / fp / card / pin
    int     BreakDurationMinutes,
    string? Notes,
    bool    HasViolation);
