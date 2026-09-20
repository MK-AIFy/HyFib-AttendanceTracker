using AttendTrack.Application.Behaviours;
using AttendTrack.Application.Common;
using AttendTrack.Application.Interfaces;
using AttendTrack.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace AttendTrack.Application.Tests;

/// <summary>
/// Covers the brute-force lockout fix (PR 4/5, kiosk security hardening):
/// previously the only lockout was KioskTerminal.razor.cs's in-memory
/// _failedAttempts counter, which reset on every browser refresh (a new
/// Blazor circuit) and never expired on its own within a locked circuit.
/// KioskLockoutBehaviour replicates AuthController's Redis-backed lockout at
/// the MediatR pipeline level, keyed by EmployeeCode.
/// </summary>
public sealed class KioskLockoutBehaviourTests
{
    private sealed record FakeKioskRequest(string EmployeeCode) : IRequest<string>, IKioskPinRequest;

    /// <summary>Minimal in-memory stand-in for Redis, enough to exercise the
    /// behaviour's Get/Set/Remove sequence across multiple calls.</summary>
    private sealed class FakeCache : IDistributedCacheWrapper
    {
        private readonly Dictionary<string, object?> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> KeyExistsAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_store.ContainsKey(key));
    }

    private static KioskLockoutBehaviour<FakeKioskRequest, string> CreateSut(FakeCache cache)
        => new(cache, NullLogger<KioskLockoutBehaviour<FakeKioskRequest, string>>.Instance);

    [Fact]
    public async Task Handle_NoPriorFailures_CallsNext_AndSucceeds()
    {
        var cache = new FakeCache();
        var sut   = CreateSut(cache);
        var request = new FakeKioskRequest("EMP-001");

        var result = await sut.Handle(request, (_) => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_FifthConsecutiveFailure_LocksOutEmployeeCode()
    {
        var cache = new FakeCache();
        var sut   = CreateSut(cache);
        var request = new FakeKioskRequest("EMP-002");

        RequestHandlerDelegate<string> failingNext =
            (_) => throw new InvalidKioskCredentialsException();

        for (var i = 0; i < 5; i++)
        {
            var act = async () => await sut.Handle(request, failingNext, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidKioskCredentialsException>();
        }

        // 6th attempt: still within the lockout window, should be rejected
        // BEFORE reaching the handler at all — the whole point of server-side
        // lockout being independent of the Blazor circuit's own retry count.
        var lockedAct = async () => await sut.Handle(request, failingNext, CancellationToken.None);
        await lockedAct.Should().ThrowAsync<KioskLockedOutException>();
    }

    [Fact]
    public async Task Handle_AlreadyLockedOut_ThrowsWithoutInvokingHandler()
    {
        var cache = new FakeCache();
        await cache.SetAsync("kiosk:lockout:EMP-003", (DateTime?)DateTime.UtcNow.AddMinutes(10));
        var sut = CreateSut(cache);
        var request = new FakeKioskRequest("emp-003"); // lower-case — key must be case-insensitive

        var nextCalled = false;
        RequestHandlerDelegate<string> next = (_) =>
        {
            nextCalled = true;
            return Task.FromResult("should not reach here");
        };

        var act = async () => await sut.Handle(request, next, CancellationToken.None);

        await act.Should().ThrowAsync<KioskLockedOutException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SuccessAfterEarlierFailure_ClearsAttemptCounter()
    {
        var cache = new FakeCache();
        var sut   = CreateSut(cache);
        var request = new FakeKioskRequest("EMP-004");

        var failOnce = async () => await sut.Handle(
            request, (_) => throw new InvalidKioskCredentialsException(), CancellationToken.None);
        await failOnce.Should().ThrowAsync<InvalidKioskCredentialsException>();

        var result = await sut.Handle(request, (_) => Task.FromResult("ok"), CancellationToken.None);
        result.Should().Be("ok");

        var attempts = await cache.GetAsync<int>("kiosk:attempts:EMP-004", CancellationToken.None);
        attempts.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NonKioskRequest_PassesThroughWithoutTouchingCache()
    {
        // Sanity check that the behaviour is a no-op for requests that don't
        // implement IKioskPinRequest (e.g. CreateEmployeeCommand).
        var cache = new FakeCache();
        var sut   = new KioskLockoutBehaviour<string, string>(
            cache, NullLogger<KioskLockoutBehaviour<string, string>>.Instance);

        var result = await sut.Handle("not a kiosk request", (_) => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }
}
