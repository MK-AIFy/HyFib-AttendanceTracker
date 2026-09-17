using AttendTrack.Application.Commands.Employee;
using AttendTrack.Application.Commands.Shift;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Integration.Tests.Helpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AttendTrack.Integration.Tests;

/// <summary>
/// Verifies that UpdateEmployeeCommand, DeactivateEmployeeCommand and AssignShiftCommand
/// actually persist their changes even though none of their handlers call
/// IEmployeeRepository.Update() explicitly. Each handler loads the aggregate through a
/// tracking query and mutates it in place via a domain method, so EF Core's snapshot
/// change tracker picks up the modified properties on SaveChangesAsync without an
/// explicit Update() call — this was flagged as a "deferred" risk in project docs but
/// was never actually exercised by a test. Each test re-reads the row from a brand-new
/// scope (a fresh DbContext) to prove the write reached the database, not just the
/// in-memory tracked instance.
/// </summary>
[Collection("EmployeeCommands")]
public sealed class EmployeeCommandTests : IClassFixture<AttendTrackWebApplicationFactory>
{
    private readonly AttendTrackWebApplicationFactory _factory;

    public EmployeeCommandTests(AttendTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateEmployeeCommand_PersistsToDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new UpdateEmployeeCommand(
            AttendTrackWebApplicationFactory.TestEmployeeId,
            "Jane Updated",
            "jane.updated@test.com",
            "9999999999"));

        var employee = await ReadFreshAsync();

        employee.FullName.Should().Be("Jane Updated");
        employee.Email.Should().Be("jane.updated@test.com");
        employee.Phone.Should().Be("9999999999");
    }

    [Fact]
    public async Task DeactivateEmployeeCommand_PersistsToDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new DeactivateEmployeeCommand(AttendTrackWebApplicationFactory.TestEmployeeId));

        var employee = await ReadFreshAsync();

        employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AssignShiftCommand_PersistsToDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new AssignShiftCommand(
            AttendTrackWebApplicationFactory.TestEmployeeId,
            AttendTrackWebApplicationFactory.TestSecondShiftId));

        var employee = await ReadFreshAsync();

        employee.DefaultShiftId.Should().Be(AttendTrackWebApplicationFactory.TestSecondShiftId);
    }

    [Fact]
    public async Task CreateEmployeeCommand_WithNonExistentDepartment_FailsOnSave()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // A syntactically valid GUID that doesn't correspond to any seeded Department.
        // CreateEmployeeCommandValidator only checks DepartmentId != Guid.Empty, not that
        // it actually exists — the FK constraint on employees.DepartmentId is what's
        // supposed to catch this.
        var act = () => sender.Send(new CreateEmployeeCommand(
            EmployeeCode:   "EMP-ORPHAN",
            FullName:       "Ghost Employee",
            Email:          "ghost@test.com",
            Phone:          "1234567890",
            Pin:            "123456",
            DepartmentId:   Guid.NewGuid(),
            DefaultShiftId: AttendTrackWebApplicationFactory.TestShiftId,
            Role:           AttendTrack.Domain.Enums.UserRole.Employee,
            JoinedAt:       DateOnly.FromDateTime(DateTime.UtcNow)));

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>Reads the seeded test employee through a brand-new scope/DbContext,
    /// bypassing any change-tracker cache from the scope that issued the command.
    /// Filters by EmployeeCode (a plain string column) rather than Id.Value — EF cannot
    /// translate a value-object member access like `e.Id.Value` into SQL.</summary>
    private async Task<AttendTrack.Domain.Entities.Employee> ReadFreshAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
        return await db.Employees.AsNoTracking()
            .SingleAsync(e => e.EmployeeCode == AttendTrackWebApplicationFactory.TestEmployeeCode);
    }
}
