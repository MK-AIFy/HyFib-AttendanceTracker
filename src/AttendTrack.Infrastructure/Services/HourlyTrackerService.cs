using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Infrastructure.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// [Gap 4] Runs every 60 seconds. For each open attendance record today it computes
/// minutes worked per IST hour slot (0–23) and upserts HourlySlot rows.
/// Produces the data required by the HourlyHeatMap admin page.
/// Uses IServiceScopeFactory for all scoped DB access.
/// </summary>
public sealed class HourlyTrackerService : BackgroundService
{
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<HourlyTrackerService>    _logger;
    private const int IntervalSeconds = 60;

    public HourlyTrackerService(
        IServiceScopeFactory          scopeFactory,
        ILogger<HourlyTrackerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HourlyTrackerService started (interval: {Sec}s)", IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOpenRecordsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HourlyTrackerService unexpected error");
            }

            await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessOpenRecordsAsync(CancellationToken ct)
    {
        using var scope          = _scopeFactory.CreateScope();
        var db                   = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
        var hourlySlotRepository = scope.ServiceProvider.GetRequiredService<IHourlySlotRepository>();
        var uow                  = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IUnitOfWork>();

        var nowUtc = DateTime.UtcNow;

        // Any still-open record needs its hourly slots kept up to date, regardless of
        // which calendar day it started on. Filtering by `WorkDate == today` would drop
        // an open night shift the moment midnight IST passes — its WorkDate stays fixed
        // at check-in time (yesterday), so the record would silently stop getting
        // HourlySlot rows for the rest of the shift. MissedPunchDetectorService bounds
        // how long a record can stay open, so this can't accumulate unboundedly.
        var openRecords = await db.AttendanceRecords
            .Where(r => r.CheckInTime  != null
                     && r.CheckOutTime == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var record in openRecords)
        {
            var slots = ComputeHourlySlots(record, nowUtc);
            foreach (var slot in slots)
                await hourlySlotRepository.UpsertAsync(slot, ct).ConfigureAwait(false);
        }

        if (openRecords.Count > 0)
        {
            await uow.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogDebug("HourlyTrackerService: updated slots for {Count} open records",
                openRecords.Count);
        }
    }

    /// <summary>
    /// Calculates how many minutes the employee worked within each IST hour slot
    /// between check-in and now (or checkout if available).
    /// </summary>
    private static List<HourlySlot> ComputeHourlySlots(
        AttendanceRecord record, DateTime nowUtc)
    {
        var checkInUtc  = record.CheckInTime!.Value;
        var checkOutUtc = record.CheckOutTime ?? nowUtc;

        // Convert to IST for hour-boundary calculations
        var checkInIst  = IstTimeHelper.ToIstDateTime(checkInUtc);
        var checkOutIst = IstTimeHelper.ToIstDateTime(checkOutUtc);

        var slots = new List<HourlySlot>();
        var current = new DateTime(
            checkInIst.Year, checkInIst.Month, checkInIst.Day,
            checkInIst.Hour, 0, 0, DateTimeKind.Unspecified);

        while (current < checkOutIst)
        {
            var slotEnd = current.AddHours(1);
            var start   = current < checkInIst  ? checkInIst  : current;
            var end     = slotEnd > checkOutIst ? checkOutIst : slotEnd;

            var minutes = (int)(end - start).TotalMinutes;
            if (minutes > 0)
            {
                var workDate = DateOnly.FromDateTime(current);
                slots.Add(HourlySlot.Create(
                    attendanceRecordId: record.Id,
                    employeeId:         record.EmployeeId.Value,
                    workDate:           workDate,
                    hourSlot:           current.Hour,
                    minutesWorked:      minutes));
            }

            current = slotEnd;
        }

        return slots;
    }
}

