namespace AttendTrack.Application.Common;

/// <summary>
/// Marker for Blazor-kiosk EmployeeCode+PIN commands (CheckIn/CheckOut/StartBreak/
/// EndBreak). KioskLockoutBehaviour tracks failed attempts per EmployeeCode in
/// Redis for any request implementing this — independent of which Blazor circuit
/// submitted it, so a browser refresh can't reset or bypass the lockout.
/// </summary>
public interface IKioskPinRequest
{
    string EmployeeCode { get; }
}
