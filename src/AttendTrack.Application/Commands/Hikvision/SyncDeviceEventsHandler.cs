using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Hikvision;

public sealed class SyncDeviceEventsHandler
    : IRequestHandler<SyncDeviceEventsCommand, SyncDeviceEventsResult>
{
    private readonly IHikvisionDeviceRepository _deviceRepo;
    private readonly IHikvisionIsapiService     _isapiService;
    private readonly IMediator                  _mediator;
    private readonly ILogger<SyncDeviceEventsHandler> _logger;

    public SyncDeviceEventsHandler(
        IHikvisionDeviceRepository        deviceRepo,
        IHikvisionIsapiService            isapiService,
        IMediator                         mediator,
        ILogger<SyncDeviceEventsHandler>  logger)
    {
        _deviceRepo   = deviceRepo;
        _isapiService = isapiService;
        _mediator     = mediator;
        _logger       = logger;
    }

    public async Task<SyncDeviceEventsResult> Handle(
        SyncDeviceEventsCommand cmd, CancellationToken ct)
    {
        var device = await _deviceRepo.GetByIdAsync(cmd.DeviceId, ct);
        if (device is null)
            throw new InvalidOperationException($"Device {cmd.DeviceId} not found.");

        var events = await _isapiService.GetRecentEventsAsync(device, cmd.SinceUtc, ct);
        int processed = 0, skipped = 0;

        foreach (var ev in events)
        {
            var exists = await _deviceRepo.EventLogExistsAsync(
                ev.DeviceSerial, ev.DeviceLocalTime, ev.EmployeeNoString, ct);
            if (exists) { skipped++; continue; }

            var epochSec = new DateTimeOffset(ev.EventTimeUtc).ToUnixTimeSeconds();
            var rawXml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <EventNotificationAlert version="2.0">
                  <macAddress>{XmlEscape(ev.DeviceSerial)}</macAddress>
                  <eventType>AccessControllerEvent</eventType>
                  <AccessControllerEvent>
                    <time>{epochSec}</time>
                    <employeeNoString>{XmlEscape(ev.EmployeeNoString)}</employeeNoString>
                    <name>{XmlEscape(ev.EmployeeName)}</name>
                    <attendanceStatus>{XmlEscape(ev.AttendanceStatus)}</attendanceStatus>
                    <currentVerifyMode>{XmlEscape(ev.CurrentVerifyMode)}</currentVerifyMode>
                    <cardNo>{XmlEscape(ev.CardNo ?? "")}</cardNo>
                  </AccessControllerEvent>
                </EventNotificationAlert>
                """;

            await _mediator.Send(new ProcessHikvisionEventCommand(rawXml, null, DateTime.UtcNow), ct);
            processed++;
        }

        _logger.LogInformation(
            "SyncDevice {DeviceId}: {P} processed, {S} skipped", cmd.DeviceId, processed, skipped);

        return new SyncDeviceEventsResult(cmd.DeviceId, processed, skipped);
    }

    private static string XmlEscape(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : System.Security.SecurityElement.Escape(value) ?? string.Empty;
}
