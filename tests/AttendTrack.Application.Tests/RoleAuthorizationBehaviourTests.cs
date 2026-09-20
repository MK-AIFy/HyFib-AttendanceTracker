using AttendTrack.Application.Behaviours;
using AttendTrack.Application.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendTrack.Application.Tests;

/// <summary>
/// Covers the Hikvision enroll/sync role-bypass fix: HikvisionDevices.razor.cs
/// called Sender.Send directly for EnrollEmployeeToDeviceCommand/
/// SyncDeviceEventsCommand with no role check, even though the equivalent HTTP
/// controller endpoints deliberately restrict both to SuperAdmin/Admin — a
/// Manager/HRManager account (allowed onto the page to view device status)
/// could still trigger either privileged action via the Blazor UI.
/// RoleAuthorizationBehaviour closes this independently of any page-level
/// [Authorize(Roles=...)], since Sender.Send from a Blazor component is an
/// in-process MediatR call that never goes through ASP.NET Core's
/// authorization middleware.
/// </summary>
public sealed class RoleAuthorizationBehaviourTests
{
    private sealed record FakeAdminRequest : IRequest<string>, IRequiresMinimumRole
    {
        public UserRole MinimumRole => UserRole.Admin;
    }

    private static RoleAuthorizationBehaviour<FakeAdminRequest, string> CreateSut(string? callerRole)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.Role).Returns(callerRole);
        return new RoleAuthorizationBehaviour<FakeAdminRequest, string>(
            currentUser.Object,
            NullLogger<RoleAuthorizationBehaviour<FakeAdminRequest, string>>.Instance);
    }

    [Theory]
    [InlineData(nameof(UserRole.SuperAdmin))]
    [InlineData(nameof(UserRole.Admin))]
    public async Task Handle_CallerMeetsMinimumRole_InvokesNext(string callerRole)
    {
        var sut = CreateSut(callerRole);

        var result = await sut.Handle(new FakeAdminRequest(), (_) => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Theory]
    [InlineData(nameof(UserRole.HRManager))]
    [InlineData(nameof(UserRole.Manager))]
    [InlineData(nameof(UserRole.Employee))]
    public async Task Handle_CallerBelowMinimumRole_ThrowsWithoutInvokingNext(string callerRole)
    {
        var sut = CreateSut(callerRole);
        var nextCalled = false;
        RequestHandlerDelegate<string> next = (_) => { nextCalled = true; return Task.FromResult("ok"); };

        var act = async () => await sut.Handle(new FakeAdminRequest(), next, CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientRoleException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoAuthenticatedCaller_ThrowsWithoutInvokingNext()
    {
        var sut = CreateSut(callerRole: null);
        var nextCalled = false;
        RequestHandlerDelegate<string> next = (_) => { nextCalled = true; return Task.FromResult("ok"); };

        var act = async () => await sut.Handle(new FakeAdminRequest(), next, CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientRoleException>();
        nextCalled.Should().BeFalse();
    }
}
