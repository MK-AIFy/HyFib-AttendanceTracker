using AttendTrack.Application.DTOs;

namespace AttendTrack.Application.Common;

/// <summary>
/// Generates PDF and CSV exports from pre-queried attendance/event data.
/// PDF: QuestPDF Community. CSV: UTF-8 BOM (Excel-compatible).
/// </summary>
public interface IReportExportService
{
    /// <summary>Monthly PDF report with per-day summaries and per-employee auth method breakdown.</summary>
    byte[] GenerateMonthlyPdf(int year, int month, IReadOnlyList<DailyReportDto> days);

    /// <summary>Weekly attendance CSV with individual records; UTF-8 BOM for Excel.</summary>
    byte[] GenerateWeeklyCsv(DateOnly weekStart, IReadOnlyList<DailyReportDto> days);

    /// <summary>Hikvision device event log CSV for audit/export.</summary>
    byte[] GenerateEventLogCsv(IReadOnlyList<HikvisionEventDto> events);
}
