using AttendTrack.Application.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Attendance;

/// <summary>
/// Starts a break for an employee via Blazor kiosk.
/// Returns the new BreakRecord ID.
/// </summary>
public sealed record StartBreakCommand(
    string    EmployeeCode,
    string    Pin,
    BreakType BreakType) : IRequest<Guid>, IAuditableRequest, IKioskPinRequest;

public sealed class StartBreakHandler : IRequestHandler<StartBreakCommand, Guid>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IAttendanceRepository _attRepo;
    private readonly IPasswordHasher       _hasher;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<StartBreakHandler> _logger;

    public StartBreakHandler(
        IEmployeeRepository        empRepo,
        IAttendanceRepository      attRepo,
        IPasswordHasher            hasher,
        IUnitOfWork                uow,
        ILogger<StartBreakHandler> logger)
    {
        _empRepo = empRepo;
        _attRepo = attRepo;
        _hasher  = hasher;
        _uow     = uow;
        _logger  = logger;
    }

    public async Task<Guid> Handle(StartBreakCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByCodeAsync(cmd.EmployeeCode, ct).ConfigureAwait(false);
        if (employee is null || !employee.IsActive || !_hasher.Verify(cmd.Pin, employee.KioskPin.Value))
            throw new InvalidKioskCredentialsException();

        var today  = IstClock.TodayIst;
        var record = await _attRepo.GetByEmployeeAndDateAsync(employee.Id, today, ct).ConfigureAwait(false)
            ?? throw new NotCheckedInException(employee.Id.Value);

        var breakRecord = record.StartBreak(cmd.BreakType, PunchSource.BlazorKiosk, DateTime.UtcNow);
        _attRepo.Update(record);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Break started: {Code} type={Type}", cmd.EmployeeCode, cmd.BreakType);
        return breakRecord.Id;
    }
}

