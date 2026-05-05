using AttendTrack.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Behaviours;

/// <summary>
/// Pipeline behaviour that catches <see cref="ConcurrencyException"/> (thrown by
/// UnitOfWork when EF Core raises DbUpdateConcurrencyException) and re-throws it
/// so the Web layer maps it to HTTP 409 Conflict (Gap 2).
/// </summary>
public sealed class ConcurrencyBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<ConcurrencyBehaviour<TRequest, TResponse>> _logger;

    public ConcurrencyBehaviour(ILogger<ConcurrencyBehaviour<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        try
        {
            return await next(ct).ConfigureAwait(false);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict in {Request} — mapped to 409",
                typeof(TRequest).Name);
            throw;  // GlobalExceptionMiddleware maps ConcurrencyException → HTTP 409
        }
    }
}

