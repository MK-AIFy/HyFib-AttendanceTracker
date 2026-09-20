using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Employee;

public sealed partial class MyAttendance : ComponentBase
{
    [Inject] private ISender               Sender      { get; set; } = default!;
    [Inject] private ICurrentUserService   CurrentUser { get; set; } = default!;
    [Inject] private NavigationManager     Nav         { get; set; } = default!;
    [Inject] private ILogger<MyAttendance> Logger      { get; set; } = default!;

    private IReadOnlyList<AttendanceDto> _records = [];
    private DateOnly _from = IstTimeHelper.TodayIst.AddDays(-30);
    private DateOnly _to   = IstTimeHelper.TodayIst;
    private bool _loading;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (CurrentUser.UserId is null)
        {
            Nav.NavigateTo("/login", forceLoad: true);
            return;
        }
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (CurrentUser.UserId is null) return;
        _loading = true;
        _error = null;
        try
        {
            _records = await Sender.Send(
                new GetEmployeeAttendanceHistoryQuery(CurrentUser.UserId.Value, _from, _to));
        }
        catch (Exception ex)
        {
            _error = "Failed to load your attendance. Please try again.";
            Logger.LogError(ex, "MyAttendance LoadDataAsync failed");
        }
        finally
        {
            _loading = false;
        }
    }

    private static string StatusBadge(string status) => status switch
    {
        "Present"        => "bg-success",
        "Absent"         => "bg-danger",
        "MissedCheckout" => "bg-warning text-dark",
        "HalfDay"        => "bg-info text-dark",
        _                => "bg-secondary"
    };
}
