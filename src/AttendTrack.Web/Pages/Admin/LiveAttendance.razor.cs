using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class LiveAttendance : ComponentBase, IAsyncDisposable
{
    [Inject] private ISender Sender { get; set; } = default!;
    [Inject] private AttendanceNotifier Notifier { get; set; } = default!;
    [Inject] private ILogger<LiveAttendance> Logger { get; set; } = default!;

    private IReadOnlyList<AttendanceDto> _records = [];
    private string _searchTerm = string.Empty;
    private bool _loading = true;
    private string? _error;

    private IEnumerable<AttendanceDto> FilteredRecords
        => string.IsNullOrWhiteSpace(_searchTerm)
            ? _records
            : _records.Where(r =>
                r.EmployeeName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                r.EmployeeCode.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        Notifier.OnAttendanceChanged += OnAttendanceChangedHandler;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _records = await Sender.Send(new GetLiveAttendanceQuery());
        }
        catch (Exception ex)
        {
            _error = "Failed to load live attendance. Please try again.";
            Logger.LogError(ex, "LiveAttendance LoadDataAsync failed");
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnSearch(ChangeEventArgs e)
        => _searchTerm = e.Value?.ToString() ?? string.Empty;

    private static string StatusBadgeClass(string status) => status switch
    {
        "Present"        => "bg-success",
        "Absent"         => "bg-danger",
        "MissedCheckout" => "bg-warning text-dark",
        "HalfDay"        => "bg-info text-dark",
        _                => "bg-secondary"
    };

    private void OnAttendanceChangedHandler()
        => InvokeAsync(async () => { await LoadDataAsync(); StateHasChanged(); });

    public async ValueTask DisposeAsync()
    {
        Notifier.OnAttendanceChanged -= OnAttendanceChangedHandler;
        await Task.CompletedTask;
    }
}
