namespace AttendTrack.Web.Middleware;

public sealed class RequestAuditMiddleware
{
    private readonly RequestDelegate _next;
    public RequestAuditMiddleware(RequestDelegate next) => _next = next;
    public async System.Threading.Tasks.Task InvokeAsync(Microsoft.AspNetCore.Http.HttpContext context)
        => await _next(context);
}
