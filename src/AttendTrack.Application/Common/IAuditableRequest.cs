namespace AttendTrack.Application.Common;

/// <summary>
/// Marker interface — MediatR requests that implement this will be recorded by AuditBehaviour.
/// Apply to all mutation commands (CheckIn, CheckOut, CreateEmployee, etc.).
/// </summary>
public interface IAuditableRequest { }
