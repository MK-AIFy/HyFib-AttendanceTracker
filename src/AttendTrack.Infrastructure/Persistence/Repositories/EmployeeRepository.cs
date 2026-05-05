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
        => await _db.Employees
            .FirstOrDefaultAsync(e => e.EmployeeCode == employeeCode, ct)
            .ConfigureAwait(false);

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

