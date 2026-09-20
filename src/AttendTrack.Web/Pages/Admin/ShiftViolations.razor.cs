using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class ShiftViolations : ComponentBase
{
    [Inject] private ISender Sender { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<ShiftViolations> Logger { get; set; } = default!;

    private IReadOnlyList<ShiftViolationDto> _violations = [];
    private DateOnly _from = IstTimeHelper.TodayIst.AddDays(-7);
    private DateOnly _to   = IstTimeHelper.TodayIst;
    private bool _loading;
    private bool _searched;
    private string? _error;

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        _loading = true;
        _searched = false;
        _error = null;
        try
        {
            _violations = await Sender.Send(new GetShiftViolationReportQuery(_from, _to));
            _searched = true;
        }
        catch (Exception ex)
        {
            _error = "Failed to load shift violations. Please try again.";
            Logger.LogError(ex, "ShiftViolations LoadDataAsync failed");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Employee Code,Employee Name,Check-In IST,Check-Out IST,Notes");
        foreach (var v in _violations)
        {
            sb.AppendLine(
                $"\"{v.WorkDate}\",\"{v.EmployeeCode}\",\"{v.EmployeeName}\"," +
                $"\"{v.CheckInTimeIst ?? ""}\",\"{v.CheckOutTimeIst ?? ""}\",\"{v.Notes}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var base64 = Convert.ToBase64String(bytes);
        await JS.InvokeVoidAsync("downloadFile",
            $"violations_{_from:yyyyMMdd}_{_to:yyyyMMdd}.csv", base64, "text/csv");
    }
}
