namespace AttendTrack.Domain.Interfaces.Services;

/// <summary>
/// DPDP Act 2023 data subject rights service (Gap 6).
/// Provides export and erasure operations for employee personal data.
/// </summary>
public interface IDataSubjectService
{
    /// <summary>
    /// Exports all personal data held for the employee as a JSON string
    /// (DPDP Act right to access / portability).
    /// </summary>
    Task<string> ExportDataAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes biometric data and anonymises PII for a soft-deleted employee
    /// (DPDP Act right to erasure). Face captures deleted from disk; templates
    /// were never stored — only on the Hikvision device.
    /// </summary>
    Task DeleteDataAsync(Guid employeeId, CancellationToken ct = default);
}
