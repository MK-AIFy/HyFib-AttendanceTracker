using AttendTrack.Application.Common;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Shift;

/// <summary>Assigns a shift to an employee (reassigns default shift).</summary>
public sealed record AssignShiftCommand(
    Guid EmployeeId,
    Guid ShiftId) : IRequest, IAuditableRequest;

public sealed class AssignShiftHandler : IRequestHandler<AssignShiftCommand>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IShiftRepository      _shiftRepo;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<AssignShiftHandler> _logger;

    public AssignShiftHandler(
        IEmployeeRepository        empRepo,
        IShiftRepository           shiftRepo,
        IUnitOfWork                uow,
        ILogger<AssignShiftHandler> logger)
    {
        _empRepo   = empRepo;
        _shiftRepo = shiftRepo;
        _uow       = uow;
        _logger    = logger;
    }

    public async Task Handle(AssignShiftCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByIdAsync(
            EmployeeId.From(cmd.EmployeeId), ct).ConfigureAwait(false)
            ?? throw new DomainException($"Employee {cmd.EmployeeId} not found.");

        var shift = await _shiftRepo.GetByIdAsync(cmd.ShiftId, ct).ConfigureAwait(false)
            ?? throw new DomainException($"Shift {cmd.ShiftId} not found.");

        employee.AssignShift(shift.Id);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Shift {ShiftId} assigned to employee {EmpId}",
            cmd.ShiftId, cmd.EmployeeId);
    }
}
