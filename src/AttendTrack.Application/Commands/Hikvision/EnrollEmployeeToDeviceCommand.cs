using MediatR;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// Enrolls an employee on a Hikvision device via ISAPI.
/// Sets Employee.IsBiometricEnrolled = true on success.
/// Face/fingerprint templates stay on device only (DPDP compliant).
/// </summary>
public sealed record EnrollEmployeeToDeviceCommand(
    Guid   EmployeeId,
    Guid   DeviceId,
    byte[] FacePhotoBytes) : IRequest<EnrollEmployeeResult>;

public sealed record EnrollEmployeeResult(
    Guid   EmployeeId,
    Guid   DeviceId,
    bool   Success,
    string? ErrorMessage = null);
