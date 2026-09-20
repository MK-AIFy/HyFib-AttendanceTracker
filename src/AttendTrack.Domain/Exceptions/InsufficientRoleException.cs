using AttendTrack.Domain.Enums;

namespace AttendTrack.Domain.Exceptions;

/// <summary>
/// Thrown when the caller's role does not meet the privilege level a command
/// requires — either a fixed minimum (RoleAuthorizationBehaviour, e.g. Hikvision
/// enrollment/sync) or a dynamic one (CreateEmployeeHandler rejecting a caller
/// granting a role more privileged than their own).
/// </summary>
public sealed class InsufficientRoleException : DomainException
{
    public UserRole RequiredRole { get; }

    public InsufficientRoleException(UserRole requiredRole)
        : base($"This action requires the {requiredRole} role or higher.")
        => RequiredRole = requiredRole;

    public InsufficientRoleException(UserRole requiredRole, string message)
        : base(message)
        => RequiredRole = requiredRole;
}
