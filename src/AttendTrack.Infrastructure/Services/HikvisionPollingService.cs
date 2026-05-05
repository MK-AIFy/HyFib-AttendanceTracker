using AttendTrack.Application.Commands.Hikvision;
using AttendTrack.Application.Options;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// LAYER 2 fallback: polls Hikvision devices every 2 minutes if webhook gap detected.
/// If LastEventReceivedAt > WebhookGapMinutes ago, fetches events via ISAPI.
/// Deduplication via EventLogExistsAsync ensures no duplicate attendance records.
/// </summary>
public sealed class HikvisionPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HikvisionPollingService> _logger;
    private readonly HikvisionOptions _options;

    public HikvisionPollingService(
        IServiceScopeFactory                  scopeFactory,
        IOptions<HikvisionOptions>            options,
        ILogger<HikvisionPollingService>      logger)
    {
        _scopeFactory = scopeFactory;
        _options      = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HikvisionPollingService started (interval: {Sec}s)",
            _options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllDevicesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HikvisionPollingService unexpected error");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task PollAllDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var deviceRepo   = scope.ServiceProvider.GetRequiredService<IHikvisionDeviceRepository>();
        var isapiService = scope.ServiceProvider.GetRequiredService<IHikvisionIsapiService>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var devices = await deviceRepo.GetActiveDevicesAsync(ct);
        var gapThreshold = TimeSpan.FromMinutes(_options.WebhookGapMinutes);

        foreach (var device in devices)
        {
            // Only poll when webhook events have been silent longer than the gap threshold
            var lastEvent = device.LastEventReceivedAt ?? DateTime.MinValue;
            if (DateTime.UtcNow - lastEvent < gapThreshold) continue;

            var since = lastEvent == DateTime.MinValue
                ? DateTime.UtcNow.AddMinutes(-_options.WebhookGapMinutes)
                : lastEvent;

            _logger.LogInformation(
                "Polling device {Serial} (last event {When})",
                device.SerialNumber, lastEvent);

            IReadOnlyList<Domain.ValueObjects.HikvisionEventParsed> events;
            try
            {
                events = await isapiService.GetRecentEventsAsync(device, since, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Poll failed for {Serial}", device.SerialNumber);
                continue;
            }

            int processed = 0, skipped = 0;
            foreach (var ev in events)
            {
                var exists = await deviceRepo.EventLogExistsAsync(
                    ev.DeviceSerial, ev.DeviceLocalTime, ev.EmployeeNoString, ct);
                if (exists) { skipped++; continue; }

                await mediator.Send(new ProcessHikvisionEventCommand(
                    RawXml:            BuildRawXmlPlaceholder(ev),
                    FaceCaptureBytes:  null,
                    ReceivedAtUtc:     DateTime.UtcNow), ct);
                processed++;
            }

            await deviceRepo.UpdateLastPollAsync(device.Id, DateTime.UtcNow, ct);

            _logger.LogInformation(
                "Poll complete for {Serial}: {P} processed, {S} skipped",
                device.SerialNumber, processed, skipped);
        }
    }

    /// <summary>
    /// Rebuild minimal XML so ProcessHikvisionEventHandler can parse it.
    /// The handler re-archives the event — DeviceSerial dedup key will prevent duplicates.
    /// </summary>
    private static string BuildRawXmlPlaceholder(Domain.ValueObjects.HikvisionEventParsed ev)
    {
        var epochSec = new DateTimeOffset(ev.EventTimeUtc).ToUnixTimeSeconds();
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <EventNotificationAlert version="2.0">
              <macAddress>{ev.DeviceSerial}</macAddress>
              <eventType>AccessControllerEvent</eventType>
              <AccessControllerEvent>
                <time>{epochSec}</time>
                <employeeNoString>{ev.EmployeeNoString}</employeeNoString>
                <name>{ev.EmployeeName}</name>
                <attendanceStatus>{ev.AttendanceStatus}</attendanceStatus>
                <currentVerifyMode>{ev.CurrentVerifyMode}</currentVerifyMode>
                <cardNo>{ev.CardNo ?? ""}</cardNo>
              </AccessControllerEvent>
            </EventNotificationAlert>
            """;
    }
}
