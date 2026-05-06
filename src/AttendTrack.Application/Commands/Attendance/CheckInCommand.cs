using AttendTrack.Application.Common;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Attendance;

/// <summary>
/// Blazor kiosk fallback check-in: EmployeeCode + 6-digit PIN.
/// PunchSource is always BlazorKiosk.
/// Show banner directing employee to use Hikvision biometric terminal when possible.
/// </summary>
public sealed record CheckInCommand(
    string  EmployeeCode,
    string  Pin,
    string? IpAddress = null) : IRequest<Guid>, IAuditableRequest;

public sealed class CheckInHandler : IRequestHandler<CheckInCommand, Guid>
{
    private readonly IEmployeeRepository   _empRepo;
    private readonly IAttendanceRepository _attRepo;
    private readonly IShiftRepository      _shiftRepo;
    private readonly IPasswordHasher       _hasher;
    private readonly IUnitOfWork           _uow;
    private readonly ILogger<CheckInHandler> _logger;

    public CheckInHandler(
        IEmployeeRepository     empRepo,
        IAttendanceRepository   attRepo,
        IShiftRepository        shiftRepo,
        IPasswordHasher         hasher,
        IUnitOfWork             uow,
        ILogger<CheckInHandler> logger)
    {
        _empRepo   = empRepo;
        _attRepo   = attRepo;
        _shiftRepo = shiftRepo;
        _hasher    = hasher;
        _uow       = uow;
        _logger    = logger;
    }

    public async Task<Guid> Handle(CheckInCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByCodeAsync(cmd.EmployeeCode, ct).ConfigureAwait(false)
            ?? throw new DomainException($"Employee not found: {cmd.EmployeeCode}");

        if (!_hasher.Verify(cmd.Pin, employee.KioskPin.Value))
            throw new UnauthorizedKioskException($"Invalid PIN for {cmd.EmployeeCode}");

        var today = IstClock.TodayIst;
        var existing = await _attRepo.GetByEmployeeAndDateAsync(
            employee.Id, today, ct).ConfigureAwait(false);
        if (existing is not null)
            throw new AlreadyCheckedInException(employee.Id.Value);

        var shift = await _shiftRepo.GetByIdAsync(employee.DefaultShiftId, ct).ConfigureAwait(false)
            ?? throw new DomainException($"Shift {employee.DefaultShiftId} not found");

        var record = AttendanceRecord.CheckIn(
            employeeId:   employee.Id,
            shift:        shift,
            workDate:     today,
            checkInTimeUtc: DateTime.UtcNow,
            source:       PunchSource.BlazorKiosk,
            verifyMode:   "pin");

        await _attRepo.AddAsync(record, ct).ConfigureAwait(false);
        try
        {
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // UNIQUE(employee_id, work_date) violation — a concurrent webhook + kiosk
            // race condition. Surface as the domain exception the controller expects.
            throw new AlreadyCheckedInException(employee.Id.Value);
        }

        _logger.LogInformation("Kiosk CheckIn: {Code} at {Time} IST",
            cmd.EmployeeCode, IstClock.FormatIstTime(record.CheckInTime!.Value));
        return record.Id;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner.Message.Contains("23505", StringComparison.Ordinal)
             || inner.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase))
                return true;
            inner = inner.InnerException;
        }
        return ex.Message.Contains("23505", StringComparison.Ordinal)
            || ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}

