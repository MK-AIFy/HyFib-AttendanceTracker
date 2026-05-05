using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Behaviours;

/// <summary>
/// Pipeline behaviour that runs FluentValidation validators before the handler.
/// Throws <see cref="ValidationException"/> on the first failure set (Gap validation).
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehaviour<TRequest, TResponse>> _logger;

    public ValidationBehaviour(
        IEnumerable<IValidator<TRequest>>                    validators,
        ILogger<ValidationBehaviour<TRequest, TResponse>>   logger)
    {
        _validators = validators;
        _logger     = logger;
    }

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        if (!_validators.Any()) return await next(ct).ConfigureAwait(false);

        var context  = new ValidationContext<TRequest>(request);
        var results  = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct))).ConfigureAwait(false);

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            _logger.LogWarning("Validation failed for {Request}: {Errors}",
                typeof(TRequest).Name,
                string.Join("; ", failures.Select(f => f.ErrorMessage)));
            throw new ValidationException(failures);
        }

        return await next(ct).ConfigureAwait(false);
    }
}

