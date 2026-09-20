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
/// Ends the active break for an employee via Blazor kiosk.
/// Resolves the open break server-side from the employee's attendance record
/// (no client-supplied BreakRecordId so the kiosk UI can stay simple).
/// </summary>
public sealed record EndBreakCommand(
    string EmployeeCode,
    string Pin) : IRequest, IAuditableRequest, IKioskPinRequest;

public sealed class EndBreakHandler : IRequestHandler<EndBreakCommand>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IAttendanceRepository _attRepo;
    private readonly IPasswordHasher       _hasher;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<EndBreakHandler> _logger;

    public EndBreakHandler(
        IEmployeeRepository      empRepo,
        IAttendanceRepository    attRepo,
        IPasswordHasher          hasher,
        IUnitOfWork              uow,
        ILogger<EndBreakHandler> logger)
    {
        _empRepo = empRepo;
        _attRepo = attRepo;
        _hasher  = hasher;
        _uow     = uow;
        _logger  = logger;
    }

    public async Task Handle(EndBreakCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByCodeAsync(cmd.EmployeeCode, ct).ConfigureAwait(false);
        if (employee is null || !employee.IsActive || !_hasher.Verify(cmd.Pin, employee.KioskPin.Value))
            throw new InvalidKioskCredentialsException();

        var today  = IstClock.TodayIst;
        var record = await _attRepo.GetByEmployeeAndDateAsync(employee.Id, today, ct).ConfigureAwait(false)
            ?? throw new NotCheckedInException(employee.Id.Value);

        var activeBreak = record.Breaks.FirstOrDefault(b => b.IsActive)
            ?? throw new NotCheckedInException(employee.Id.Value);

        record.EndBreak(activeBreak.Id, PunchSource.BlazorKiosk);
        _attRepo.Update(record);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Break ended: {Code} breakId={Id}", cmd.EmployeeCode, activeBreak.Id);
    }
}

