using AttendTrack.Application.Commands.Hikvision;
using AttendTrack.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AttendTrack.Web.Controllers;

/// <summary>
/// Admin endpoints for Hikvision device management and employee enrollment.
/// All endpoints require Admin or SuperAdmin role.
/// </summary>
[ApiController, Route("api/admin/hikvision")]
// AuthenticationSchemes must be listed explicitly alongside Roles: specifying Roles on
// [Authorize] makes ASP.NET Core build an ad-hoc policy instead of falling back to
// Program.cs's DefaultPolicy (which is the only place the Cookie+JWT scheme combination
// is configured) — so without this, a validly-authenticated JWT bearer client (the whole
// point of IJwtTokenService, per its own doc comment: "API clients, Hikvision admin
// tools, mobile, integrations") can never satisfy the role check here, only cookie-
// authenticated Blazor UI sessions can. Confirmed empirically: a JWT with role=SuperAdmin
// gets 401 on this endpoint but 200 on GET /auth/me (bare [Authorize], no Roles).
[Authorize(Roles = "SuperAdmin,Admin",
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class HikvisionAdminController : ControllerBase
{
    private readonly IMediator                  _mediator;
    private readonly IHikvisionDeviceRepository _deviceRepo;

    public HikvisionAdminController(
        IMediator                  mediator,
        IHikvisionDeviceRepository deviceRepo)
    {
        _mediator   = mediator;
        _deviceRepo = deviceRepo;
    }

    /// <summary>List all registered Hikvision devices.</summary>
    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(CancellationToken ct)
    {
        var devices = await _deviceRepo.GetActiveDevicesAsync(ct);
        return Ok(devices.Select(d => new
        {
            d.Id, d.DeviceName, d.Model, d.SerialNumber, d.IpAddress,
            d.Location, d.FirmwareVersion, d.IsActive,
            d.EnrolledEmployeeCount, d.LastEventReceivedAt, d.LastPollAt
        }));
    }

    /// <summary>Register a new Hikvision device.</summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterHikvisionDeviceCommand cmd, CancellationToken ct)
    {
        var id = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetDevice), new { deviceId = id }, new { id });
    }

    /// <summary>Get single device details.</summary>
    [HttpGet("devices/{deviceId:guid}")]
    public async Task<IActionResult> GetDevice(Guid deviceId, CancellationToken ct)
    {
        var device = await _deviceRepo.GetByIdAsync(deviceId, ct);
        if (device is null) return NotFound();

        // Same projection as GetDevices — never serialize AdminPasswordHash or
        // AdminPasswordProtected. The latter is decryptable (IDataProtector), so
        // returning it here would hand out the device's real admin password.
        return Ok(new
        {
            device.Id, device.DeviceName, device.Model, device.SerialNumber, device.IpAddress,
            device.Location, device.FirmwareVersion, device.IsActive,
            device.EnrolledEmployeeCount, device.LastEventReceivedAt, device.LastPollAt
        });
    }

    /// <summary>
    /// Enroll an employee on a Hikvision device.
    /// Accepts multipart/form-data: facePhoto (required JPEG).
    /// </summary>
    [HttpPost("devices/{deviceId:guid}/enroll/{employeeId:guid}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> EnrollEmployee(
        Guid              deviceId,
        Guid              employeeId,
        IFormFile         facePhoto,
        CancellationToken ct)
    {
        if (facePhoto is null || facePhoto.Length == 0)
            return BadRequest(new { error = "facePhoto is required" });

        using var ms = new System.IO.MemoryStream();
        await facePhoto.CopyToAsync(ms, ct);

        var result = await _mediator.Send(
            new EnrollEmployeeToDeviceCommand(employeeId, deviceId, ms.ToArray()), ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
