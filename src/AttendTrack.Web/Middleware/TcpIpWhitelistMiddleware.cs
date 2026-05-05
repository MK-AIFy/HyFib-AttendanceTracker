namespace AttendTrack.Web.Middleware;

public sealed class TcpIpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    public TcpIpWhitelistMiddleware(RequestDelegate next) => _next = next;
    public async System.Threading.Tasks.Task InvokeAsync(Microsoft.AspNetCore.Http.HttpContext context)
        => await _next(context);
}
