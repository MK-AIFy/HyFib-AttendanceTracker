namespace AttendTrack.Application.DTOs;

/// <summary>API response DTO for Hikvision event status (used by admin event log views).</summary>
public sealed record HikvisionEventDto(
    Guid    Id,
    string  DeviceSerialNumber,
    string  EmployeeCode,
    string  EmployeeName,
    string  AttendanceStatus,
    string  VerifyMode,
    string? CardNo,
    string  DeviceLocalTime,   // IST formatted: "15 Jan 2025 09:03 AM"
    bool    IsProcessed,
    string? ProcessingError,
    Guid?   AttendanceRecordId,
    string? FaceCaptureStoragePath);
