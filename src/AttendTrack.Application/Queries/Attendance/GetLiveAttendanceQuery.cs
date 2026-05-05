using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;

namespace AttendTrack.Application.Queries.Attendance;

/// <summary>Returns today's attendance records for the live dashboard.</summary>
public sealed record GetLiveAttendanceQuery : IRequest<IReadOnlyList<AttendanceDto>>;

public sealed class GetLiveAttendanceHandler
    : IRequestHandler<GetLiveAttendanceQuery, IReadOnlyList<AttendanceDto>>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetLiveAttendanceHandler(
        IAttendanceRepository attRepo,
        IEmployeeRepository   empRepo)
    {
        _attRepo = attRepo;
        _empRepo = empRepo;
    }

    public async Task<IReadOnlyList<AttendanceDto>> Handle(
        GetLiveAttendanceQuery query, CancellationToken ct)
    {
        var today   = IstClock.TodayIst;
        var records = await _attRepo.GetByDateAsync(today, ct).ConfigureAwait(false);
        var allEmployees = await _empRepo.GetAllActiveAsync(ct).ConfigureAwait(false);
        var empMap  = allEmployees.ToDictionary(e => e.Id.Value);

        return records.Select(r =>
        {
            empMap.TryGetValue(r.EmployeeId.Value, out var emp);
            var hoursWorked = r.CheckInTime.HasValue && r.CheckOutTime.HasValue
                ? (r.CheckOutTime.Value - r.CheckInTime.Value).TotalHours - r.BreakDuration.TotalHours
                : r.CheckInTime.HasValue
                    ? (DateTime.UtcNow - r.CheckInTime.Value).TotalHours - r.BreakDuration.TotalHours
                    : 0.0;

            return new AttendanceDto(
                AttendanceRecordId:    r.Id,
                EmployeeId:            r.EmployeeId.Value,
                EmployeeCode:          emp?.EmployeeCode ?? "",
                EmployeeName:          emp?.FullName ?? "Unknown",
                WorkDate:              IstClock.FormatIstDate(r.CheckInTime ?? DateTime.UtcNow),
                CheckInTimeIst:        r.CheckInTime.HasValue ? IstClock.FormatIstTime(r.CheckInTime.Value) : null,
                CheckOutTimeIst:       r.CheckOutTime.HasValue ? IstClock.FormatIstTime(r.CheckOutTime.Value) : null,
                TotalHoursWorked:      Math.Max(0, Math.Round(hoursWorked, 2)),
                Status:                r.Status.ToString(),
                CheckInSource:         r.CheckInSource.ToString(),
                VerifyMode:            r.VerifyMode,
                BreakDurationMinutes:  (int)r.BreakDuration.TotalMinutes,
                Notes:                 r.Notes,
                HasViolation:          !string.IsNullOrWhiteSpace(r.Notes));
        }).ToList().AsReadOnly();
    }
}

