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

        if (openRecords.Count == 0) return;

        // Batch-load every existing slot for these records in ONE query, instead of
        // a SELECT-then-write round trip per slot per record per tick (the previous
        // UpsertAsync-per-slot pattern). Also tells ComputeHourlySlots, per record,
        // whether this is its first-ever tick (no rows yet — needs a full backfill
        // from check-in) or a steady-state one (only the current/previous hour can
        // still change) — see its own comment.
        var existingByRecord = await hourlySlotRepository.GetByAttendanceIdsAsync(
            openRecords.Select(r => r.Id), ct).ConfigureAwait(false);

        foreach (var record in openRecords)
        {
            existingByRecord.TryGetValue(record.Id, out var existingSlots);
            var existingByHour = (existingSlots ?? Array.Empty<HourlySlot>())
                .ToDictionary(s => s.HourSlotNumber);

            var computed = ComputeHourlySlots(record, nowUtc, backfillFromCheckIn: existingByHour.Count == 0);
            foreach (var slot in computed)
            {
                if (existingByHour.TryGetValue(slot.HourSlotNumber, out var existing))
                {
                    existing.Update(slot.MinutesWorked, slot.IsBreak, slot.IsOvertime);
                    hourlySlotRepository.Update(existing);
                }
                else
                {
                    await hourlySlotRepository.AddAsync(slot, ct).ConfigureAwait(false);
                }
            }
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("HourlyTrackerService: updated slots for {Count} open records",
            openRecords.Count);
    }

    /// <summary>
    /// Calculates how many minutes the employee worked within each IST hour slot
    /// between check-in and now (or checkout if available).
    ///
    /// <paramref name="backfillFromCheckIn"/> controls how far back to recompute:
    /// true (a record's first-ever tick, no HourlySlot rows exist yet) walks every
    /// hour since check-in, same as before. false (steady state — every earlier
    /// tick already stored a final value for every fully-elapsed hour) caps the
    /// walk to the current hour plus the immediately preceding one (in case a tick
    /// was missed right at an hour boundary, e.g. after a restart), bounding the
    /// work to at most 2 slots/record/tick regardless of how long the shift has
    /// been open — previously this recomputed every elapsed hour on every single
    /// tick, growing unboundedly through the day.
    /// </summary>
    private static List<HourlySlot> ComputeHourlySlots(
        AttendanceRecord record, DateTime nowUtc, bool backfillFromCheckIn)
    {
        var checkInUtc  = record.CheckInTime!.Value;
        var checkOutUtc = record.CheckOutTime ?? nowUtc;

        // Convert to IST for hour-boundary calculations
        var checkInIst  = IstTimeHelper.ToIstDateTime(checkInUtc);
        var checkOutIst = IstTimeHelper.ToIstDateTime(checkOutUtc);

        var checkInHourStart = new DateTime(
            checkInIst.Year, checkInIst.Month, checkInIst.Day,
            checkInIst.Hour, 0, 0, DateTimeKind.Unspecified);

        DateTime current;
        if (backfillFromCheckIn)
        {
            current = checkInHourStart;
        }
        else
        {
            var currentHourStart = new DateTime(
                checkOutIst.Year, checkOutIst.Month, checkOutIst.Day,
                checkOutIst.Hour, 0, 0, DateTimeKind.Unspecified);
            var recomputeFrom = currentHourStart.AddHours(-1);
            current = recomputeFrom > checkInHourStart ? recomputeFrom : checkInHourStart;
        }

        var slots = new List<HourlySlot>();

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

