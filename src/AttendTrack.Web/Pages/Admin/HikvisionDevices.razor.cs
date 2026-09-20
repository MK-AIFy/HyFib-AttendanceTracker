using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class HikvisionDevices : ComponentBase
{
    private const long MaxFaceBytes = 5 * 1024 * 1024; // 5 MB

    [Inject] private ISender Sender { get; set; } = default!;
    [Inject] private IHikvisionDeviceRepository DeviceRepo { get; set; } = default!;
    [Inject] private IEmployeeRepository EmployeeRepo { get; set; } = default!;
    [Inject] private ILogger<HikvisionDevices> Logger { get; set; } = default!;

    private IReadOnlyList<HikvisionDevice> _devices = [];
    private Dictionary<string, IReadOnlyList<HikvisionEventLog>> _eventLogs = [];
    private bool _loading = true;

    // Enroll panel state
    private Guid? _enrollPanelDeviceId;
    private string _enrollCode = string.Empty;
    private string _enrollMessage = string.Empty;
    private bool _enrollSuccess;
    private byte[] _enrollFaceBytes = Array.Empty<byte>();
    private string _enrollFaceFilename = string.Empty;

    // Sync feedback
    private string _syncMessage = string.Empty;

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        _loading = true;
        _devices = await DeviceRepo.GetActiveDevicesAsync();

        var logDict = new Dictionary<string, IReadOnlyList<HikvisionEventLog>>();
        foreach (var device in _devices)
        {
            var logs = await DeviceRepo.GetRecentEventLogsAsync(device.SerialNumber, 20);
            logDict[device.SerialNumber] = logs;
        }
        _eventLogs = logDict;
        _loading = false;
    }

    private void ShowEnrollPanel(Guid deviceId)
    {
        _enrollPanelDeviceId = _enrollPanelDeviceId == deviceId ? null : deviceId;
        _enrollMessage = string.Empty;
        _enrollCode = string.Empty;
        _enrollFaceBytes = Array.Empty<byte>();
        _enrollFaceFilename = string.Empty;
    }

    private async Task OnFaceFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null) return;

        if (file.Size > MaxFaceBytes)
        {
            _enrollMessage = $"File too large ({file.Size / 1024} KB). Max 5 MB.";
            _enrollSuccess = false;
            _enrollFaceBytes = Array.Empty<byte>();
            return;
        }

        using var ms = new MemoryStream();
        await using (var stream = file.OpenReadStream(MaxFaceBytes))
        {
            await stream.CopyToAsync(ms);
        }
        _enrollFaceBytes = ms.ToArray();
        _enrollFaceFilename = file.Name;
        _enrollMessage = string.Empty;
    }

    private async Task EnrollEmployeeAsync(Guid deviceId)
    {
        if (string.IsNullOrWhiteSpace(_enrollCode))
        {
            _enrollMessage = "Please enter an employee code.";
            _enrollSuccess = false;
            return;
        }

        var employee = await EmployeeRepo.GetByCodeAsync(_enrollCode);
        if (employee is null)
        {
            _enrollMessage = $"Employee code '{_enrollCode}' not found.";
            _enrollSuccess = false;
            return;
        }

        if (_enrollFaceBytes.Length == 0)
        {
            _enrollMessage = "Please select a face photo before enrolling.";
            _enrollSuccess = false;
            return;
        }

        try
        {
            var result = await Sender.Send(new EnrollEmployeeToDeviceCommand(
                EmployeeId: employee.Id.Value,
                DeviceId: deviceId,
                FacePhotoBytes: _enrollFaceBytes));

            _enrollSuccess = result.Success;
            _enrollMessage = result.Success
                ? "Employee enrolled successfully."
                : $"Enrollment failed: {result.ErrorMessage}";

            if (result.Success)
            {
                _enrollFaceBytes = Array.Empty<byte>();
                _enrollFaceFilename = string.Empty;
                _enrollCode = string.Empty;
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            _enrollSuccess = false;
            _enrollMessage = $"Enrollment failed: {ex.Message}";
            Logger.LogError(ex, "HikvisionDevices EnrollEmployeeAsync failed for device {DeviceId}", deviceId);
        }
    }

    private async Task SyncDeviceAsync(Guid deviceId)
    {
        _syncMessage = "Syncing...";
        try
        {
            var result = await Sender.Send(new SyncDeviceEventsCommand(deviceId, DateTime.UtcNow.AddHours(-1)));
            _syncMessage = $"Sync complete — {result.EventsProcessed} events processed.";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _syncMessage = $"Sync failed: {ex.Message}";
            Logger.LogError(ex, "HikvisionDevices SyncDeviceAsync failed for device {DeviceId}", deviceId);
        }
        StateHasChanged();
        await Task.Delay(3000);
        _syncMessage = string.Empty;
    }

    private static string AttendanceStatusBadge(string status) => status switch
    {
        "checkIn"  => "bg-success",
        "checkOut" => "bg-secondary",
        "breakIn"  => "bg-warning text-dark",
        "breakOut" => "bg-info text-dark",
        _          => "bg-light text-dark"
    };
}
