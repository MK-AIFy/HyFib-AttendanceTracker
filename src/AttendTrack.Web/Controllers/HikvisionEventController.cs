using AttendTrack.Application.Commands.Hikvision;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AttendTrack.Web.Controllers;

/// <summary>
/// PRIMARY webhook receiver for Hikvision DS-K1T320MFWX events (LAYER 1).
/// Authentication is handled upstream by HikvisionWebhookAuthMiddleware (IP + Basic auth).
/// ALWAYS returns HTTP 200 — device retries on any non-200 response.
/// </summary>
[ApiController, Route("api/hikvision")]
public sealed class HikvisionEventController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<HikvisionEventController> _logger;

    public HikvisionEventController(
        IMediator                            mediator,
        ILogger<HikvisionEventController>    logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    /// <summary>
    /// Receives event push from Hikvision device.
    /// Handles two content types:
    ///   1. multipart/form-data  — XML part + optional JPEG face capture
    ///   2. application/xml      — raw XML body (some firmware versions)
    /// </summary>
    [HttpPost("events")]
    [DisableRequestSizeLimit]         // Multipart with JPEG can be large
    [AllowAnonymous]                  // IP + BCrypt auth done in middleware
    [EnableRateLimiting("webhook")]
    public async Task<IActionResult> ReceiveEvent(CancellationToken ct)
    {
        string? xmlPayload   = null;
        byte[]? faceCapture  = null;

        if (Request.HasFormContentType)
        {
            // Multipart: part 1 = application/xml, part 2 (optional) = image/jpeg
            foreach (var file in Request.Form.Files)
            {
                if (file.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new System.IO.StreamReader(file.OpenReadStream());
                    xmlPayload = await reader.ReadToEndAsync(ct);
                }
                else if (file.ContentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
                      || file.ContentType.Contains("jpg",  StringComparison.OrdinalIgnoreCase))
                {
                    using var ms = new System.IO.MemoryStream();
                    await file.CopyToAsync(ms, ct);
                    faceCapture = ms.ToArray();
                }
            }
        }
        else
        {
            // Raw XML body
            using var reader = new System.IO.StreamReader(Request.Body);
            xmlPayload = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(xmlPayload))
        {
            _logger.LogWarning("Hikvision event received with empty payload");
            return BadRequest(new { error = "Empty event payload" });
        }

        try
        {
            var result = await _mediator.Send(new ProcessHikvisionEventCommand(
                RawXml:           xmlPayload,
                FaceCaptureBytes: faceCapture,
                ReceivedAtUtc:    DateTime.UtcNow), ct);

            // Always return 200 — device retries on any non-200
            return Ok(new { status = "ok", recordId = result.AttendanceRecordId });
        }
        catch (System.Xml.XmlException ex)
        {
            _logger.LogError(ex, "Hikvision event: malformed XML payload — returning 400");
            return BadRequest(new { error = "Invalid XML payload" });
        }
    }

    /// <summary>
    /// Admin-triggered manual sync for a specific device.
    /// Fetches events since the given UTC timestamp via ISAPI polling.
    /// </summary>
    [HttpPost("sync/{deviceId:guid}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> SyncDevice(
        Guid     deviceId,
        DateTime sinceUtc,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SyncDeviceEventsCommand(deviceId, sinceUtc), ct);
        return Ok(result);
    }
}
