using AttendTrack.Application.DTOs;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;

namespace AttendTrack.Application.Queries.Attendance;

/// <summary>Returns per-employee hourly breakdown for the heat-map (Gap 4).</summary>
public sealed record GetHourlyBreakdownQuery(
    DateOnly Date) : IRequest<IReadOnlyList<HourlyBreakdownDto>>;

public sealed class GetHourlyBreakdownHandler
    : IRequestHandler<GetHourlyBreakdownQuery, IReadOnlyList<HourlyBreakdownDto>>
{
    private readonly IHourlySlotRepository _slotRepo;
    private readonly IEmployeeRepository   _empRepo;

    public GetHourlyBreakdownHandler(
        IHourlySlotRepository slotRepo,
        IEmployeeRepository   empRepo)
    {
        _slotRepo = slotRepo;
        _empRepo  = empRepo;
    }

    public async Task<IReadOnlyList<HourlyBreakdownDto>> Handle(
        GetHourlyBreakdownQuery query, CancellationToken ct)
    {
        var slots      = await _slotRepo.GetByDateAsync(query.Date, ct).ConfigureAwait(false);
        var employees  = await _empRepo.GetAllActiveAsync(ct).ConfigureAwait(false);
        var empMap     = employees.ToDictionary(e => e.Id.Value);

        return slots
            .GroupBy(s => s.EmployeeId)
            .Select(g =>
            {
                empMap.TryGetValue(g.Key, out var emp);
                var slotDtos = g.Select(s => new HourlySlotDto(
                    HourSlot:     s.HourSlotNumber,
                    MinutesWorked: s.MinutesWorked,
                    IsBreak:      s.IsBreak,
                    IsOvertime:   s.IsOvertime,
                    HourLabel:    $"{s.HourSlotNumber:D2}:00")).ToList();

                return new HourlyBreakdownDto(
                    EmployeeId:   g.Key,
                    EmployeeCode: emp?.EmployeeCode ?? "",
                    EmployeeName: emp?.FullName ?? "Unknown",
                    WorkDate:     query.Date.ToString("dd MMM yyyy"),
                    Slots:        slotDtos.AsReadOnly());
            }).ToList().AsReadOnly();
    }
}

