using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Attendance;

/// <summary>
/// System-initiated auto checkout for employees who forgot to check out (Gap 1).
/// Called by MissedPunchDetectorService every 15 minutes.
/// </summary>
public sealed record AutoCheckOutCommand(
    Guid     AttendanceRecordId,
    DateTime AutoCheckOutTimeUtc) : IRequest;

public sealed class AutoCheckOutHandler : IRequestHandler<AutoCheckOutCommand>
{
    private readonly IAttendanceRepository _attRepo;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<AutoCheckOutHandler> _logger;

    public AutoCheckOutHandler(
        IAttendanceRepository        attRepo,
        IUnitOfWork                  uow,
        ILogger<AutoCheckOutHandler> logger)
    {
        _attRepo = attRepo;
        _uow     = uow;
        _logger  = logger;
    }

    public async Task Handle(AutoCheckOutCommand cmd, CancellationToken ct)
    {
        var record = await _attRepo.GetByIdAsync(cmd.AttendanceRecordId, ct).ConfigureAwait(false);
        if (record is null)
        {
            // [3E] Idempotent: another scheduler instance or manual checkout may have
            // resolved this record already. Log and return rather than throwing.
            _logger.LogInformation(
                "AutoCheckOut skipped: record {Id} not found (already resolved)",
                cmd.AttendanceRecordId);
            return;
        }

        if (record.CheckOutTime is not null)
        {
            _logger.LogInformation(
                "AutoCheckOut skipped: record {Id} already checked out at {Time}",
                cmd.AttendanceRecordId, record.CheckOutTime);
            return;
        }

        record.ForceAutoCheckOut(cmd.AutoCheckOutTimeUtc);
        _attRepo.Update(record);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Auto-checkout applied to record {Id} at {Time} UTC",
            cmd.AttendanceRecordId, cmd.AutoCheckOutTimeUtc);
    }
}

