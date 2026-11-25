using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Customers.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class RefundLoyaltyServiceTests
{
    [Theory]
    [InlineData(498, CustomerTier.Standard)]
    [InlineData(499, CustomerTier.Silver)]
    [InlineData(1498, CustomerTier.Silver)]
    [InlineData(1499, CustomerTier.Gold)]
    [InlineData(4998, CustomerTier.Gold)]
    [InlineData(4999, CustomerTier.Platinum)]
    public async Task Sale_OnePointCrossesTierAtThresholdAndReplayLeavesBalanceUnchanged(int initial, CustomerTier expected)
    {
        // Arrange
        var journal = new Journal(initial);
        var service = new RefundLoyaltyService(journal, TimeProvider.System);
        var orderId = Guid.NewGuid();

        // Act
        await service.StageSaleAsync(orderId, journal.Customer.Id, 199_999.99m, default);
        await service.StageSaleAsync(orderId, journal.Customer.Id, 199_999.99m, default);

        // Assert
        Assert.Equal(expected, journal.Customer.Tier);
        Assert.Equal(initial + 1, journal.Customer.LoyaltyPoints);
        var entry = Assert.Single(journal.Entries);
        Assert.Equal(1, entry.PointsChange);
        Assert.Equal(initial + 1, entry.BalanceAfter);
        Assert.True(entry.IsSaleAccrual);
        Assert.Null(entry.RefundCaseId);
    }

    [Fact]
    public async Task Refund_RecomputesFloorOnRetainedNetAndRespectsInitialBalance()
    {
        // Arrange
        var journal = new Journal(499);
        var service = new RefundLoyaltyService(journal, TimeProvider.System);
        var order = Guid.NewGuid();
        var refund = Guid.NewGuid();
        await service.StageSaleAsync(order, journal.Customer.Id, 250_000m, default);

        // Act
        await service.StageReversalAsync(refund, order, journal.Customer.Id, 199_999.99m, default);
        await service.StageReversalAsync(refund, order, journal.Customer.Id, 199_999.99m, default);
        await service.StageReversalAsync(Guid.NewGuid(), order, journal.Customer.Id, 100_000m, default);
        await service.StageReversalAsync(Guid.NewGuid(), order, journal.Customer.Id, 0m, default);

        // Assert
        Assert.Equal(new[] { 2, -1, -1 }, journal.Entries.Select(x => x.PointsChange));
        Assert.Equal(new[] { 501, 500, 499 }, journal.Entries.Select(x => x.BalanceAfter));
        Assert.Equal(499, journal.Customer.LoyaltyPoints);
        Assert.Equal(CustomerTier.Standard, journal.Customer.Tier);
    }

    [Fact]
    public async Task Refund_NoAttributedSaleCredit_PreservesHistoricalPointsAndTier()
    {
        // Arrange
        var journal = new Journal(208);
        var service = new RefundLoyaltyService(journal, TimeProvider.System);
        var id = Guid.NewGuid();
        journal.Entries.Add(new() { CustomerId = journal.Customer.Id, PointsChange = 208, BalanceAfter = 208, Reason = "Opening balance" });

        // Act
        await service.StageSaleAsync(id, journal.Customer.Id, 99_999.99m, default);
        await service.StageReversalAsync(Guid.NewGuid(), id, journal.Customer.Id, 0m, default);

        // Assert
        Assert.Single(journal.Entries);
        Assert.Equal(208, journal.Customer.LoyaltyPoints);
        Assert.Equal(CustomerTier.Standard, journal.Customer.Tier);
    }

    private sealed class Journal(int balance) : ILoyaltyPointRepository
    {
        public Customer Customer { get; } = new() { LoyaltyPoints = balance };
        public List<CustomerLoyaltyPoint> Entries { get; } = [];
        public Task<Customer> LockCustomerAsync(Guid customerId, CancellationToken ct) => Task.FromResult(Customer);
        public Task<List<CustomerLoyaltyPoint>> OrderEntriesAsync(Guid orderId, Guid customerId, CancellationToken ct) =>
            Task.FromResult(Entries.Where(e => e.OrderId == orderId && e.CustomerId == customerId).ToList());
        public void Add(CustomerLoyaltyPoint entry) => Entries.Add(entry);
    }
}
