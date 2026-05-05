using MediatR;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// Registers a new Hikvision DS-K1T320MFWX device in AttendTrack.
/// Stores BCrypt hash for webhook auth validation.
/// AdminPasswordProtected stores an encrypted version used for ISAPI outbound calls.
/// </summary>
public sealed record RegisterHikvisionDeviceCommand(
    string DeviceName,
    string Model,
    string SerialNumber,
    string IpAddress,
    int    Port,
    string AdminUsername,
    string AdminPassword,             // Plain text — hashed before storage
    string Location,
    string FirmwareVersion) : IRequest<Guid>;
