namespace AttendTrack.Domain.Exceptions;

/// <summary>
/// Thrown by UnitOfWork when EF Core detects a DbUpdateConcurrencyException.
/// Mapped to HTTP 409 Conflict by GlobalExceptionMiddleware (Gap 2).
/// </summary>
public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
}
