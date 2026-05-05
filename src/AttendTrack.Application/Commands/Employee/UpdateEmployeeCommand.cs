using AttendTrack.Application.Common;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Employee;

/// <summary>Updates name, email, phone of an existing employee.</summary>
public sealed record UpdateEmployeeCommand(
    Guid   EmployeeId,
    string FullName,
    string Email,
    string Phone) : IRequest, IAuditableRequest;

public sealed class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<UpdateEmployeeHandler> _logger;

    public UpdateEmployeeHandler(
        IEmployeeRepository          empRepo,
        IUnitOfWork                  uow,
        ILogger<UpdateEmployeeHandler> logger)
    {
        _empRepo = empRepo;
        _uow     = uow;
        _logger  = logger;
    }

    public async Task Handle(UpdateEmployeeCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByIdAsync(
            EmployeeId.From(cmd.EmployeeId), ct).ConfigureAwait(false)
            ?? throw new DomainException($"Employee {cmd.EmployeeId} not found.");

        employee.UpdateProfile(cmd.FullName, cmd.Email, cmd.Phone);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Employee updated: {Id}", cmd.EmployeeId);
    }
}
