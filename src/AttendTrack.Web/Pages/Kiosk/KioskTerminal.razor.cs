using Microsoft.AspNetCore.Components;
using System.Threading;

namespace AttendTrack.Web.Pages.Kiosk;

public sealed partial class KioskTerminal : ComponentBase, IAsyncDisposable
{
    [Inject] private ISender            Sender   { get; set; } = default!;
    [Inject] private AttendanceNotifier Notifier { get; set; } = default!;

    private string _employeeCode = string.Empty;
    private string _pin          = string.Empty;
    private string _message      = string.Empty;
    private bool   _isError;
    private bool   _lockedOut;
    private int    _failedAttempts;
    private int    _presentCount;
    private string _currentTime = string.Empty;
    private string _todayDate   = string.Empty;

    private PeriodicTimer? _clockTimer;
    private CancellationTokenSource? _idleCts;

    protected override async Task OnInitializedAsync()
    {
        Notifier.OnAttendanceChanged += OnAttendanceChangedHandler;
        _todayDate = IstTimeHelper.TodayIst.ToString("dd MMM yyyy");
        _clockTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = RunClockAsync();
        ResetIdleTimer();
        await Task.CompletedTask;
    }

    private async Task RunClockAsync()
    {
        while (_clockTimer is not null && await _clockTimer.WaitForNextTickAsync())
        {
            _currentTime = IstTimeHelper.NowIst.ToString("hh:mm:ss tt");
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ResetIdleTimer(Microsoft.AspNetCore.Components.ChangeEventArgs? args = null)
    {
        _idleCts?.Cancel();
        _idleCts?.Dispose();
        _idleCts = new CancellationTokenSource();
        var token = _idleCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                await InvokeAsync(() =>
                {
                    _employeeCode = string.Empty;
                    _pin = string.Empty;
                    _message = string.Empty;
                    StateHasChanged();
                });
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private async Task CheckInAsync()
    {
        if (!ValidateInput()) return;
        try
        {
            await Sender.Send(new CheckInCommand(_employeeCode, _pin, null));
            ShowSuccess("Check-in recorded.");
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private async Task CheckOutAsync()
    {
        if (!ValidateInput()) return;
        try
        {
            await Sender.Send(new CheckOutCommand(_employeeCode, _pin));
            ShowSuccess("Check-out recorded.");
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private async Task BreakStartAsync()
    {
        if (!ValidateInput()) return;
        try
        {
            await Sender.Send(new StartBreakCommand(_employeeCode, _pin, BreakType.Lunch));
            ShowSuccess("Break started.");
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private async Task BreakEndAsync()
    {
        if (!ValidateInput()) return;
        try
        {
            await Sender.Send(new EndBreakCommand(_employeeCode, _pin, Guid.Empty));
            ShowSuccess("Break ended.");
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_employeeCode) || string.IsNullOrWhiteSpace(_pin))
        {
            _message = "Please enter employee code and PIN.";
            _isError = true;
            return false;
        }
        return true;
    }

    private void ShowSuccess(string msg)
    {
        _failedAttempts = 0;
        _message = msg;
        _isError = false;
        _employeeCode = string.Empty;
        _pin = string.Empty;
    }

    private void HandleError(Exception ex)
    {
        _failedAttempts++;
        _isError = true;
        _message = ex.Message;
        if (_failedAttempts >= 3)
            _lockedOut = true;
    }

    private void OnAttendanceChangedHandler()
        => InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        Notifier.OnAttendanceChanged -= OnAttendanceChangedHandler;
        _clockTimer?.Dispose();
        _idleCts?.Cancel();
        _idleCts?.Dispose();
        await Task.CompletedTask;
    }
}
