using AttendTrack.Application.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Attendance;

/// <summary>Blazor kiosk fallback check-out: EmployeeCode + PIN auth.</summary>
public sealed record CheckOutCommand(
    string  EmployeeCode,
    string  Pin) : IRequest, IAuditableRequest, IKioskPinRequest;

public sealed class CheckOutHandler : IRequestHandler<CheckOutCommand>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IAttendanceRepository _attRepo;
    private readonly IShiftRepository      _shiftRepo;
    private readonly IPasswordHasher       _hasher;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<CheckOutHandler> _logger;

    public CheckOutHandler(
        IEmployeeRepository      empRepo,
        IAttendanceRepository    attRepo,
        IShiftRepository         shiftRepo,
        IPasswordHasher          hasher,
        IUnitOfWork              uow,
        ILogger<CheckOutHandler> logger)
    {
        _empRepo   = empRepo;
        _attRepo   = attRepo;
        _shiftRepo = shiftRepo;
        _hasher    = hasher;
        _uow       = uow;
        _logger    = logger;
    }

    public async Task Handle(CheckOutCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByCodeAsync(cmd.EmployeeCode, ct).ConfigureAwait(false);
        if (employee is null || !employee.IsActive || !_hasher.Verify(cmd.Pin, employee.KioskPin.Value))
            throw new InvalidKioskCredentialsException();

        var today = IstClock.TodayIst;
        var record = await _attRepo.GetByEmployeeAndDateAsync(employee.Id, today, ct).ConfigureAwait(false)
            ?? throw new NotCheckedInException(employee.Id.Value);

        var shift = await _shiftRepo.GetByIdAsync(employee.DefaultShiftId, ct).ConfigureAwait(false)
            ?? throw new DomainException($"Shift {employee.DefaultShiftId} not found");

        record.CheckOut(shift, DateTime.UtcNow, PunchSource.BlazorKiosk, "pin");
        _attRepo.Update(record);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Kiosk CheckOut: {Code} at {Time} IST",
            cmd.EmployeeCode, IstClock.FormatIstTime(record.CheckOutTime!.Value));
    }
}

