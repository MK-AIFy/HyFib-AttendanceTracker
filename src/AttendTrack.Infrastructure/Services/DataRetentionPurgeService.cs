using AttendTrack.Application.Options;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// [Gap 6 / Gap 15] DPDP Act 2023 compliance — data retention purge.
/// Runs once daily at 2 AM IST.
/// <list type="bullet">
///   <item>Hard-deletes employees soft-deleted more than 7 years ago.</item>
///   <item>Purges face-capture JPEG files older than 30 days (biometric data).</item>
/// </list>
/// Uses IServiceScopeFactory for all scoped DB access.
/// </summary>
public sealed class DataRetentionPurgeService : BackgroundService
{
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<DataRetentionPurgeService> _logger;
    private readonly string                            _faceCaptureBasePath;

    private const int PurgeHourIst     = 2;   // 2 AM IST
    private const int RetentionYears   = 7;   // DPDP hard-delete threshold
    private const int FaceCaptureDays  = 30;  // Biometric photo retention

    public DataRetentionPurgeService(
        IServiceScopeFactory                scopeFactory,
        IOptions<HikvisionOptions>          hikvisionOptions,
        ILogger<DataRetentionPurgeService>  logger)
    {
        _scopeFactory        = scopeFactory;
        _logger              = logger;
        _faceCaptureBasePath = hikvisionOptions.Value.FaceCaptureBasePath;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DataRetentionPurgeService started (daily at {:D2}:00 IST)", PurgeHourIst);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowIst      = IstTimeHelper.NowIst;
            var nextRunIst  = new DateTime(
                nowIst.Year, nowIst.Month, nowIst.Day,
                PurgeHourIst, 0, 0, DateTimeKind.Unspecified)
                .AddDays(nowIst.Hour >= PurgeHourIst ? 1 : 0);

            var delay = IstTimeHelper.ToUtc(nextRunIst) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            try
            {
                await RunPurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "DataRetentionPurgeService unexpected error");
            }
        }
    }

    private async Task RunPurgeAsync(CancellationToken ct)
    {
        _logger.LogInformation("DataRetentionPurgeService: starting daily purge run");

        await PurgeExpiredEmployeesAsync(ct);
        PurgeStaleFaceCaptureFiles();

        _logger.LogInformation("DataRetentionPurgeService: purge run complete");
    }

    /// <summary>
    /// Hard-deletes employee rows (and all FK-cascaded data) for employees whose
    /// DeletedAt is older than the DPDP 7-year retention window.
    /// </summary>
    private async Task PurgeExpiredEmployeesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();

        var cutoff = DateTime.UtcNow.AddYears(-RetentionYears);

        // HasQueryFilter(e => e.DeletedAt == null) is active on Employees — must use IgnoreQueryFilters
        var expired = await db.Employees
            .IgnoreQueryFilters()
            .Where(e => e.DeletedAt != null && e.DeletedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (expired.Count == 0) return;

        _logger.LogWarning(
            "DataRetentionPurgeService: hard-deleting {Count} expired employee records (DeletedAt < {Cutoff:yyyy-MM-dd})",
            expired.Count, cutoff);

        db.Employees.RemoveRange(expired);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes JPEG face-capture files from disk that are older than 30 days.
    /// DPDP requirement: biometric data must not be retained beyond 30 days.
    /// </summary>
    private void PurgeStaleFaceCaptureFiles()
    {
        if (!Directory.Exists(_faceCaptureBasePath)) return;

        var cutoff = DateTime.UtcNow.AddDays(-FaceCaptureDays);
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(
            _faceCaptureBasePath, "*.jpg", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete face capture file {File}", file);
            }
        }

        if (deleted > 0)
            _logger.LogInformation(
                "DataRetentionPurgeService: deleted {Count} stale face-capture files", deleted);
    }
}

