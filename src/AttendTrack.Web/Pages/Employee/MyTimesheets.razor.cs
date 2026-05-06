using AttendTrack.Application.Common;
using AttendTrack.Application.DTOs;
using AttendTrack.Application.Queries.Attendance;
using AttendTrack.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Employee;

public sealed partial class MyTimesheets : ComponentBase
{
    [Inject] private ISender              Sender      { get; set; } = default!;
    [Inject] private ICurrentUserService  CurrentUser { get; set; } = default!;
    [Inject] private NavigationManager    Nav         { get; set; } = default!;

    private IReadOnlyList<AttendanceDto> _records = [];
    private DateOnly _weekStart = GetMondayOf(IstTimeHelper.TodayIst);
    private bool _loading;

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
        _records = await Sender.Send(new GetEmployeeAttendanceHistoryQuery(
            CurrentUser.UserId.Value, _weekStart, _weekStart.AddDays(6)));
        _loading = false;
    }

    private async Task PrevWeek()
    {
        _weekStart = _weekStart.AddDays(-7);
        await LoadDataAsync();
    }

    private async Task NextWeek()
    {
        _weekStart = _weekStart.AddDays(7);
        await LoadDataAsync();
    }

    private static DateOnly GetMondayOf(DateOnly date)
    {
        var delta = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
        if (delta < 0) delta += 7;
        return date.AddDays(-delta);
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
