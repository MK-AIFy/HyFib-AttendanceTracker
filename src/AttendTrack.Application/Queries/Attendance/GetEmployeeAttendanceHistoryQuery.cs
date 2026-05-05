using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using MediatR;

namespace AttendTrack.Application.Queries.Attendance;

/// <summary>Returns paginated attendance history for one employee.</summary>
public sealed record GetEmployeeAttendanceHistoryQuery(
    Guid     EmployeeId,
    DateOnly From,
    DateOnly To) : IRequest<IReadOnlyList<AttendanceDto>>;

public sealed class GetEmployeeAttendanceHistoryHandler
    : IRequestHandler<GetEmployeeAttendanceHistoryQuery, IReadOnlyList<AttendanceDto>>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetEmployeeAttendanceHistoryHandler(
        IAttendanceRepository attRepo,
        IEmployeeRepository   empRepo)
    {
        _attRepo = attRepo;
        _empRepo = empRepo;
    }

    public async Task<IReadOnlyList<AttendanceDto>> Handle(
        GetEmployeeAttendanceHistoryQuery query, CancellationToken ct)
    {
        var employee = await _empRepo.GetByIdAsync(
            EmployeeId.From(query.EmployeeId), ct).ConfigureAwait(false);

        var records = await _attRepo.GetByEmployeeAsync(
            EmployeeId.From(query.EmployeeId), query.From, query.To, ct).ConfigureAwait(false);

        return records
            .OrderByDescending(r => r.WorkDate)
            .Select(r =>
            {
                var hoursWorked = r.CheckInTime.HasValue && r.CheckOutTime.HasValue
                    ? (r.CheckOutTime.Value - r.CheckInTime.Value).TotalHours - r.BreakDuration.TotalHours
                    : 0.0;

                return new AttendanceDto(
                    AttendanceRecordId:    r.Id,
                    EmployeeId:            r.EmployeeId.Value,
                    EmployeeCode:          employee?.EmployeeCode ?? "",
                    EmployeeName:          employee?.FullName ?? "Unknown",
                    WorkDate:              r.WorkDate.ToString("dd MMM yyyy"),
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

