using AttendTrack.Application.Common;
using AttendTrack.Domain.Enums;
using MediatR;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// Enrolls an employee on a Hikvision device via ISAPI.
/// Sets Employee.IsBiometricEnrolled = true on success.
/// Face/fingerprint templates stay on device only (DPDP compliant).
/// SuperAdmin/Admin only — matches HikvisionAdminController's equivalent
/// [Authorize(Roles="SuperAdmin,Admin")] endpoint (RoleAuthorizationBehaviour).
/// </summary>
public sealed record EnrollEmployeeToDeviceCommand(
    Guid   EmployeeId,
    Guid   DeviceId,
    byte[] FacePhotoBytes) : IRequest<EnrollEmployeeResult>, IRequiresMinimumRole
{
    public UserRole MinimumRole => UserRole.Admin;
}

public sealed record EnrollEmployeeResult(
    Guid   EmployeeId,
    Guid   DeviceId,
    bool   Success,
    string? ErrorMessage = null);
