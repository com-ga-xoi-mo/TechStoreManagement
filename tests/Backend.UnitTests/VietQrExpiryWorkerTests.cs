using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales;
using TechStore.Api.Sales.Repositories;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class VietQrExpiryWorkerTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeVietQrHoldRepository _requests = new();
    private readonly FakeVietQrHoldService _holds;
    private readonly VietQrExpiryWorker _worker;

    public VietQrExpiryWorkerTests()
    {
        _holds = new FakeVietQrHoldService(_requests);
        var services = new ServiceCollection()
            .AddScoped<IVietQrHoldRepository>(_ => _requests)
            .AddScoped<IVietQrHoldService>(_ => _holds)
            .BuildServiceProvider();
        _worker = new VietQrExpiryWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new VietQrOptions()),
            new FixedTimeProvider(new DateTimeOffset(Now)),
            NullLogger<VietQrExpiryWorker>.Instance);
    }

    [Fact]
    public async Task SweepAsync_ExpiresOnlyPendingRequestsPastTheirDeadline()
    {
        // Arrange
        var overdue = _requests.AddRequest(NewOrder(), Guid.NewGuid(), expiresAt: Now.AddMinutes(-1));
        var waiting = _requests.AddRequest(NewOrder(), Guid.NewGuid(), expiresAt: Now.AddMinutes(5));
        var paid = _requests.AddRequest(NewOrder(), Guid.NewGuid(), VietQrStatus.Confirmed, Now.AddMinutes(-30));

        // Act
        var closed = await _worker.SweepAsync(CancellationToken.None);

        // Assert
        closed.Should().Be(1);
        overdue.Status.Should().Be(VietQrStatus.Expired);
        waiting.Status.Should().Be(VietQrStatus.Pending);
        paid.Status.Should().Be(VietQrStatus.Confirmed);
    }

    [Fact]
    public async Task SweepAsync_NothingDue_ReturnsZero()
    {
        // Arrange
        _requests.AddRequest(NewOrder(), Guid.NewGuid(), expiresAt: Now.AddMinutes(5));

        // Act and Assert
        (await _worker.SweepAsync(CancellationToken.None)).Should().Be(0);
        _holds.Expired.Should().BeEmpty();
    }

    [Fact]
    public async Task SweepAsync_OneRequestFails_TheRestAreStillExpired()
    {
        // Arrange
        var first = _requests.AddRequest(NewOrder(), Guid.NewGuid(), expiresAt: Now.AddMinutes(-3));
        var second = _requests.AddRequest(NewOrder(), Guid.NewGuid(), expiresAt: Now.AddMinutes(-2));
        var failing = new ThrowingFirstHoldService(_holds, first.Id);
        var services = new ServiceCollection()
            .AddScoped<IVietQrHoldRepository>(_ => _requests)
            .AddScoped<IVietQrHoldService>(_ => failing)
            .BuildServiceProvider();
        var worker = new VietQrExpiryWorker(
            services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new VietQrOptions()),
            new FixedTimeProvider(new DateTimeOffset(Now)), NullLogger<VietQrExpiryWorker>.Instance);

        // Act
        var closed = await worker.SweepAsync(CancellationToken.None);

        // Assert
        closed.Should().Be(1);
        first.Status.Should().Be(VietQrStatus.Pending);
        second.Status.Should().Be(VietQrStatus.Expired);
    }

    [Fact]
    public async Task ExecuteAsync_SweepSecondsZero_DoesNothingAndEndsAtOnce()
    {
        // Arrange
        _requests.AddRequest(NewOrder(), Guid.NewGuid(), expiresAt: Now.AddMinutes(-1));
        var options = new VietQrOptions { ExpirySweepSeconds = 0 };
        var worker = new VietQrExpiryWorker(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Options.Create(options),
            new FixedTimeProvider(new DateTimeOffset(Now)), NullLogger<VietQrExpiryWorker>.Instance);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _holds.Expired.Should().BeEmpty();
    }

    private static Order NewOrder() => new()
    {
        OrderCode = "ORD-" + Guid.NewGuid().ToString("N")[..6],
        CashierUserId = Guid.NewGuid(),
        PaymentMethod = PaymentMethod.VietQr,
        TotalAmount = 1m
    };

    private sealed class ThrowingFirstHoldService(IVietQrHoldService inner, Guid failingId) : IVietQrHoldService
    {
        public Task<PosResult<ClosedVietQrHold>> CloseAsync(Guid id, VietQrCloseReason reason, TechStore.Api.Identity.Services.AuthenticatedUser actor, CancellationToken ct) =>
            inner.CloseAsync(id, reason, actor, ct);

        public Task<PosResult<ClosedVietQrHold>> ExpireDueAsync(Guid id, CancellationToken ct) =>
            id == failingId ? throw new InvalidOperationException("database unavailable") : inner.ExpireDueAsync(id, ct);
    }
}
