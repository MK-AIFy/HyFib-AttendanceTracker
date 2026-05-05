namespace AttendTrack.Application.DTOs;

/// <summary>Shift violation record for the ShiftViolations report page (Gap 8).</summary>
public sealed record ShiftViolationDto(
    Guid    AttendanceRecordId,
    Guid    EmployeeId,
    string  EmployeeCode,
    string  EmployeeName,
    string  WorkDate,
    string  Notes,
    string? CheckInTimeIst,
    string? CheckOutTimeIst);
