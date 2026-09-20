using AttendTrack.Application.Commands.Employee;
using AttendTrack.Application.Common;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendTrack.Application.Tests;

/// <summary>
/// Covers the privilege-escalation fix (PR: authorization hardening):
/// CreateEmployeeHandler used to create an employee with whatever Role the
/// caller specified, with no check that the caller was even permitted to
/// grant it — any role allowed onto /admin/employees (as low as Manager)
/// could create a brand-new SuperAdmin account and log in as it. The handler
/// now rejects any requested role more privileged than the caller's own
/// (UserRole's ordinal is privilege-ordered: SuperAdmin=0 is most privileged).
/// </summary>
public sealed class CreateEmployeeHandlerRoleTests
{
    private static CreateEmployeeHandler CreateSut(string? callerRole, out Mock<IEmployeeRepository> empRepo)
    {
        empRepo = new Mock<IEmployeeRepository>();
        empRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Employee?)null);
        empRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Employee?)null);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("$2a$hashed");

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.Role).Returns(callerRole);

        return new CreateEmployeeHandler(
            empRepo.Object, hasher.Object, Mock.Of<IUnitOfWork>(), currentUser.Object,
            NullLogger<CreateEmployeeHandler>.Instance);
    }

    private static CreateEmployeeCommand BuildCommand(UserRole requestedRole) => new(
        EmployeeCode: "EMP-100", FullName: "Test User", Email: "test@example.com",
        Phone: "555-0100", Pin: "123456",
        DepartmentId: Guid.NewGuid(), DefaultShiftId: Guid.NewGuid(),
        Role: requestedRole, JoinedAt: DateOnly.FromDateTime(DateTime.UtcNow));

    [Fact]
    public async Task Handle_ManagerRequestsSuperAdminRole_ThrowsInsufficientRole()
    {
        var sut = CreateSut(nameof(UserRole.Manager), out var empRepo);

        var act = async () => await sut.Handle(BuildCommand(UserRole.SuperAdmin), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientRoleException>();
        empRepo.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ManagerRequestsAdminRole_ThrowsInsufficientRole()
    {
        var sut = CreateSut(nameof(UserRole.Manager), out _);

        var act = async () => await sut.Handle(BuildCommand(UserRole.Admin), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientRoleException>();
    }

    [Fact]
    public async Task Handle_ManagerRequestsEmployeeRole_Succeeds()
    {
        var sut = CreateSut(nameof(UserRole.Manager), out var empRepo);

        var id = await sut.Handle(BuildCommand(UserRole.Employee), CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        empRepo.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ManagerRequestsManagerRole_PeerLevelSucceeds()
    {
        var sut = CreateSut(nameof(UserRole.Manager), out var empRepo);

        var id = await sut.Handle(BuildCommand(UserRole.Manager), CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        empRepo.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuperAdminRequestsSuperAdminRole_Succeeds()
    {
        var sut = CreateSut(nameof(UserRole.SuperAdmin), out var empRepo);

        var id = await sut.Handle(BuildCommand(UserRole.SuperAdmin), CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        empRepo.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthenticatedCaller_ThrowsInsufficientRole()
    {
        var sut = CreateSut(callerRole: null, out _);

        var act = async () => await sut.Handle(BuildCommand(UserRole.Employee), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientRoleException>();
    }
}
