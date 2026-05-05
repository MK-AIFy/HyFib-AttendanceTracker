using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Employee;

public sealed partial class MyTimesheets : ComponentBase
{
    [Inject] private ISender Sender { get; set; } = default!;

    private IReadOnlyList<DailyReportDto> _days = [];
    private DateOnly _weekStart = GetMondayOf(IstTimeHelper.TodayIst);
    private bool _loading;

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        _loading = true;
        _days = await Sender.Send(new GetWeeklyReportQuery(_weekStart));
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
