using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class HourlyHeatMap : ComponentBase, IAsyncDisposable
{
    [Inject] private ISender Sender { get; set; } = default!;
    [Inject] private AttendanceNotifier Notifier { get; set; } = default!;

    private IReadOnlyList<HourlyBreakdownDto> _breakdowns = [];
    private DateOnly _selectedDate = IstTimeHelper.TodayIst;
    private bool _loading;

    protected override async Task OnInitializedAsync()
    {
        Notifier.OnAttendanceChanged += OnAttendanceChangedHandler;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _breakdowns = await Sender.Send(new GetHourlyBreakdownQuery(_selectedDate));
        _loading = false;
    }

    private void OnAttendanceChangedHandler()
    {
        // Only auto-refresh when the heat map is showing today's data —
        // historical date views should stay static.
        if (_selectedDate != IstTimeHelper.TodayIst) return;
        InvokeAsync(async () => { await LoadDataAsync(); StateHasChanged(); });
    }

    public async ValueTask DisposeAsync()
    {
        Notifier.OnAttendanceChanged -= OnAttendanceChangedHandler;
        await Task.CompletedTask;
    }

    private static string HeatColor(HourlySlotDto? slot)
    {
        if (slot is null || slot.MinutesWorked == 0) return "#dee2e6"; // absent/grey
        if (slot.IsBreak)     return "#ffc107"; // amber break
        if (slot.IsOvertime)  return "#6610f2"; // purple overtime
        // Green intensity by minutes: 1-30 → lighter, 31-60 → darker
        var intensity = slot.MinutesWorked >= 45 ? "#198754" : slot.MinutesWorked >= 20 ? "#20c997" : "#a3d9a5";
        return intensity;
    }
}
