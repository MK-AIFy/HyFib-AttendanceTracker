using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AttendTrack.Integration.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AttendTrack.Integration.Tests;

/// <summary>
/// Integration tests for POST /api/hikvision/events.
/// Uses a real PostgreSQL container via TestContainers.
/// <see cref="AttendTrackWebApplicationFactory"/> boots the full ASP.NET Core pipeline.
/// </summary>
[Collection("HikvisionWebhook")]
public sealed class HikvisionWebhookTests : IClassFixture<AttendTrackWebApplicationFactory>
{
    private const string WebhookPath = "/api/hikvision/events";

    private readonly AttendTrackWebApplicationFactory _factory;

    public HikvisionWebhookTests(AttendTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a check-in XML payload with a unique employee code and timestamp
    /// to avoid unique-constraint collisions between tests.
    /// </summary>
    private static StringContent XmlContent(string xml) =>
        new(xml, Encoding.UTF8, "application/xml");

    private static string CheckInXml(
        string employeeCode = AttendTrackWebApplicationFactory.TestEmployeeCode,
        string dateTime     = "2025-06-01T09:00:00+05:30",
        string macAddress   = "AA:BB:CC:DD:EE:FF") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <EventNotificationAlert version="2.0">
          <macAddress>{macAddress}</macAddress>
          <dateTime>{dateTime}</dateTime>
          <eventType>AccessControllerEvent</eventType>
          <AccessControllerEvent>
            <employeeNoString>{employeeCode}</employeeNoString>
            <name>John Doe</name>
            <currentVerifyMode>faceAndFp</currentVerifyMode>
            <attendanceStatus>checkIn</attendanceStatus>
          </AccessControllerEvent>
        </EventNotificationAlert>
        """;

    // ── Test 1: Registered device IP → 200 ───────────────────────────────────

    [Fact]
    public async Task Post_FromRegisteredDeviceIp_Returns200()
    {
        // Use a unique timestamp so this event doesn't collide with other tests
        using var client = _factory.CreateDeviceClient();
        var xml = CheckInXml(dateTime: "2025-06-01T08:00:00+05:30");

        var response = await client.PostAsync(WebhookPath, XmlContent(xml));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Test 2: Unknown IP → 403 ──────────────────────────────────────────────

    [Fact]
    public async Task Post_FromUnknownIp_Returns403()
    {
        // CreateClient() does NOT add X-Test-RemoteIp → middleware sees null RemoteIpAddress → 403
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                AttendTrackWebApplicationFactory.BasicAuthToken());

        var response = await client.PostAsync(WebhookPath,
            XmlContent(CheckInXml(dateTime: "2025-06-02T09:00:00+05:30")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Test 3: Multipart XML + JPEG → 200 + event archived ──────────────────

    [Fact]
    public async Task Post_MultipartXmlAndJpeg_EventLoggedAndProcessed()
    {
        // Unique employee code + time to isolate this test's event log entry
        const string uniqueCode = "EMP-MULTI-001";
        const string uniqueTime = "2025-06-03T09:30:00+05:30";

        using var client = _factory.CreateDeviceClient();

        var multipart = new MultipartFormDataContent();

        // Part 1: XML event payload
        var xmlPart = new StringContent(
            CheckInXml(uniqueCode, uniqueTime), Encoding.UTF8, "application/xml");
        multipart.Add(xmlPart, "xmldata", "event.xml");

        // Part 2: Minimal 1x1 JPEG (valid JFIF header)
        var jpegBytes = new byte[]
        {
            0xFF,0xD8,0xFF,0xE0, 0x00,0x10, 0x4A,0x46,0x49,0x46, 0x00,
            0x01,0x01,0x00, 0x00,0x01, 0x00,0x01, 0x00,0x00,
            0xFF,0xD9
        };
        var jpegPart = new ByteArrayContent(jpegBytes);
        jpegPart.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        multipart.Add(jpegPart, "faceImage", "capture.jpg");

        var response = await client.PostAsync(WebhookPath, multipart);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the event was archived in the DB
        var count = await _factory.CountEventLogsAsync(uniqueCode);
        count.Should().Be(1, "multipart XML+JPEG event must be archived");
    }

    // ── Test 4: Duplicate event → 200 idempotent (event archived only once) ──

    [Fact]
    public async Task Post_DuplicateEvent_Returns200Idempotent_NoSecondRecord()
    {
        // Unique code + timestamp to avoid collision with other tests
        const string uniqueCode = "EMP-DUP-001";
        const string uniqueTime = "2025-06-04T09:00:00+05:30";
        var xml = XmlContent(CheckInXml(uniqueCode, uniqueTime));

        using var client = _factory.CreateDeviceClient();

        // First POST — creates the event log entry
        var first = await client.PostAsync(WebhookPath, xml);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second POST with IDENTICAL payload — must also return 200 (device retries on non-200)
        xml = XmlContent(CheckInXml(uniqueCode, uniqueTime));   // re-create (StringContent is consumed)
        var second = await client.PostAsync(WebhookPath, xml);
        second.StatusCode.Should().Be(HttpStatusCode.OK,
            "device must always receive 200; duplicate events must be idempotent");

        // Exactly ONE event log entry despite two posts
        var count = await _factory.CountEventLogsAsync(uniqueCode);
        count.Should().Be(1, "duplicate event must NOT create a second event log entry");
    }

    // ── Test 5: Invalid XML → 200, absorbed ────────────────────────────────────

    [Fact]
    public async Task Post_InvalidXml_Returns200Absorbed()
    {
        using var client = _factory.CreateDeviceClient();

        var malformed = new StringContent(
            "<not-valid xml <<<", Encoding.UTF8, "application/xml");

        var response = await client.PostAsync(WebhookPath, malformed);

        // By design (ProcessHikvisionEventHandler.Handle, catch around
        // HikvisionEventParser.Parse), malformed payloads are logged and absorbed
        // rather than rejected — the device retries on any non-200, so a genuinely
        // malformed payload must still get 200 or the device would retry forever.
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "malformed XML is absorbed, not rejected, so the device doesn't retry forever");
    }
}

