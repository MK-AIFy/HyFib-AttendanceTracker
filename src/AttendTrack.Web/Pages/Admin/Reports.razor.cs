using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class Reports : ComponentBase
{
    [Inject] private ISender              Sender        { get; set; } = default!;
    [Inject] private IReportExportService ReportExport  { get; set; } = default!;
    [Inject] private IJSRuntime           JS            { get; set; } = default!;
    [Inject] private ILogger<Reports>     Logger        { get; set; } = default!;

    private IReadOnlyList<DailyReportDto> _reportDays = [];
    private IReadOnlyList<AttendanceDto>  _overtimeRecords = [];
    private bool _loading;
    private bool _exporting;
    private string? _error;

    // Weekly params
    private DateOnly _weekStart = IstTimeHelper.TodayIst.AddDays(-7);

    // Monthly params
    private int _year  = IstTimeHelper.TodayIst.Year;
    private int _month = IstTimeHelper.TodayIst.Month;

    // OT params
    private int _otYear  = IstTimeHelper.TodayIst.Year;
    private int _otMonth = IstTimeHelper.TodayIst.Month;

    private async Task LoadWeeklyAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _reportDays = await Sender.Send(new GetWeeklyReportQuery(_weekStart));
        }
        catch (Exception ex)
        {
            _error = "Failed to load the weekly report. Please try again.";
            Logger.LogError(ex, "Reports LoadWeeklyAsync failed");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadMonthlyAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _reportDays = await Sender.Send(new GetMonthlyReportQuery(_year, _month));
        }
        catch (Exception ex)
        {
            _error = "Failed to load the monthly report. Please try again.";
            Logger.LogError(ex, "Reports LoadMonthlyAsync failed");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadOvertimeAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            var from = new DateOnly(_otYear, _otMonth, 1);
            var to   = from.AddMonths(1).AddDays(-1);
            _overtimeRecords = await Sender.Send(new GetOvertimeReportQuery(from, to));
        }
        catch (Exception ex)
        {
            _error = "Failed to load the overtime report. Please try again.";
            Logger.LogError(ex, "Reports LoadOvertimeAsync failed");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ExportMonthlyPdfAsync()
    {
        if (_reportDays.Count == 0) return;
        _exporting = true;
        try
        {
            var pdf  = ReportExport.GenerateMonthlyPdf(_year, _month, _reportDays);
            var b64  = Convert.ToBase64String(pdf);
            var name = $"AttendTrack_Monthly_{_year}_{_month:D2}.pdf";
            await JS.InvokeVoidAsync("downloadFile", name, b64, "application/pdf");
        }
        finally { _exporting = false; }
    }

    private async Task ExportWeeklyCsvAsync()
    {
        if (_reportDays.Count == 0) return;
        _exporting = true;
        try
        {
            var csv  = ReportExport.GenerateWeeklyCsv(_weekStart, _reportDays);
            var b64  = Convert.ToBase64String(csv);
            var name = $"AttendTrack_Weekly_{_weekStart:yyyy-MM-dd}.csv";
            await JS.InvokeVoidAsync("downloadFile", name, b64, "text/csv");
        }
        finally { _exporting = false; }
    }
}
