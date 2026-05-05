namespace AttendTrack.Application.DTOs;

/// <summary>Per-employee hourly breakdown for the heat-map page (Gap 4).</summary>
public sealed record HourlyBreakdownDto(
    Guid                         EmployeeId,
    string                       EmployeeCode,
    string                       EmployeeName,
    string                       WorkDate,
    IReadOnlyList<HourlySlotDto> Slots);

/// <summary>Single hour slot (0–23 IST) for the heat-map.</summary>
public sealed record HourlySlotDto(
    int    HourSlot,        // 0-23
    int    MinutesWorked,   // 0-60
    bool   IsBreak,
    bool   IsOvertime,
    string HourLabel);      // "09:00"
