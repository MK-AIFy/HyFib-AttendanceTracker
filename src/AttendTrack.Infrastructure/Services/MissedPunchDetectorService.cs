using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// [Gap 1] Runs every 15 minutes. Finds open check-ins (no checkout) that are
/// older than the end of the default work day and force-closes them as MissedCheckout.
/// Prevents "ghost" open attendance records from polluting reports.
/// Uses IServiceScopeFactory for all scoped DB access.
/// </summary>
public sealed class MissedPunchDetectorService : BackgroundService
{
    private readonly IServiceScopeFactory                _scopeFactory;
    private readonly ILogger<MissedPunchDetectorService> _logger;
    private const int IntervalMinutes     = 15;
    private const int AutoCheckoutHoursIst = 22; // 10 PM IST — latest possible checkout

    public MissedPunchDetectorService(
        IServiceScopeFactory                 scopeFactory,
        ILogger<MissedPunchDetectorService>  logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MissedPunchDetectorService started (interval: {Min}m, threshold: {Hour}:00 IST)",
            IntervalMinutes, AutoCheckoutHoursIst);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectAndCloseOpenCheckInsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "MissedPunchDetectorService unexpected error");
            }

            await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), stoppingToken);
        }
    }

    private async Task DetectAndCloseOpenCheckInsAsync(CancellationToken ct)
    {
        // Build auto-checkout threshold: today at AutoCheckoutHoursIst in IST → UTC
        var nowIst           = IstTimeHelper.NowIst;
        var autoCheckoutIst  = new DateTime(
            nowIst.Year, nowIst.Month, nowIst.Day,
            AutoCheckoutHoursIst, 0, 0, DateTimeKind.Unspecified);
        var thresholdUtc = IstTimeHelper.ToUtc(autoCheckoutIst);

        // Only run the detector after the auto-checkout time has passed today
        if (DateTime.UtcNow < thresholdUtc) return;

        using var scope          = _scopeFactory.CreateScope();
        var attendanceRepository = scope.ServiceProvider.GetRequiredService<IAttendanceRepository>();
        var uow                  = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var openRecords = await attendanceRepository
            .GetOpenCheckInsOlderThanAsync(thresholdUtc, ct)
            .ConfigureAwait(false);

        if (openRecords.Count == 0) return;

        _logger.LogWarning(
            "MissedPunchDetectorService: auto-closing {Count} open check-ins (threshold {Threshold} UTC)",
            openRecords.Count, thresholdUtc);

        foreach (var record in openRecords)
        {
            record.ForceAutoCheckOut(thresholdUtc);
            attendanceRepository.Update(record);

            _logger.LogInformation(
                "Auto-checkout applied: Employee {EmpId}, WorkDate {Date}",
                record.EmployeeId, record.WorkDate);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

