using MediatR;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// PRIMARY attendance input: processes a raw event received from Hikvision DS-K1T320MFWX.
/// Dispatched by HikvisionEventController on every webhook POST from the device.
/// </summary>
public sealed record ProcessHikvisionEventCommand(
    string   RawXml,
    byte[]?  FaceCaptureBytes,
    DateTime ReceivedAtUtc) : IRequest<HikvisionProcessResult>;

/// <summary>Result returned to the webhook controller (always 200).</summary>
public sealed record HikvisionProcessResult(
    string  EmployeeCode,
    string  AttendanceStatus,
    string  VerifyMode,
    Guid?   AttendanceRecordId,
    bool    IsUnrecognized = false)
{
    public static HikvisionProcessResult Unrecognized(string employeeCode)
        => new(employeeCode, string.Empty, string.Empty, null, IsUnrecognized: true);
}
