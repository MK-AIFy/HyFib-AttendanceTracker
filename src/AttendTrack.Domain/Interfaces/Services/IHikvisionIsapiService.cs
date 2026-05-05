using AttendTrack.Domain.Entities;
using AttendTrack.Domain.ValueObjects;

namespace AttendTrack.Domain.Interfaces.Services;

/// <summary>
/// Abstraction for Hikvision DS-K1T320MFWX ISAPI HTTP operations.
/// Implementation lives in Infrastructure (Prompt 4) using HttpClient + Digest auth.
///
/// DPDP NOTE: Biometric templates (face/fingerprint) are stored ONLY on the device
/// and never leave it. Only JPEG face captures are transmitted per event.
/// </summary>
public interface IHikvisionIsapiService
{
    /// <summary>
    /// Enrolls an employee on the device via ISAPI:
    /// 1. PUT /ISAPI/AccessControl/UserInfo/SetUp  — creates user
    /// 2. PUT /ISAPI/Intelligent/FDLib/FaceDataRecord — uploads face photo
    /// </summary>
    Task EnrollEmployeeAsync(
        HikvisionDevice device,
        Employee        employee,
        byte[]          facePhotoBytes,
        CancellationToken ct = default);

    /// <summary>
    /// Polling fallback: fetches events since the given UTC timestamp via
    /// POST /ISAPI/AccessControl/AcsEvent.
    /// Called by HikvisionPollingService when webhook gap > 5 minutes.
    /// </summary>
    Task<IReadOnlyList<HikvisionEventParsed>> GetRecentEventsAsync(
        HikvisionDevice device,
        DateTime        since,
        CancellationToken ct = default);

    /// <summary>
    /// Health check: GET /ISAPI/System/deviceInfo.
    /// Returns true if device responds with HTTP 200.
    /// </summary>
    Task<bool> PingDeviceAsync(HikvisionDevice device, CancellationToken ct = default);
}
