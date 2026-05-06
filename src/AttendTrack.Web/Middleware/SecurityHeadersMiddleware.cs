namespace AttendTrack.Web.Middleware;

/// <summary>
/// Adds OWASP-recommended security headers to every response.
/// CSP allows 'unsafe-inline' / 'unsafe-eval' because Blazor Server requires them.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next          = next;
        _isDevelopment = env.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;

            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"]        = "DENY";
            h["Referrer-Policy"]        = "same-origin";
            h["Permissions-Policy"]     = "geolocation=(), microphone=(), camera=()";

            if (!_isDevelopment)
                h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            // Blazor Server requires unsafe-inline for the bootstrap script + unsafe-eval for the .NET runtime.
            // The connect-src 'self' covers SignalR over the same origin.
            h["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "font-src 'self' data: https://cdn.jsdelivr.net; " +
                "img-src 'self' data: blob:; " +
                "connect-src 'self' wss: ws:; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'";

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
