using System.Security.Claims;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;

namespace AttendTrack.Web.Middleware;

/// <summary>
/// Persists an AuditLog entry for every state-changing HTTP request
/// (POST/PUT/PATCH/DELETE) by an authenticated user (DPDP Gap 6).
/// Skips static files, /_blazor, /health, and /api/hikvision/events
/// (the latter is audited separately as HikvisionEventLog).
/// </summary>
public sealed class RequestAuditMiddleware
{
    private static readonly HashSet<string> SkipPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/_blazor", "/_framework", "/_content", "/health",
        "/api/hikvision/events", "/css", "/js", "/lib", "/favicon",
    };

    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestAuditMiddleware> _logger;

    public RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (!ShouldAudit(context)) return;

        try
        {
            var actorId = TryGetActorId(context.User);
            var role    = context.User.FindFirstValue(ClaimTypes.Role);
            var ip      = context.Connection.RemoteIpAddress?.ToString();
            var action  = $"{context.Request.Method} {context.Request.Path}";

            var entry = AuditLog.Create(
                action:    action,
                actorId:   actorId,
                actorRole: role,
                ipAddress: ip);

            var repo = context.RequestServices.GetRequiredService<IAuditRepository>();
            var uow  = context.RequestServices.GetRequiredService<IUnitOfWork>();

            await repo.AddAsync(entry, context.RequestAborted);
            await uow.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RequestAudit failed for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
    }

    private static bool ShouldAudit(HttpContext ctx)
    {
        if (!AuditedMethods.Contains(ctx.Request.Method)) return false;
        if (ctx.User?.Identity?.IsAuthenticated != true) return false;

        var path = ctx.Request.Path.Value ?? "";
        return !SkipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? TryGetActorId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
