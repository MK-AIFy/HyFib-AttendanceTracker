using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Hikvision;

public sealed class EnrollEmployeeToDeviceHandler
    : IRequestHandler<EnrollEmployeeToDeviceCommand, EnrollEmployeeResult>
{
    private readonly IHikvisionDeviceRepository _deviceRepo;
    private readonly IEmployeeRepository        _employeeRepo;
    private readonly IHikvisionIsapiService     _isapiService;
    private readonly IUnitOfWork                _uow;
    private readonly ILogger<EnrollEmployeeToDeviceHandler> _logger;

    public EnrollEmployeeToDeviceHandler(
        IHikvisionDeviceRepository              deviceRepo,
        IEmployeeRepository                     employeeRepo,
        IHikvisionIsapiService                  isapiService,
        IUnitOfWork                             uow,
        ILogger<EnrollEmployeeToDeviceHandler>  logger)
    {
        _deviceRepo   = deviceRepo;
        _employeeRepo = employeeRepo;
        _isapiService = isapiService;
        _uow          = uow;
        _logger       = logger;
    }

    public async Task<EnrollEmployeeResult> Handle(
        EnrollEmployeeToDeviceCommand cmd, CancellationToken ct)
    {
        var device   = await _deviceRepo.GetByIdAsync(cmd.DeviceId, ct);
        var employee = await _employeeRepo.GetByIdAsync(
            new AttendTrack.Domain.ValueObjects.EmployeeId(cmd.EmployeeId), ct);

        if (device is null)
            return new EnrollEmployeeResult(cmd.EmployeeId, cmd.DeviceId, false,
                $"Device {cmd.DeviceId} not found");
        if (employee is null)
            return new EnrollEmployeeResult(cmd.EmployeeId, cmd.DeviceId, false,
                $"Employee {cmd.EmployeeId} not found");

        try
        {
            await _isapiService.EnrollEmployeeAsync(device, employee, cmd.FacePhotoBytes, ct);
            employee.MarkBiometricEnrolled(employee.EmployeeCode);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Employee {Code} enrolled on device {Serial}",
                employee.EmployeeCode, device.SerialNumber);
            return new EnrollEmployeeResult(employee.Id.Value, device.Id, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enrollment failed for {Code} on {Serial}",
                employee.EmployeeCode, device.SerialNumber);
            return new EnrollEmployeeResult(employee.Id.Value, device.Id, false, ex.Message);
        }
    }
}
