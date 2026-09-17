using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly AttendTrackDbContext _db;
    public EmployeeRepository(AttendTrackDbContext db) => _db = db;

    public async Task<Employee?> GetByIdAsync(EmployeeId id, CancellationToken ct = default)
        => await _db.Employees
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

    public async Task<Employee?> GetByCodeAsync(string employeeCode, CancellationToken ct = default)
    {
        // EmployeeCode is stored uppercase (see Employee.Create), but callers — kiosk
        // login, the Hikvision webhook — pass whatever case they received. Normalize
        // here so lookups match regardless of caller, rather than trusting every call
        // site to remember to uppercase first.
        var normalized = employeeCode.ToUpperInvariant();
        return await _db.Employees
            .FirstOrDefaultAsync(e => e.EmployeeCode == normalized, ct)
            .ConfigureAwait(false);
    }

    public async Task<Employee?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Employees
            .FirstOrDefaultAsync(e => e.Email == email, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Employee>> GetAllActiveAsync(CancellationToken ct = default)
        => await _db.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Employee>> GetByDepartmentAsync(Guid departmentId, CancellationToken ct = default)
        => await _db.Employees
            .Where(e => e.DepartmentId == departmentId && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task AddAsync(Employee employee, CancellationToken ct = default)
        => await _db.Employees.AddAsync(employee, ct).ConfigureAwait(false);

    public void Update(Employee employee)
        => _db.Employees.Update(employee);
}

