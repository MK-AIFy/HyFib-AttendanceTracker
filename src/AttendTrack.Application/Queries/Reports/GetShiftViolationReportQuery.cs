using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;

namespace AttendTrack.Application.Queries.Reports;

/// <summary>Returns records with shift violations (late, early departure) for Gap 8 report.</summary>
public sealed record GetShiftViolationReportQuery(
    DateOnly From,
    DateOnly To) : IRequest<IReadOnlyList<ShiftViolationDto>>;

public sealed class GetShiftViolationReportHandler
    : IRequestHandler<GetShiftViolationReportQuery, IReadOnlyList<ShiftViolationDto>>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetShiftViolationReportHandler(
        IAttendanceRepository attRepo,
        IEmployeeRepository   empRepo)
    {
        _attRepo = attRepo;
        _empRepo = empRepo;
    }

    public async Task<IReadOnlyList<ShiftViolationDto>> Handle(
        GetShiftViolationReportQuery query, CancellationToken ct)
    {
        var records      = await _attRepo.GetByDateRangeAsync(query.From, query.To, ct).ConfigureAwait(false);
        var allEmployees = await _empRepo.GetAllActiveAsync(ct).ConfigureAwait(false);
        var empMap       = allEmployees.ToDictionary(e => e.Id.Value);

        return records
            .Where(r => !string.IsNullOrWhiteSpace(r.Notes))
            .Select(r =>
            {
                empMap.TryGetValue(r.EmployeeId.Value, out var emp);
                return new ShiftViolationDto(
                    AttendanceRecordId: r.Id,
                    EmployeeId:         r.EmployeeId.Value,
                    EmployeeCode:       emp?.EmployeeCode ?? "",
                    EmployeeName:       emp?.FullName ?? "Unknown",
                    WorkDate:           r.WorkDate.ToString("dd MMM yyyy"),
                    Notes:              r.Notes!,
                    CheckInTimeIst:     r.CheckInTime.HasValue ? IstClock.FormatIstTime(r.CheckInTime.Value) : null,
                    CheckOutTimeIst:    r.CheckOutTime.HasValue ? IstClock.FormatIstTime(r.CheckOutTime.Value) : null);
            }).ToList().AsReadOnly();
    }
}

