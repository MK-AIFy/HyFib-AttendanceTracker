using System.Net;
using System.Text.Json;
using AttendTrack.Domain.Exceptions;
using FluentValidation;

namespace AttendTrack.Web.Middleware;

/// <summary>
/// Catches unhandled exceptions, maps them to HTTP responses with a TraceId,
/// and never leaks stack traces or internal details to clients.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogError(ex, "Exception after response started — cannot intercept");
            throw ex;
        }

        var traceId = context.TraceIdentifier;
        var (status, payload) = ex switch
        {
            ValidationException ve => (HttpStatusCode.BadRequest, (object)new
            {
                error  = "validation_failed",
                errors = ve.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }),
                traceId,
            }),
            AlreadyCheckedInException  => (HttpStatusCode.Conflict, new { error = "already_checked_in",  message = ex.Message, traceId }),
            AlreadyCheckedOutException => (HttpStatusCode.Conflict, new { error = "already_checked_out", message = ex.Message, traceId }),
            ConcurrentCheckInException => (HttpStatusCode.Conflict, new { error = "concurrent_checkin",  message = ex.Message, traceId }),
            ConcurrencyException       => (HttpStatusCode.Conflict, new { error = "concurrency",        message = ex.Message, traceId }),
            NotCheckedInException      => (HttpStatusCode.UnprocessableEntity, new { error = "not_checked_in", message = ex.Message, traceId }),
            UnauthorizedKioskException => (HttpStatusCode.Forbidden, new { error = "unauthorized_kiosk", message = ex.Message, traceId }),
            DomainException            => (HttpStatusCode.BadRequest, new { error = "domain_error", message = ex.Message, traceId }),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, new { error = "unauthorized", traceId }),
            _ => (HttpStatusCode.InternalServerError, new { error = "internal_error", traceId }),
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Unhandled exception {TraceId} on {Path}", traceId, context.Request.Path);
        else
            _logger.LogWarning(ex, "{Status} on {Path} — {TraceId}", status, context.Request.Path, traceId);

        context.Response.StatusCode  = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
