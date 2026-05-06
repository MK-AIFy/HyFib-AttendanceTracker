using System.Net;
using System.Text;
using System.Xml.Linq;
using AttendTrack.Application.Options;
using AttendTrack.Application.Parsers;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Domain.ValueObjects;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// Hikvision DS-K1T320MFWX ISAPI HTTP client.
/// Uses HttpClientHandler with NetworkCredential for Digest auth (device default).
/// DPDP NOTE: biometric templates never leave the device — only JPEG face captures travel.
/// </summary>
public sealed class HikvisionIsapiService : IHikvisionIsapiService
{
    private readonly IDataProtector _protector;
    private readonly HikvisionOptions _options;
    private readonly ILogger<HikvisionIsapiService> _logger;

    private const string ProtectorPurpose = "Hikvision.ISAPI.Credentials";

    public HikvisionIsapiService(
        IDataProtectionProvider         dataProtectionProvider,
        IOptions<HikvisionOptions>       options,
        ILogger<HikvisionIsapiService>   logger)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _options   = options.Value;
        _logger    = logger;
    }

    // ─── Protect / unprotect helpers used by registration ────────────────────

    public string ProtectPassword(string plainPassword)
        => _protector.Protect(plainPassword);

    private string? UnprotectPassword(string? protectedPassword)
    {
        if (string.IsNullOrEmpty(protectedPassword)) return null;
        try { return _protector.Unprotect(protectedPassword); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect Hikvision ISAPI credential");
            return null;
        }
    }

    // ─── IHikvisionIsapiService ───────────────────────────────────────────────

    /// <summary>
    /// Step 1: PUT /ISAPI/AccessControl/UserInfo/SetUp — creates the user account.
    /// Step 2: PUT /ISAPI/Intelligent/FDLib/FaceDataRecord — uploads face photo.
    /// </summary>
    public async Task EnrollEmployeeAsync(
        HikvisionDevice device, Employee employee, byte[] facePhotoBytes, CancellationToken ct)
    {
        using var client = CreateHttpClient(device);

        // Create / update user on device
        var userXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <UserInfoList version="2.0">
              <UserInfo>
                <employeeNo>{XmlEscape(employee.EmployeeCode)}</employeeNo>
                <name>{XmlEscape(employee.FullName)}</name>
                <userType>normal</userType>
                <Valid>
                  <enable>true</enable>
                  <beginTime>2000-01-01T00:00:00</beginTime>
                  <endTime>2037-12-31T23:59:59</endTime>
                </Valid>
                <doorRight>1</doorRight>
              </UserInfo>
            </UserInfoList>
            """;

        var userResp = await client.PutAsync(
            $"http://{device.IpAddress}:{device.Port}/ISAPI/AccessControl/UserInfo/SetUp",
            new StringContent(userXml, Encoding.UTF8, "application/xml"), ct);

        if (!userResp.IsSuccessStatusCode)
            _logger.LogWarning("ISAPI UserInfo/SetUp returned {Status} for {Code}",
                userResp.StatusCode, employee.EmployeeCode);

        // Upload face photo for face recognition enrollment
        var faceRecord = $"""<FaceDataRecord version="2.0"><employeeNo>{XmlEscape(employee.EmployeeCode)}</employeeNo></FaceDataRecord>""";

        using var formContent = new MultipartFormDataContent();
        formContent.Add(
            new StringContent(faceRecord, Encoding.UTF8, "application/xml"),
            "FaceDataRecord");
        formContent.Add(
            new ByteArrayContent(facePhotoBytes) { Headers = { ContentType = new("image/jpeg") } },
            "face", "face.jpg");

        var faceResp = await client.PutAsync(
            $"http://{device.IpAddress}:{device.Port}/ISAPI/Intelligent/FDLib/FaceDataRecord",
            formContent, ct);

        if (!faceResp.IsSuccessStatusCode)
            _logger.LogWarning("ISAPI FaceDataRecord returned {Status} for {Code}",
                faceResp.StatusCode, employee.EmployeeCode);

        _logger.LogInformation("Enrolled employee {Code} on device {Serial}",
            employee.EmployeeCode, device.SerialNumber);
    }

    /// <summary>Polling fallback: fetch recent events via AcsEvent search API.</summary>
    public async Task<IReadOnlyList<HikvisionEventParsed>> GetRecentEventsAsync(
        HikvisionDevice device, DateTime since, CancellationToken ct)
    {
        using var client = CreateHttpClient(device);

        var searchXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <AcsEventCond version="2.0">
              <searchID>1</searchID>
              <maxResults>200</maxResults>
              <startTime>{since.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}</startTime>
              <endTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</endTime>
            </AcsEventCond>
            """;

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(
                $"http://{device.IpAddress}:{device.Port}/ISAPI/AccessControl/AcsEvent",
                new StringContent(searchXml, Encoding.UTF8, "application/xml"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ISAPI AcsEvent poll failed for {Serial}", device.SerialNumber);
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("ISAPI AcsEvent returned {Status} for {Serial}",
                response.StatusCode, device.SerialNumber);
            return [];
        }

        var xml = await response.Content.ReadAsStringAsync(ct);
        return ParseEventList(xml, device.SerialNumber);
    }

    /// <summary>Health check: GET /ISAPI/System/deviceInfo.</summary>
    public async Task<bool> PingDeviceAsync(HikvisionDevice device, CancellationToken ct)
    {
        using var client = CreateHttpClient(device);
        try
        {
            var resp = await client.GetAsync(
                $"http://{device.IpAddress}:{device.Port}/ISAPI/System/deviceInfo", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ISAPI ping failed for {IP}", device.IpAddress);
            return false;
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private HttpClient CreateHttpClient(HikvisionDevice device)
    {
        var plain = UnprotectPassword(device.AdminPasswordProtected);
        var handler = new HttpClientHandler();

        if (!string.IsNullOrEmpty(plain))
        {
            handler.Credentials = new NetworkCredential(device.AdminUsername, plain);
            handler.PreAuthenticate = false; // Let device send 401 challenge first
        }

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(_options.IsapiTimeoutSeconds)
        };
    }

    private static IReadOnlyList<HikvisionEventParsed> ParseEventList(string xml, string deviceSerial)
    {
        var results = new List<HikvisionEventParsed>();
        try
        {
            var doc = XDocument.Parse(xml);
            var ns  = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            foreach (var info in doc.Descendants(ns + "AcsEvent")
                                    .Concat(doc.Descendants("AcsEvent")))
            {
                try
                {
                    var singleEventXml = WrapAsEventNotification(info, ns, deviceSerial);
                    results.Add(HikvisionEventParser.Parse(singleEventXml));
                }
                catch (Exception)
                {
                    // skip malformed individual entries
                }
            }
        }
        catch (Exception)
        {
            // non-XML response — return empty
        }

        return results;
    }

    /// <summary>
    /// AcsEvent search results use a different schema than the webhook EventNotificationAlert.
    /// Wrap individual AcsEvent elements so HikvisionEventParser can process them.
    /// </summary>
    private static string WrapAsEventNotification(XElement ace, XNamespace ns, string deviceSerial)
    {
        var timeElem    = ace.Element(ns + "time")?.Value ?? ace.Element("time")?.Value ?? "0";
        var empCode     = ace.Element(ns + "employeeNoString")?.Value
                       ?? ace.Element("employeeNoString")?.Value ?? "";
        var empName     = ace.Element(ns + "name")?.Value
                       ?? ace.Element("name")?.Value ?? "";
        var status      = ace.Element(ns + "attendanceStatus")?.Value
                       ?? ace.Element("attendanceStatus")?.Value ?? "";
        var verifyMode  = ace.Element(ns + "currentVerifyMode")?.Value
                       ?? ace.Element("currentVerifyMode")?.Value ?? "";
        var cardNo      = ace.Element(ns + "cardNo")?.Value
                       ?? ace.Element("cardNo")?.Value ?? "";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <EventNotificationAlert version="2.0">
              <macAddress>{XmlEscape(deviceSerial)}</macAddress>
              <eventType>AccessControllerEvent</eventType>
              <AccessControllerEvent>
                <time>{XmlEscape(timeElem)}</time>
                <employeeNoString>{XmlEscape(empCode)}</employeeNoString>
                <name>{XmlEscape(empName)}</name>
                <attendanceStatus>{XmlEscape(status)}</attendanceStatus>
                <currentVerifyMode>{XmlEscape(verifyMode)}</currentVerifyMode>
                <cardNo>{XmlEscape(cardNo)}</cardNo>
              </AccessControllerEvent>
            </EventNotificationAlert>
            """;
    }

    /// <summary>
    /// Escapes user-supplied strings so they cannot inject XML structure into
    /// outbound payloads. Cheap pass — only the five XML special characters.
    /// </summary>
    private static string XmlEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }
}
