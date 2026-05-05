using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Components;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class Employees : ComponentBase
{
    [Inject] private ISender            Sender      { get; set; } = default!;
    [Inject] private IEmployeeRepository EmployeeRepo { get; set; } = default!;
    [Inject] private IShiftRepository   ShiftRepo   { get; set; } = default!;

    private IReadOnlyList<EmployeeDto> _employees = [];
    private string _search = string.Empty;
    private bool _loading = true;

    // Modal state
    private bool   _showModal;
    private Guid?  _editId;
    private string _fCode  = string.Empty;
    private string _fName  = string.Empty;
    private string _fEmail = string.Empty;
    private string _fPhone = string.Empty;
    private string _fPin   = string.Empty;
    private string _fRoleStr = nameof(UserRole.Employee);
    private string _formError = string.Empty;

    private IEnumerable<EmployeeDto> FilteredEmployees =>
        string.IsNullOrWhiteSpace(_search)
            ? _employees
            : _employees.Where(e =>
                e.EmployeeCode.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                e.FullName.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                e.Email.Contains(_search, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        _loading = true;
        var employees = await EmployeeRepo.GetAllActiveAsync();
        _employees = employees
            .Select(e => new EmployeeDto(
                e.Id.Value, e.EmployeeCode, e.FullName, e.Email, e.Phone,
                e.DepartmentId, e.DefaultShiftId,
                e.Role.ToString(), e.IsActive, e.IsBiometricEnrolled,
                e.BadgeRfidCard, e.JoinedAt.ToString("dd MMM yyyy")))
            .ToList();
        _loading = false;
    }

    private void ShowAddModal()
    {
        _editId = null;
        _fCode = _fName = _fEmail = _fPhone = _fPin = _formError = string.Empty;
        _fRoleStr = nameof(UserRole.Employee);
        _showModal = true;
    }

    private void ShowEditModal(EmployeeDto emp)
    {
        _editId = emp.EmployeeId;
        _fName = emp.FullName;
        _fEmail = emp.Email;
        _fPhone = emp.Phone;
        _formError = string.Empty;
        _showModal = true;
    }

    private void CloseModal()
    {
        _showModal = false;
        _formError = string.Empty;
    }

    private async Task SaveEmployeeAsync()
    {
        _formError = string.Empty;

        if (_editId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(_fName) || string.IsNullOrWhiteSpace(_fEmail))
            { _formError = "Name and email are required."; return; }

            await Sender.Send(new UpdateEmployeeCommand(_editId.Value, _fName, _fEmail, _fPhone));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_fCode) || string.IsNullOrWhiteSpace(_fName) ||
                string.IsNullOrWhiteSpace(_fEmail) || _fPin.Length != 6)
            { _formError = "Code, name, email and a 6-digit PIN are required."; return; }

            if (!Enum.TryParse<UserRole>(_fRoleStr, out var role))
                role = UserRole.Employee;

            // Use first available shift/department as defaults
            var shifts = await ShiftRepo.GetAllActiveAsync();
            var defaultShift = shifts.FirstOrDefault();
            if (defaultShift is null)
            { _formError = "No shifts configured. Please create a shift first."; return; }

            await Sender.Send(new CreateEmployeeCommand(
                _fCode, _fName, _fEmail, _fPhone, _fPin,
                Guid.Empty,              // DepartmentId — placeholder (no dept mgmt UI yet)
                defaultShift.Id,
                role,
                IstTimeHelper.TodayIst));
        }

        _showModal = false;
        await LoadDataAsync();
    }

    private async Task DeactivateAsync(Guid employeeId)
    {
        await Sender.Send(new DeactivateEmployeeCommand(employeeId));
        await LoadDataAsync();
    }
}
