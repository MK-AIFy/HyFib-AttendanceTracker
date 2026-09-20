using AttendTrack.Application.Common;
using AttendTrack.Application.Interfaces;
using AttendTrack.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Behaviours;

/// <summary>
/// Pipeline behaviour enforcing Redis-backed brute-force lockout on Blazor-kiosk
/// EmployeeCode+PIN commands, mirroring AuthController's admin-login lockout
/// (5 failures → 15-minute lock). Runs for any request implementing
/// <see cref="IKioskPinRequest"/>. Keyed by EmployeeCode (not by Blazor circuit),
/// so the lock can't be reset by refreshing the kiosk browser tab, and it
/// self-expires via Redis TTL instead of requiring admin intervention — unlike
/// the previous KioskTerminal.razor.cs in-memory _failedAttempts counter, which
/// was per-circuit and reset on any page reload.
/// </summary>
public sealed class KioskLockoutBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int    MaxFailedAttempts = 5;
    private const int    LockoutMinutes    = 15;
    private const string LockoutKeyPrefix  = "kiosk:lockout:";
    private const string AttemptsKeyPrefix = "kiosk:attempts:";

    private readonly IDistributedCacheWrapper _cache;
    private readonly ILogger<KioskLockoutBehaviour<TRequest, TResponse>> _logger;

    public KioskLockoutBehaviour(
        IDistributedCacheWrapper                              cache,
        ILogger<KioskLockoutBehaviour<TRequest, TResponse>>   logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        if (request is not IKioskPinRequest kioskRequest ||
            string.IsNullOrWhiteSpace(kioskRequest.EmployeeCode))
        {
            return await next(ct).ConfigureAwait(false);
        }

        var code       = kioskRequest.EmployeeCode.ToUpperInvariant();
        var lockoutKey = LockoutKeyPrefix + code;

        var lockedUntil = await _cache.GetAsync<DateTime?>(lockoutKey, ct).ConfigureAwait(false);
        if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Kiosk request for {Code} rejected — locked until {Until}",
                code, lockedUntil.Value);
            throw new KioskLockedOutException(lockedUntil.Value);
        }

        try
        {
            var response = await next(ct).ConfigureAwait(false);
            await _cache.RemoveAsync(AttemptsKeyPrefix + code, ct).ConfigureAwait(false);
            return response;
        }
        catch (InvalidKioskCredentialsException)
        {
            await RecordFailureAsync(code, ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task RecordFailureAsync(string code, CancellationToken ct)
    {
        var attemptsKey = AttemptsKeyPrefix + code;
        var attempts    = await _cache.GetAsync<int>(attemptsKey, ct).ConfigureAwait(false);
        attempts++;

        await _cache.SetAsync(attemptsKey, attempts, TimeSpan.FromMinutes(LockoutMinutes), ct)
            .ConfigureAwait(false);

        if (attempts >= MaxFailedAttempts)
        {
            var until = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            await _cache.SetAsync(LockoutKeyPrefix + code, (DateTime?)until,
                TimeSpan.FromMinutes(LockoutMinutes), ct).ConfigureAwait(false);
            _logger.LogWarning("Kiosk terminal locked for {Code} until {Until} after {Attempts} failures",
                code, until, attempts);
        }
    }
}
