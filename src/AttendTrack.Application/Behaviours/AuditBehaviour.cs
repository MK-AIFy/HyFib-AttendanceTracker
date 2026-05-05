using System.Text.Json;
using AttendTrack.Application.Common;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Behaviours;

/// <summary>
/// Pipeline behaviour that writes an AuditLog entry for mutation commands
/// that implement <see cref="IAuditableRequest"/> (Gap 6 DPDP).
/// Request JSON = OldValues (command intent); response JSON = NewValues (result).
/// </summary>
public sealed class AuditBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditRepository    _auditRepo;
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditBehaviour<TRequest, TResponse>> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented       = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AuditBehaviour(
        IAuditRepository                              auditRepo,
        IUnitOfWork                                   uow,
        ICurrentUserService                           currentUser,
        ILogger<AuditBehaviour<TRequest, TResponse>> logger)
    {
        _auditRepo   = auditRepo;
        _uow         = uow;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        // Only audit requests that opt-in via the marker interface
        if (request is not IAuditableRequest)
            return await next(ct).ConfigureAwait(false);

        var requestName = typeof(TRequest).Name;
        var oldValues   = JsonSerializer.Serialize<object>(request, _jsonOpts);

        var response = await next(ct).ConfigureAwait(false);

        try
        {
            var newValues = JsonSerializer.Serialize<object>(response!, _jsonOpts);
            var auditLog  = AuditLog.Create(
                action:    requestName,
                actorId:   _currentUser.UserId,
                actorRole: _currentUser.Role,
                oldValues: oldValues,
                newValues: newValues,
                ipAddress: _currentUser.IpAddress);

            await _auditRepo.AddAsync(auditLog, ct).ConfigureAwait(false);
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit failures must never break the main operation
            _logger.LogWarning(ex, "Audit write failed for {Request} — continuing", requestName);
        }

        return response;
    }
}

