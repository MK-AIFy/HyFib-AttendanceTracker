namespace AttendTrack.Domain.Exceptions;

/// <summary>
/// Thrown when Blazor-kiosk EmployeeCode+PIN authentication fails — either the
/// code doesn't exist or the PIN is wrong. Both cases use this single, generic
/// exception and message so a failed attempt can never be used to tell an
/// attacker whether a given employee code exists (employee-code enumeration).
/// </summary>
public sealed class InvalidKioskCredentialsException : DomainException
{
    public InvalidKioskCredentialsException()
        : base("Invalid employee code or PIN.") { }
}
