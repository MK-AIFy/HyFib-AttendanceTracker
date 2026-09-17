using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;

namespace AttendTrack.Application.Queries.Reports;

/// <summary>Returns daily report rows for a 7-day week range.</summary>
public sealed record GetWeeklyReportQuery(
    DateOnly WeekStart) : IRequest<IReadOnlyList<DailyReportDto>>;

public sealed class GetWeeklyReportHandler
    : IRequestHandler<GetWeeklyReportQuery, IReadOnlyList<DailyReportDto>>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetWeeklyReportHandler(
        IAttendanceRepository attRepo,
        IEmployeeRepository   empRepo)
    {
        _attRepo = attRepo;
        _empRepo = empRepo;
    }

    public async Task<IReadOnlyList<DailyReportDto>> Handle(
        GetWeeklyReportQuery query, CancellationToken ct)
    {
        var weekEnd      = query.WeekStart.AddDays(6);
        var records      = await _attRepo.GetByDateRangeAsync(query.WeekStart, weekEnd, ct).ConfigureAwait(false);
        var allEmployees = await _empRepo.GetAllActiveAsync(ct).ConfigureAwait(false);
        var empMap       = allEmployees.ToDictionary(e => e.Id.Value);

        var result = new List<DailyReportDto>();
        for (var day = query.WeekStart; day <= weekEnd; day = day.AddDays(1))
        {
            var dayRecords = records.Where(r => r.WorkDate == day).ToList();
            var dtos = dayRecords.Select(r =>
            {
                empMap.TryGetValue(r.EmployeeId.Value, out var emp);
                var h = r.CheckInTime.HasValue && r.CheckOutTime.HasValue
                    ? Math.Max(0, (r.CheckOutTime.Value - r.CheckInTime.Value).TotalHours - r.BreakDuration.TotalHours)
                    : 0.0;
                return new AttendanceDto(
                    AttendanceRecordId:   r.Id,
                    EmployeeId:           r.EmployeeId.Value,
                    EmployeeCode:         emp?.EmployeeCode ?? "",
                    EmployeeName:         emp?.FullName ?? "Unknown",
                    WorkDate:             day.ToString("dd MMM yyyy"),
                    CheckInTimeIst:       r.CheckInTime.HasValue ? IstClock.FormatIstTime(r.CheckInTime.Value) : null,
                    CheckOutTimeIst:      r.CheckOutTime.HasValue ? IstClock.FormatIstTime(r.CheckOutTime.Value) : null,
                    TotalHoursWorked:     Math.Round(h, 2),
                    Status:               r.Status.ToString(),
                    CheckInSource:        r.CheckInSource.ToString(),
                    VerifyMode:           r.VerifyMode,
                    BreakDurationMinutes: (int)r.BreakDuration.TotalMinutes,
                    Notes:                r.Notes,
                    HasViolation:         !string.IsNullOrWhiteSpace(r.Notes));
            }).ToList();

            // "Absent" means an employee who was already employed on `day` but has no
            // attendance record. allEmployees is a snapshot of who is active *today*, so
            // for earlier days in the range it must be narrowed to who had actually
            // joined by `day` — otherwise someone hired mid-week gets counted as absent
            // for days before they existed.
            var eligibleCount = allEmployees.Count(e => e.JoinedAt <= day);

            result.Add(new DailyReportDto(
                Date:                day.ToString("dd MMM yyyy"),
                TotalPresent:        dtos.Count(d => d.Status == "Present"),
                TotalAbsent:         Math.Max(0, eligibleCount - dtos.Count),
                TotalLate:           dtos.Count(d => d.Notes?.Contains("Late") == true),
                TotalMissedCheckout: dtos.Count(d => d.Status == "MissedCheckout"),
                Records:             dtos.AsReadOnly()));
        }
        return result.AsReadOnly();
    }
}

