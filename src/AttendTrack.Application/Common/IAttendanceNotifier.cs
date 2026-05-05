namespace AttendTrack.Application.Common;

/// <summary>
/// Signals that attendance state has changed.
/// Implemented in AttendTrack.Web as a singleton event bus.
/// Injected into MediatR handlers to trigger Blazor component refreshes.
/// </summary>
public interface IAttendanceNotifier
{
    /// <summary>Raised when any attendance record is created or updated.</summary>
    event Action? OnAttendanceChanged;

    /// <summary>Signal that one or more attendance records have changed.</summary>
    void SignalAttendanceChanged();
}
