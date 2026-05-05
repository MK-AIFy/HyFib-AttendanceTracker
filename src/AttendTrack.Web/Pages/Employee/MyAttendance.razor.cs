using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Employee;

public sealed partial class MyAttendance : ComponentBase
{
    [Inject] private ISender               Sender      { get; set; } = default!;
    [Inject] private ICurrentUserService   CurrentUser { get; set; } = default!;

    private IReadOnlyList<AttendanceDto> _records = [];
    private DateOnly _from = IstTimeHelper.TodayIst.AddDays(-30);
    private DateOnly _to   = IstTimeHelper.TodayIst;
    private bool _loading;

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (CurrentUser.UserId is null) return;
        _loading = true;
        _records = await Sender.Send(
            new GetEmployeeAttendanceHistoryQuery(CurrentUser.UserId.Value, _from, _to));
        _loading = false;
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
