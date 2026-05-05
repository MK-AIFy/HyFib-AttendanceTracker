using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;

namespace AttendTrack.Application.Queries.Reports;

/// <summary>Returns attendance records where worked hours exceeded the overtime threshold.</summary>
public sealed record GetOvertimeReportQuery(
    DateOnly From,
    DateOnly To,
    double   OvertimeThresholdHours = 9.0) : IRequest<IReadOnlyList<AttendanceDto>>;

public sealed class GetOvertimeReportHandler
    : IRequestHandler<GetOvertimeReportQuery, IReadOnlyList<AttendanceDto>>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetOvertimeReportHandler(
        IAttendanceRepository attRepo,
        IEmployeeRepository   empRepo)
    {
        _attRepo = attRepo;
        _empRepo = empRepo;
    }

    public async Task<IReadOnlyList<AttendanceDto>> Handle(
        GetOvertimeReportQuery query, CancellationToken ct)
    {
        var records      = await _attRepo.GetByDateRangeAsync(query.From, query.To, ct).ConfigureAwait(false);
        var allEmployees = await _empRepo.GetAllActiveAsync(ct).ConfigureAwait(false);
        var empMap       = allEmployees.ToDictionary(e => e.Id.Value);

        return records
            .Where(r => r.CheckInTime.HasValue && r.CheckOutTime.HasValue)
            .Select(r =>
            {
                empMap.TryGetValue(r.EmployeeId.Value, out var emp);
                var h = Math.Max(0,
                    (r.CheckOutTime!.Value - r.CheckInTime!.Value).TotalHours
                    - r.BreakDuration.TotalHours);
                return (record: r, emp, hours: h);
            })
            .Where(x => x.hours > query.OvertimeThresholdHours)
            .Select(x => new AttendanceDto(
                AttendanceRecordId:   x.record.Id,
                EmployeeId:           x.record.EmployeeId.Value,
                EmployeeCode:         x.emp?.EmployeeCode ?? "",
                EmployeeName:         x.emp?.FullName ?? "Unknown",
                WorkDate:             x.record.WorkDate.ToString("dd MMM yyyy"),
                CheckInTimeIst:       IstClock.FormatIstTime(x.record.CheckInTime!.Value),
                CheckOutTimeIst:      IstClock.FormatIstTime(x.record.CheckOutTime!.Value),
                TotalHoursWorked:     Math.Round(x.hours, 2),
                Status:               x.record.Status.ToString(),
                CheckInSource:        x.record.CheckInSource.ToString(),
                VerifyMode:           x.record.VerifyMode,
                BreakDurationMinutes: (int)x.record.BreakDuration.TotalMinutes,
                Notes:                x.record.Notes,
                HasViolation:         false))
            .ToList().AsReadOnly();
    }
}

