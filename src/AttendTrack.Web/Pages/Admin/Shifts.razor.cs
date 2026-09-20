using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class Shifts : ComponentBase
{
    [Inject] private ISender         Sender    { get; set; } = default!;
    [Inject] private IShiftRepository ShiftRepo { get; set; } = default!;
    [Inject] private ILogger<Shifts>  Logger    { get; set; } = default!;

    private IReadOnlyList<Shift> _shifts = [];
    private bool _loading = true;
    private bool _showModal;

    private string _fName  = string.Empty;
    private string _fStart = string.Empty;
    private string _fEnd   = string.Empty;
    private int    _fGrace = 15;
    private int    _fOt    = 30;
    private string _formError = string.Empty;

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        _loading = true;
        _shifts = await ShiftRepo.GetAllActiveAsync();
        _loading = false;
    }

    private void ShowAddModal()
    {
        _fName = _fStart = _fEnd = _formError = string.Empty;
        _fGrace = 15;
        _fOt = 30;
        _showModal = true;
    }

    private void CloseModal() => _showModal = false;

    private async Task SaveAsync()
    {
        _formError = string.Empty;
        if (string.IsNullOrWhiteSpace(_fName))
        { _formError = "Name is required."; return; }
        if (!TimeOnly.TryParse(_fStart, out var start))
        { _formError = "Invalid start time (HH:mm)."; return; }
        if (!TimeOnly.TryParse(_fEnd, out var end))
        { _formError = "Invalid end time (HH:mm)."; return; }

        try
        {
            await Sender.Send(new CreateShiftCommand(_fName, start, end, _fGrace, _fOt));
        }
        catch (Exception ex)
        {
            _formError = ex.Message;
            Logger.LogError(ex, "Shifts SaveAsync failed");
            return;
        }

        _showModal = false;
        await LoadDataAsync();
    }
}
