using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Hikvision;

public sealed class RegisterHikvisionDeviceHandler
    : IRequestHandler<RegisterHikvisionDeviceCommand, Guid>
{
    private readonly IHikvisionDeviceRepository            _deviceRepo;
    private readonly IUnitOfWork                           _uow;
    private readonly IPasswordHasher                       _passwordHasher;
    private readonly ILogger<RegisterHikvisionDeviceHandler> _logger;

    // ProtectPassword is a service-layer concern — injected via factory func
    private readonly Func<string, string> _protectPassword;

    public RegisterHikvisionDeviceHandler(
        IHikvisionDeviceRepository               deviceRepo,
        IUnitOfWork                              uow,
        IPasswordHasher                          passwordHasher,
        Func<string, string>                     protectPassword,
        ILogger<RegisterHikvisionDeviceHandler>  logger)
    {
        _deviceRepo      = deviceRepo;
        _uow             = uow;
        _passwordHasher  = passwordHasher;
        _protectPassword = protectPassword;
        _logger          = logger;
    }

    public async Task<Guid> Handle(
        RegisterHikvisionDeviceCommand cmd, CancellationToken ct)
    {
        var id = Guid.NewGuid();

        // BCrypt hash — stored for webhook validation (one-way)
        var hash = _passwordHasher.Hash(cmd.AdminPassword);

        // Data-Protection encrypted — used for outbound ISAPI calls (reversible)
        var protectedPwd = _protectPassword(cmd.AdminPassword);

        var device = HikvisionDevice.Register(
            id:                     id,
            deviceName:             cmd.DeviceName,
            model:                  cmd.Model,
            serialNumber:           cmd.SerialNumber,
            ipAddress:              cmd.IpAddress,
            port:                   cmd.Port,
            adminUsername:          cmd.AdminUsername,
            adminPasswordHash:      hash,
            adminPasswordProtected: protectedPwd,
            location:               cmd.Location,
            firmwareVersion:        cmd.FirmwareVersion);

        await _deviceRepo.AddAsync(device, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Registered Hikvision device {Serial} at {IP}",
            cmd.SerialNumber, cmd.IpAddress);
        return id;
    }
}
