using AttendTrack.Application.Common;

namespace AttendTrack.Web.Services;

/// <summary>
/// Singleton event bus that signals Blazor components to refresh when attendance changes.
/// Registered as IAttendanceNotifier in DI so MediatR handlers can inject it.
/// Blazor pages subscribe to OnAttendanceChanged during OnInitializedAsync and
/// unsubscribe in DisposeAsync — always use InvokeAsync(StateHasChanged) on receipt.
/// </summary>
public sealed class AttendanceNotifier : IAttendanceNotifier
{
    public event Action? OnAttendanceChanged;

    public void SignalAttendanceChanged() => OnAttendanceChanged?.Invoke();
}
