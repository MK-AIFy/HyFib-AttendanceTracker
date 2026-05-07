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
/// minutes worked per IST hour slot (0–23), subtracting any break overlap, and upserts
/// HourlySlot rows. Produces the data required by the HourlyHeatMap admin page.
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

        var todayIst = IstTimeHelper.TodayIst;
        var nowUtc   = DateTime.UtcNow;

        // Eager-load breaks so ComputeHourlySlots can subtract their overlap per hour.
        var openRecords = await db.AttendanceRecords
            .Include(r => r.Breaks)
            .Where(r => r.WorkDate     == todayIst
                     && r.CheckInTime  != null
                     && r.CheckOutTime == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var record in openRecords)
        {
            var breaks = record.Breaks
                .Select(b => (b.StartTime, b.EndTime))
                .ToList();

            var slots = ComputeHourlySlots(
                attendanceRecordId: record.Id,
                employeeId:         record.EmployeeId.Value,
                checkInUtc:         record.CheckInTime!.Value,
                checkOutUtc:        record.CheckOutTime,
                breaks:             breaks,
                nowUtc:             nowUtc);

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
    /// Pure function: given an attendance window and zero-or-more break windows (both in UTC),
    /// produces one <see cref="HourlySlot"/> per IST hour the employee was on the clock.
    /// Per-hour break overlap is subtracted from <c>MinutesWorked</c>; <c>IsBreak</c> is set
    /// when breaks consumed at least half of the hour's window.
    /// </summary>
    /// <param name="checkOutUtc">Null when the employee is still on the clock — falls back to <paramref name="nowUtc"/>.</param>
    internal static List<HourlySlot> ComputeHourlySlots(
        Guid attendanceRecordId,
        Guid employeeId,
        DateTime checkInUtc,
        DateTime? checkOutUtc,
        IReadOnlyList<(DateTime StartUtc, DateTime? EndUtc)> breaks,
        DateTime nowUtc)
    {
        var checkInIst  = IstTimeHelper.ToIstDateTime(checkInUtc);
        var checkOutIst = IstTimeHelper.ToIstDateTime(checkOutUtc ?? nowUtc);

        // Convert breaks to IST once. Open breaks are clamped to nowUtc.
        var breakIstWindows = breaks
            .Select(b => (
                Start: IstTimeHelper.ToIstDateTime(b.StartUtc),
                End:   IstTimeHelper.ToIstDateTime(b.EndUtc ?? nowUtc)))
            .Where(b => b.End > b.Start)
            .ToList();

        var slots = new List<HourlySlot>();
        var current = new DateTime(
            checkInIst.Year, checkInIst.Month, checkInIst.Day,
            checkInIst.Hour, 0, 0, DateTimeKind.Unspecified);

        while (current < checkOutIst)
        {
            var slotEnd = current.AddHours(1);
            var start   = current < checkInIst  ? checkInIst  : current;
            var end     = slotEnd > checkOutIst ? checkOutIst : slotEnd;

            var rawMinutes = (int)(end - start).TotalMinutes;
            if (rawMinutes > 0)
            {
                var breakMinutes = breakIstWindows.Sum(b => OverlapMinutes(start, end, b.Start, b.End));
                var workedMinutes = Math.Max(0, rawMinutes - breakMinutes);
                var isBreak       = breakMinutes > 0 && breakMinutes * 2 >= rawMinutes;

                slots.Add(HourlySlot.Create(
                    attendanceRecordId: attendanceRecordId,
                    employeeId:         employeeId,
                    workDate:           DateOnly.FromDateTime(current),
                    hourSlot:           current.Hour,
                    minutesWorked:      workedMinutes,
                    isBreak:            isBreak));
            }

            current = slotEnd;
        }

        return slots;
    }

    private static int OverlapMinutes(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        var start = aStart > bStart ? aStart : bStart;
        var end   = aEnd   < bEnd   ? aEnd   : bEnd;
        var minutes = (int)(end - start).TotalMinutes;
        return minutes > 0 ? minutes : 0;
    }
}
