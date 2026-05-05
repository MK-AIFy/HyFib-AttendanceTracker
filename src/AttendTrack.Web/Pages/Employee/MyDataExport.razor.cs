using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Employee;

public sealed partial class MyDataExport : ComponentBase
{
    [Inject] private ISender              Sender      { get; set; } = default!;
    [Inject] private ICurrentUserService  CurrentUser { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;

    private bool   _exporting;
    private string _message = string.Empty;

    private async Task ExportAsync()
    {
        if (CurrentUser.UserId is null) return;
        _exporting = true;
        _message = string.Empty;

        var bytes = await Sender.Send(new ExportEmployeeDataCommand(CurrentUser.UserId.Value));
        var base64 = Convert.ToBase64String(bytes);
        await JS.InvokeVoidAsync("downloadFile",
            $"my-data-{DateTime.UtcNow:yyyyMMddHHmmss}.json", base64, "application/json");

        _exporting = false;
        _message = "Export downloaded. Keep this file secure — it contains your personal data.";
    }
}
