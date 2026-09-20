using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Pages.Admin;

public sealed partial class Employees : ComponentBase
{
    [Inject] private ISender             Sender      { get; set; } = default!;
    [Inject] private IEmployeeRepository EmployeeRepo { get; set; } = default!;
    [Inject] private IShiftRepository    ShiftRepo   { get; set; } = default!;
    [Inject] private AttendTrackDbContext Db          { get; set; } = default!;
    [Inject] private ILogger<Employees>  Logger      { get; set; } = default!;

    private IReadOnlyList<EmployeeDto>  _employees   = [];
    private IReadOnlyList<Shift>        _shifts      = [];
    private IReadOnlyList<Department>   _departments = [];
    private string _search = string.Empty;
    private bool _loading = true;
    private string? _error;

    // Modal state
    private bool   _showModal;
    private Guid?  _editId;
    private string _fCode  = string.Empty;
    private string _fName  = string.Empty;
    private string _fEmail = string.Empty;
    private string _fPhone = string.Empty;
    private string _fPin   = string.Empty;
    private string _fRoleStr    = nameof(UserRole.Employee);
    private Guid   _fShiftId;
    private Guid   _fDeptId;
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

        _shifts      = await Db.Shifts.Where(s => s.IsActive).OrderBy(s => s.StartTime).ToListAsync();
        _departments = await Db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        _loading = false;
    }

    private void ShowAddModal()
    {
        _editId = null;
        _fCode = _fName = _fEmail = _fPhone = _fPin = _formError = string.Empty;
        _fRoleStr = nameof(UserRole.Employee);
        _fShiftId = _shifts.FirstOrDefault()?.Id ?? Guid.Empty;
        _fDeptId  = _departments.FirstOrDefault()?.Id ?? Guid.Empty;
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

        try
        {
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

                if (_fShiftId == Guid.Empty)
                { _formError = "Please select a shift."; return; }

                if (_fDeptId == Guid.Empty)
                { _formError = "Please select a department."; return; }

                if (!Enum.TryParse<UserRole>(_fRoleStr, out var role))
                    role = UserRole.Employee;

                await Sender.Send(new CreateEmployeeCommand(
                    _fCode, _fName, _fEmail, _fPhone, _fPin,
                    _fDeptId,
                    _fShiftId,
                    role,
                    IstTimeHelper.TodayIst));
            }
        }
        catch (Exception ex)
        {
            // Keep the modal open so the user can see the error and retry,
            // instead of the exception propagating and tearing down the circuit.
            _formError = ex.Message;
            Logger.LogError(ex, "Employees SaveEmployeeAsync failed");
            return;
        }

        _showModal = false;
        await LoadDataAsync();
    }

    private async Task DeactivateAsync(Guid employeeId)
    {
        _error = null;
        try
        {
            await Sender.Send(new DeactivateEmployeeCommand(employeeId));
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            Logger.LogError(ex, "Employees DeactivateAsync failed for {EmployeeId}", employeeId);
        }
    }
}
