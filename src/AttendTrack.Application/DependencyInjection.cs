using AttendTrack.Application.Behaviours;
using AttendTrack.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AttendTrack.Application;

/// <summary>
/// Registers all Application layer services:
/// MediatR (handlers + pipeline behaviours), FluentValidation, AutoMapper.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR — scans this assembly for all IRequestHandler<,> implementations
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Pipeline behaviours — ORDER MATTERS.
        // MediatR executes them in registration order (first registered = outermost).
        // Desired runtime flow:
        //   request → Validation → Logging → Audit → Concurrency → Handler
        //   Validation runs first so we never log/audit invalid commands.
        //   Concurrency wraps the handler closest so it catches DbUpdate* and maps to 409.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ConcurrencyBehaviour<,>));

        // FluentValidation — register all validators in this assembly
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // AutoMapper — register all Profile-derived classes in this assembly
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        return services;
    }
}
