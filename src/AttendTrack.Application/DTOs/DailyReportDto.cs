namespace AttendTrack.Application.DTOs;

/// <summary>Aggregated daily attendance summary with per-employee detail rows.</summary>
public sealed record DailyReportDto(
    string                       Date,               // "15 Jan 2025" IST
    int                          TotalPresent,
    int                          TotalAbsent,
    int                          TotalLate,
    int                          TotalMissedCheckout,
    IReadOnlyList<AttendanceDto> Records);
