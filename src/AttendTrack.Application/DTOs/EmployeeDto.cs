namespace AttendTrack.Application.DTOs;

/// <summary>Employee data DTO — safe for API responses (no PIN hash).</summary>
public sealed record EmployeeDto(
    Guid    EmployeeId,
    string  EmployeeCode,
    string  FullName,
    string  Email,
    string  Phone,
    Guid    DepartmentId,
    Guid    DefaultShiftId,
    string  Role,
    bool    IsActive,
    bool    IsBiometricEnrolled,
    string? BadgeRfidCard,
    string  JoinedAt);              // "01 Jan 2024" formatted
