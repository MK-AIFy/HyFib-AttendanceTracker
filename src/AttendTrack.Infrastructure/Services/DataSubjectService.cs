using System.Text.Json;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Domain.ValueObjects;
using AttendTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// [Gap 6] DPDP Act 2023 — Data Subject Services.
/// Supports employee data export and erasure requests.
/// </summary>
public sealed class DataSubjectService : IDataSubjectService
{
    private readonly AttendTrackDbContext          _db;
    private readonly IEmployeeRepository           _employeeRepo;
    private readonly IUnitOfWork                   _uow;
    private readonly ILogger<DataSubjectService>   _logger;

    public DataSubjectService(
        AttendTrackDbContext         db,
        IEmployeeRepository          employeeRepo,
        IUnitOfWork                  uow,
        ILogger<DataSubjectService>  logger)
    {
        _db           = db;
        _employeeRepo = employeeRepo;
        _uow          = uow;
        _logger       = logger;
    }

    /// <summary>
    /// Exports all data held for a data subject as a portable JSON string.
    /// Suitable for DPDP Act Article 11 "right to access" requests.
    /// </summary>
    public async Task<string> ExportDataAsync(Guid employeeId, CancellationToken ct = default)
    {
        var id = EmployeeId.From(employeeId);
        var employee = await _employeeRepo.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Employee {employeeId} not found");

        var attendanceRecords = await _db.AttendanceRecords
            .IgnoreQueryFilters()
            .Include(r => r.Breaks)
            .Where(r => r.EmployeeId == id)
            .OrderBy(r => r.WorkDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var export = new
        {
            ExportedAt          = DateTime.UtcNow,
            Employee            = new
            {
                employee.EmployeeCode,
                employee.FullName,
                employee.Email,
                employee.Phone,
                employee.DepartmentId,
                employee.JoinedAt,
                employee.IsActive,
                employee.Role,
                employee.IsBiometricEnrolled,
                // PIN hash deliberately excluded — not useful to data subject
            },
            AttendanceRecordCount = attendanceRecords.Count,
            AttendanceRecords     = attendanceRecords.Select(r => new
            {
                r.WorkDate,
                r.CheckInTime,
                r.CheckOutTime,
                r.Status,
                r.CheckInSource,
                r.CheckOutSource,
                r.VerifyMode,
                BreakCount  = r.Breaks.Count,
                r.BreakDuration,
            }),
        };

        _logger.LogInformation(
            "DPDP data export: {EmployeeCode} exported {Count} attendance records",
            employee.EmployeeCode, attendanceRecords.Count);

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
    }

    /// <summary>
    /// Soft-deletes the employee so their data is excluded from queries.
    /// Hard deletion is deferred to DataRetentionPurgeService after the 7-year window.
    /// DPDP Act — "right to erasure" (data still retained for legal compliance period).
    /// </summary>
    public async Task DeleteDataAsync(Guid employeeId, CancellationToken ct = default)
    {
        var id = EmployeeId.From(employeeId);
        var employee = await _employeeRepo.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Employee {employeeId} not found");

        employee.SoftDelete();
        _employeeRepo.Update(employee);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogWarning(
            "DPDP erasure request: {EmployeeCode} soft-deleted. Hard delete scheduled after 7 years.",
            employee.EmployeeCode);
    }
}


