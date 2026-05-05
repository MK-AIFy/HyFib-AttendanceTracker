using System.Globalization;
using System.Text;
using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// Generates PDF and CSV reports from pre-queried attendance data.
/// PDF uses QuestPDF Community (free). CSV uses CsvHelper with UTF-8 BOM for Excel.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    // ── Monthly PDF ──────────────────────────────────────────────────────────

    public byte[] GenerateMonthlyPdf(int year, int month, IReadOnlyList<DailyReportDto> days)
    {
        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(s => s.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"AttendTrack — Monthly Attendance Report: {monthLabel}")
                        .Bold().FontSize(14);
                    col.Item().Text($"Generated: {DateTime.Now:dd MMM yyyy hh:mm tt} IST")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(8).Column(contentCol =>
                {
                    // Summary table header
                    contentCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(80);   // Date
                            cols.ConstantColumn(55);   // Present
                            cols.ConstantColumn(55);   // Absent
                            cols.ConstantColumn(55);   // Late
                            cols.ConstantColumn(70);   // Missed Checkout
                            cols.RelativeColumn();     // Employee details
                        });

                        // Header row
                        static void HeaderCell(ITableCellContainer cell, string text) =>
                            cell.Background(Colors.BlueGrey.Darken3)
                                .Padding(4)
                                .Text(text).Bold().FontColor(Colors.White).FontSize(8);

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Date");
                            HeaderCell(h.Cell(), "Present");
                            HeaderCell(h.Cell(), "Absent");
                            HeaderCell(h.Cell(), "Late");
                            HeaderCell(h.Cell(), "Missed C/O");
                            HeaderCell(h.Cell(), "Employee Breakdown");
                        });

                        var rowIndex = 0;
                        foreach (var day in days)
                        {
                            var bg = (rowIndex++ % 2 == 0) ? Colors.White : Colors.Grey.Lighten4;

                            static void DataCell(ITableCellContainer cell, string text, string bg) =>
                                cell.Background(bg).Padding(4).Text(text).FontSize(8);

                            DataCell(table.Cell(), day.Date, bg);
                            DataCell(table.Cell(), day.TotalPresent.ToString(), bg);
                            DataCell(table.Cell(), day.TotalAbsent.ToString(), bg);
                            DataCell(table.Cell(), day.TotalLate.ToString(), bg);
                            DataCell(table.Cell(), day.TotalMissedCheckout.ToString(), bg);

                            // Auth method breakdown for the day
                            var authSummary = BuildAuthSummary(day.Records);
                            DataCell(table.Cell(), authSummary, bg);
                        }
                    });

                    // Per-employee auth method totals for the full month
                    contentCol.Item().PaddingTop(12).Text("Authentication Method Summary (Month)")
                        .Bold().FontSize(10);

                    var allRecords = days.SelectMany(d => d.Records).ToList();
                    var empGroups  = allRecords.GroupBy(r => r.EmployeeCode).OrderBy(g => g.Key);

                    contentCol.Item().PaddingTop(4).Table(empTable =>
                    {
                        empTable.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(80);   // Code
                            cols.RelativeColumn(2);    // Name
                            cols.ConstantColumn(60);   // Days Present
                            cols.ConstantColumn(60);   // Hours
                            cols.RelativeColumn(3);    // Auth breakdown
                        });

                        empTable.Header(h =>
                        {
                            static void H(ITableCellContainer c, string t) =>
                                c.Background(Colors.BlueGrey.Lighten3).Padding(3)
                                 .Text(t).Bold().FontSize(8);
                            H(h.Cell(), "Code");
                            H(h.Cell(), "Name");
                            H(h.Cell(), "Days");
                            H(h.Cell(), "Total Hours");
                            H(h.Cell(), "Auth Breakdown");
                        });

                        var ri = 0;
                        foreach (var grp in empGroups)
                        {
                            var bg    = (ri++ % 2 == 0) ? Colors.White : Colors.Grey.Lighten4;
                            var recs  = grp.ToList();
                            var hours = recs.Sum(r => r.TotalHoursWorked);
                            var auth  = BuildAuthSummaryPerEmployee(recs);

                            static void C(ITableCellContainer c, string t, string bg) =>
                                c.Background(bg).Padding(3).Text(t).FontSize(8);

                            C(empTable.Cell(), grp.Key,                              bg);
                            C(empTable.Cell(), recs.First().EmployeeName,            bg);
                            C(empTable.Cell(), recs.Count.ToString(),                bg);
                            C(empTable.Cell(), $"{hours:F1}h",                       bg);
                            C(empTable.Cell(), auth,                                 bg);
                        }
                    });
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    // ── Weekly CSV ───────────────────────────────────────────────────────────

    public byte[] GenerateWeeklyCsv(DateOnly weekStart, IReadOnlyList<DailyReportDto> days)
    {
        using var ms  = new MemoryStream();
        var       bom = Encoding.UTF8.GetPreamble();
        ms.Write(bom, 0, bom.Length);

        using var writer = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true);
        using var csv    = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        // Write header
        csv.WriteField("Date");
        csv.WriteField("Employee Code");
        csv.WriteField("Employee Name");
        csv.WriteField("Check In (IST)");
        csv.WriteField("Check Out (IST)");
        csv.WriteField("Total Hours");
        csv.WriteField("Break Minutes");
        csv.WriteField("Status");
        csv.WriteField("Auth Method");
        csv.WriteField("Source");
        csv.WriteField("Has Violation");
        csv.WriteField("Notes");
        csv.NextRecord();

        foreach (var day in days)
        {
            foreach (var rec in day.Records)
            {
                csv.WriteField(rec.WorkDate);
                csv.WriteField(rec.EmployeeCode);
                csv.WriteField(rec.EmployeeName);
                csv.WriteField(rec.CheckInTimeIst  ?? "");
                csv.WriteField(rec.CheckOutTimeIst ?? "");
                csv.WriteField(rec.TotalHoursWorked.ToString("F2", CultureInfo.InvariantCulture));
                csv.WriteField(rec.BreakDurationMinutes.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(rec.Status);
                csv.WriteField(rec.VerifyMode ?? "");
                csv.WriteField(rec.CheckInSource);
                csv.WriteField(rec.HasViolation ? "Yes" : "No");
                csv.WriteField(rec.Notes ?? "");
                csv.NextRecord();
            }
        }

        writer.Flush();
        return ms.ToArray();
    }

    // ── Event Log CSV ────────────────────────────────────────────────────────

    public byte[] GenerateEventLogCsv(IReadOnlyList<HikvisionEventDto> events)
    {
        using var ms  = new MemoryStream();
        var       bom = Encoding.UTF8.GetPreamble();
        ms.Write(bom, 0, bom.Length);

        using var writer = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true);
        using var csv    = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        csv.WriteField("Event ID");
        csv.WriteField("Device Serial");
        csv.WriteField("Employee Code");
        csv.WriteField("Employee Name");
        csv.WriteField("Action");
        csv.WriteField("Auth Method");
        csv.WriteField("Card No");
        csv.WriteField("Device Time (IST)");
        csv.WriteField("Processed");
        csv.WriteField("Processing Error");
        csv.WriteField("Attendance Record ID");
        csv.NextRecord();

        foreach (var ev in events)
        {
            csv.WriteField(ev.Id.ToString());
            csv.WriteField(ev.DeviceSerialNumber);
            csv.WriteField(ev.EmployeeCode);
            csv.WriteField(ev.EmployeeName);
            csv.WriteField(ev.AttendanceStatus);
            csv.WriteField(ev.VerifyMode);
            csv.WriteField(ev.CardNo ?? "");
            csv.WriteField(ev.DeviceLocalTime);
            csv.WriteField(ev.IsProcessed ? "Yes" : "No");
            csv.WriteField(ev.ProcessingError ?? "");
            csv.WriteField(ev.AttendanceRecordId?.ToString() ?? "");
            csv.NextRecord();
        }

        writer.Flush();
        return ms.ToArray();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildAuthSummary(IReadOnlyList<AttendanceDto> records)
    {
        if (records.Count == 0) return "";

        var groups = records
            .Where(r => r.VerifyMode != null)
            .GroupBy(r => r.VerifyMode!)
            .Select(g => $"{g.Count()} × {g.Key}")
            .ToList();

        return groups.Count > 0 ? string.Join(", ", groups) : "";
    }

    private static string BuildAuthSummaryPerEmployee(IReadOnlyList<AttendanceDto> records)
    {
        var biometric = records.Count(r =>
            r.VerifyMode is "faceAndFp" or "face" or "fp");
        var card      = records.Count(r => r.VerifyMode == "card");
        var pin       = records.Count(r =>
            r.VerifyMode == "pin" || r.CheckInSource == "BlazorKiosk");

        var parts = new List<string>();
        if (biometric > 0) parts.Add($"{biometric} day(s) via Biometric");
        if (card      > 0) parts.Add($"{card} day(s) via Card");
        if (pin       > 0) parts.Add($"{pin} day(s) via PIN kiosk");

        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }
}
