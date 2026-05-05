using AttendTrack.Application.Common;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

// Type alias: prevents shadowing by the Commands.Shift sub-namespace
using DomainShift = AttendTrack.Domain.Entities.Shift;

namespace AttendTrack.Application.Commands.Shift;

/// <summary>Creates a new shift definition (Admin only).</summary>
public sealed record CreateShiftCommand(
    string   Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int      GracePeriodMinutes       = 15,
    int      OvertimeThresholdMinutes = 30) : IRequest<Guid>, IAuditableRequest;

public sealed class CreateShiftHandler : IRequestHandler<CreateShiftCommand, Guid>
{
    private readonly IShiftRepository   _shiftRepo;
    private readonly IUnitOfWork        _uow;
    private readonly ILogger<CreateShiftHandler> _logger;

    public CreateShiftHandler(
        IShiftRepository        shiftRepo,
        IUnitOfWork             uow,
        ILogger<CreateShiftHandler> logger)
    {
        _shiftRepo = shiftRepo;
        _uow       = uow;
        _logger    = logger;
    }

    public async Task<Guid> Handle(CreateShiftCommand cmd, CancellationToken ct)
    {
        var shift = DomainShift.Create(
            id:                     Guid.NewGuid(),
            name:                   cmd.Name,
            startTime:              cmd.StartTime,
            endTime:                cmd.EndTime,
            gracePeriodMinutes:     cmd.GracePeriodMinutes,
            overtimeThresholdMinutes: cmd.OvertimeThresholdMinutes);

        await _shiftRepo.AddAsync(shift, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Shift created: {Name} ({Id})", cmd.Name, shift.Id);
        return shift.Id;
    }
}
