using AttendTrack.Application.Common;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Employee;

/// <summary>Deactivates an employee (soft-disable, not DPDP hard-delete).</summary>
public sealed record DeactivateEmployeeCommand(
    Guid EmployeeId) : IRequest, IAuditableRequest;

public sealed class DeactivateEmployeeHandler : IRequestHandler<DeactivateEmployeeCommand>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<DeactivateEmployeeHandler> _logger;

    public DeactivateEmployeeHandler(
        IEmployeeRepository              empRepo,
        IUnitOfWork                      uow,
        ILogger<DeactivateEmployeeHandler> logger)
    {
        _empRepo = empRepo;
        _uow     = uow;
        _logger  = logger;
    }

    public async Task Handle(DeactivateEmployeeCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByIdAsync(
            EmployeeId.From(cmd.EmployeeId), ct).ConfigureAwait(false)
            ?? throw new DomainException($"Employee {cmd.EmployeeId} not found.");

        employee.Deactivate();
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Employee deactivated: {Id}", cmd.EmployeeId);
    }
}
