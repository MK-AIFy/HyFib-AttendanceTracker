using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Employee;

public sealed partial class MyDataExport : ComponentBase
{
    [Inject] private ISender              Sender      { get; set; } = default!;
    [Inject] private ICurrentUserService  CurrentUser { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<MyDataExport> Logger      { get; set; } = default!;

    private bool   _exporting;
    private bool   _isError;
    private string _message = string.Empty;

    private async Task ExportAsync()
    {
        if (CurrentUser.UserId is null) return;
        _exporting = true;
        _isError = false;
        _message = string.Empty;

        try
        {
            var bytes = await Sender.Send(new ExportEmployeeDataCommand(CurrentUser.UserId.Value));
            var base64 = Convert.ToBase64String(bytes);
            await JS.InvokeVoidAsync("downloadFile",
                $"my-data-{DateTime.UtcNow:yyyyMMddHHmmss}.json", base64, "application/json");

            _message = "Export downloaded. Keep this file secure — it contains your personal data.";
        }
        catch (Exception ex)
        {
            _isError = true;
            _message = "Failed to export your data. Please try again.";
            Logger.LogError(ex, "MyDataExport ExportAsync failed");
        }
        finally
        {
            _exporting = false;
        }
    }
}
