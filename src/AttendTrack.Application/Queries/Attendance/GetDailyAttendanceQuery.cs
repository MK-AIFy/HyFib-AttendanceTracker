using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;

namespace AttendTrack.Application.Queries.Attendance;

/// <summary>Returns full attendance report for a specific IST date.</summary>
public sealed record GetDailyAttendanceQuery(
    DateOnly Date) : IRequest<DailyReportDto>;

public sealed class GetDailyAttendanceHandler
    : IRequestHandler<GetDailyAttendanceQuery, DailyReportDto>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetDailyAttendanceHandler(
        IAttendanceRepository attRepo,
        IEmployeeRepository   empRepo)
    {
        _attRepo = attRepo;
        _empRepo = empRepo;
    }

    public async Task<DailyReportDto> Handle(GetDailyAttendanceQuery query, CancellationToken ct)
    {
        var records      = await _attRepo.GetByDateAsync(query.Date, ct).ConfigureAwait(false);
        var allEmployees = await _empRepo.GetAllActiveAsync(ct).ConfigureAwait(false);
        var empMap       = allEmployees.ToDictionary(e => e.Id.Value);

        var dtos = records.Select(r =>
        {
            empMap.TryGetValue(r.EmployeeId.Value, out var emp);
            var hoursWorked = r.CheckInTime.HasValue && r.CheckOutTime.HasValue
                ? (r.CheckOutTime.Value - r.CheckInTime.Value).TotalHours - r.BreakDuration.TotalHours
                : 0.0;

            return new AttendanceDto(
                AttendanceRecordId:    r.Id,
                EmployeeId:            r.EmployeeId.Value,
                EmployeeCode:          emp?.EmployeeCode ?? "",
                EmployeeName:          emp?.FullName ?? "Unknown",
                WorkDate:              query.Date.ToString("dd MMM yyyy"),
                CheckInTimeIst:        r.CheckInTime.HasValue ? IstClock.FormatIstTime(r.CheckInTime.Value) : null,
                CheckOutTimeIst:       r.CheckOutTime.HasValue ? IstClock.FormatIstTime(r.CheckOutTime.Value) : null,
                TotalHoursWorked:      Math.Max(0, Math.Round(hoursWorked, 2)),
                Status:                r.Status.ToString(),
                CheckInSource:         r.CheckInSource.ToString(),
                VerifyMode:            r.VerifyMode,
                BreakDurationMinutes:  (int)r.BreakDuration.TotalMinutes,
                Notes:                 r.Notes,
                HasViolation:          !string.IsNullOrWhiteSpace(r.Notes));
        }).ToList();

        return new DailyReportDto(
            Date:                query.Date.ToString("dd MMM yyyy"),
            TotalPresent:        dtos.Count(d => d.Status == "Present"),
            TotalAbsent:         allEmployees.Count - dtos.Count,
            TotalLate:           dtos.Count(d => d.HasViolation && d.Notes?.Contains("Late") == true),
            TotalMissedCheckout: dtos.Count(d => d.Status == "MissedCheckout"),
            Records:             dtos.AsReadOnly());
    }
}

