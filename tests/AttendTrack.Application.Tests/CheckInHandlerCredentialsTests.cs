using AttendTrack.Application.Commands.Attendance;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendTrack.Application.Tests;

/// <summary>
/// Covers the employee-code-enumeration fix (PR 4/5, kiosk security hardening):
/// CheckInHandler used to throw DomainException("Employee not found: {code}")
/// for an unknown code but UnauthorizedKioskException($"Invalid PIN for {code}")
/// for a wrong PIN — two different exception types with two different, code-
/// echoing messages, letting a kiosk attacker enumerate valid employee codes by
/// the error text alone. Both cases now throw the single, generic
/// InvalidKioskCredentialsException with an identical message.
/// </summary>
public sealed class CheckInHandlerCredentialsTests
{
    private static CheckInHandler CreateSut(Mock<IEmployeeRepository> empRepo, Mock<IPasswordHasher> hasher)
        => new(
            empRepo.Object,
            Mock.Of<IAttendanceRepository>(),
            Mock.Of<IShiftRepository>(),
            hasher.Object,
            Mock.Of<IUnitOfWork>(),
            NullLogger<CheckInHandler>.Instance);

    [Fact]
    public async Task Handle_UnknownEmployeeCode_ThrowsInvalidKioskCredentials()
    {
        var empRepo = new Mock<IEmployeeRepository>();
        empRepo.Setup(r => r.GetByCodeAsync("EMP-404", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Employee?)null);
        var hasher = new Mock<IPasswordHasher>();
        var sut = CreateSut(empRepo, hasher);

        var act = async () => await sut.Handle(new CheckInCommand("EMP-404", "123456"), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<InvalidKioskCredentialsException>();
        thrown.Which.Message.Should().Be("Invalid employee code or PIN.");
    }

    [Fact]
    public async Task Handle_WrongPin_ThrowsIdenticalInvalidKioskCredentials()
    {
        var employee = Employee.Create(
            EmployeeId.New(), "EMP-001", "Jane Doe", "jane@example.com", "555-0100",
            PinHash.From("$2a$hash"), Guid.NewGuid(), Guid.NewGuid(), UserRole.Employee,
            DateOnly.FromDateTime(DateTime.UtcNow));

        var empRepo = new Mock<IEmployeeRepository>();
        empRepo.Setup(r => r.GetByCodeAsync("EMP-001", It.IsAny<CancellationToken>()))
               .ReturnsAsync(employee);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("000000", employee.KioskPin.Value)).Returns(false);
        var sut = CreateSut(empRepo, hasher);

        var act = async () => await sut.Handle(new CheckInCommand("EMP-001", "000000"), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<InvalidKioskCredentialsException>();
        thrown.Which.Message.Should().Be("Invalid employee code or PIN.");
    }

    [Fact]
    public async Task Handle_InactiveEmployee_CorrectPin_StillRejected()
    {
        var employee = Employee.Create(
            EmployeeId.New(), "EMP-002", "John Doe", "john@example.com", "555-0101",
            PinHash.From("$2a$hash"), Guid.NewGuid(), Guid.NewGuid(), UserRole.Employee,
            DateOnly.FromDateTime(DateTime.UtcNow));
        employee.Deactivate();

        var empRepo = new Mock<IEmployeeRepository>();
        empRepo.Setup(r => r.GetByCodeAsync("EMP-002", It.IsAny<CancellationToken>()))
               .ReturnsAsync(employee);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("123456", employee.KioskPin.Value)).Returns(true);
        var sut = CreateSut(empRepo, hasher);

        var act = async () => await sut.Handle(new CheckInCommand("EMP-002", "123456"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidKioskCredentialsException>();
    }
}
