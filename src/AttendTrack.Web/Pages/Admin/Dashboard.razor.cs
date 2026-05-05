using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class Dashboard : ComponentBase, IAsyncDisposable
{
    [Inject] private ISender Sender { get; set; } = default!;
    [Inject] private IHikvisionDeviceRepository DeviceRepo { get; set; } = default!;
    [Inject] private AttendanceNotifier Notifier { get; set; } = default!;

    private IReadOnlyList<AttendanceDto> _liveRecords = [];
    private IReadOnlyList<HikvisionDevice> _devices = [];
    private bool _loading = true;

    private IEnumerable<AttendanceDto> _recentRecords
        => _liveRecords.OrderByDescending(r => r.CheckInTimeIst).Take(10);

    private int PresentCount  => _liveRecords.Count(r => r.Status == "Present");
    private int AbsentCount   => _liveRecords.Count(r => r.Status == "Absent");
    private int LateCount     => _liveRecords.Count(r => r.HasViolation);
    private int OnBreakCount  => _liveRecords.Count(r => r.BreakDurationMinutes > 0 && r.CheckOutTimeIst == null);

    protected override async Task OnInitializedAsync()
    {
        Notifier.OnAttendanceChanged += OnAttendanceChangedHandler;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _liveRecords = await Sender.Send(new GetLiveAttendanceQuery());
        _devices = await DeviceRepo.GetActiveDevicesAsync();
        _loading = false;
    }

    private void OnAttendanceChangedHandler()
        => InvokeAsync(async () => { await LoadDataAsync(); StateHasChanged(); });

    public async ValueTask DisposeAsync()
    {
        Notifier.OnAttendanceChanged -= OnAttendanceChangedHandler;
        await Task.CompletedTask;
    }
}
