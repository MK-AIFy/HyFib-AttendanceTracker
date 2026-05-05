using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Behaviours;

/// <summary>
/// Pipeline behaviour that logs request start, completion, and elapsed milliseconds via Serilog.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        var name = typeof(TRequest).Name;
        var sw   = Stopwatch.StartNew();

        _logger.LogInformation("AttendTrack → {Request}", name);
        try
        {
            var response = await next(ct).ConfigureAwait(false);
            sw.Stop();
            _logger.LogInformation("AttendTrack ← {Request} completed in {ElapsedMs} ms",
                name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "AttendTrack ✗ {Request} failed after {ElapsedMs} ms",
                name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

