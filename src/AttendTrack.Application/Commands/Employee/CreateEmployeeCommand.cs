using AttendTrack.Application.Common;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Employee;

/// <summary>Creates a new employee. IsBiometricEnrolled defaults false.</summary>
public sealed record CreateEmployeeCommand(
    string   EmployeeCode,
    string   FullName,
    string   Email,
    string   Phone,
    string   Pin,               // plain 6-digit PIN — hashed here
    Guid     DepartmentId,
    Guid     DefaultShiftId,
    UserRole Role,
    DateOnly JoinedAt) : IRequest<Guid>, IAuditableRequest;

public sealed class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, Guid>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IPasswordHasher       _hasher;
    private readonly IUnitOfWork           _uow;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<CreateEmployeeHandler> _logger;

    public CreateEmployeeHandler(
        IEmployeeRepository          empRepo,
        IPasswordHasher              hasher,
        IUnitOfWork                  uow,
        ICurrentUserService          currentUser,
        ILogger<CreateEmployeeHandler> logger)
    {
        _empRepo     = empRepo;
        _hasher      = hasher;
        _uow         = uow;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<Guid> Handle(CreateEmployeeCommand cmd, CancellationToken ct)
    {
        // A caller can never grant a role more privileged than their own —
        // without this, any role permitted onto /admin/employees (as low as
        // Manager) could create a brand-new SuperAdmin account and log in as
        // it. UserRole's ordinal is privilege-ordered: SuperAdmin=0 is most
        // privileged, so a *lower* requested ordinal than the caller's own
        // means an escalation attempt.
        if (!Enum.TryParse<UserRole>(_currentUser.Role, out var callerRole) || cmd.Role < callerRole)
        {
            _logger.LogWarning(
                "Blocked role-escalation attempt: caller role {CallerRole} tried to create employee with role {RequestedRole}",
                _currentUser.Role ?? "(none)", cmd.Role);
            throw new InsufficientRoleException(cmd.Role,
                $"You do not have permission to create an employee with the '{cmd.Role}' role.");
        }

        var existing = await _empRepo.GetByCodeAsync(cmd.EmployeeCode, ct).ConfigureAwait(false);
        if (existing is not null)
            throw new DomainException($"Employee code '{cmd.EmployeeCode}' already in use.");

        var byEmail = await _empRepo.GetByEmailAsync(cmd.Email, ct).ConfigureAwait(false);
        if (byEmail is not null)
            throw new DomainException($"Email '{cmd.Email}' already in use.");

        var pinHash  = PinHash.From(_hasher.Hash(cmd.Pin));
        var employee = Domain.Entities.Employee.Create(
            id:            EmployeeId.New(),
            employeeCode:  cmd.EmployeeCode,
            fullName:      cmd.FullName,
            email:         cmd.Email,
            phone:         cmd.Phone,
            kioskPin:      pinHash,
            departmentId:  cmd.DepartmentId,
            defaultShiftId: cmd.DefaultShiftId,
            role:          cmd.Role,
            joinedAt:      cmd.JoinedAt);

        await _empRepo.AddAsync(employee, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Employee created: {Code} ({Name})", cmd.EmployeeCode, cmd.FullName);
        return employee.Id.Value;
    }
}
