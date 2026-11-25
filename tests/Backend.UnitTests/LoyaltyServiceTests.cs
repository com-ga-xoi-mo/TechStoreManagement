using FluentAssertions;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

// M4 Vy: earn on a completed sale, reverse on a refund, idempotent by order and by return.
public class LoyaltyServiceTests
{
    private readonly FakeLoyaltyRepository _loyalty = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly LoyaltyService _service;

    public LoyaltyServiceTests()
    {
        _service = new LoyaltyService(_loyalty, _customers);
    }

    [Fact]
    public async Task AwardForOrderAsync_M4ReferenceSale_Adds18PointsAndLedgerRow()
    {
        // Arrange: 2 x 1,000,000 with a 200,000 order discount = 1,800,000 goods before VAT.
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.Applied);
        change.PointsChange.Should().Be(18);
        change.BalanceAfter.Should().Be(18);
        customer.LoyaltyPoints.Should().Be(18);
        var entry = _loyalty.Entries.Should().ContainSingle().Subject;
        entry.CustomerId.Should().Be(customer.Id);
        entry.OrderId.Should().Be(order.Id);
        entry.PointsChange.Should().Be(18);
        entry.BalanceAfter.Should().Be(18);
        entry.ReferenceKey.Should().Be($"earn:{order.Id}");
    }

    [Fact]
    public async Task AwardForOrderAsync_PointsCrossSilverThreshold_UpdatesTier()
    {
        // Arrange
        var customer = AddCustomer(points: 490);
        var order = CompletedOrder(customer.Id, subtotal: 1_000_000m, discount: 0m);

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        customer.LoyaltyPoints.Should().Be(500);
        customer.Tier.Should().Be(CustomerTier.Silver);
        change.Tier.Should().Be(CustomerTier.Silver);
    }

    [Fact]
    public async Task AwardForOrderAsync_SameOrderTwice_AddsPointsOnce()
    {
        // Arrange
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);
        await _service.AwardForOrderAsync(order);

        // Act
        var second = await _service.AwardForOrderAsync(order);

        // Assert
        second.Outcome.Should().Be(LoyaltyOutcome.AlreadyApplied);
        second.PointsChange.Should().Be(18);
        _loyalty.Entries.Should().HaveCount(1);
        customer.LoyaltyPoints.Should().Be(18);
    }

    [Fact]
    public async Task AwardForOrderAsync_OrderWithoutCustomer_IsNotEligible()
    {
        // Arrange
        var order = CompletedOrder(null, subtotal: 5_000_000m, discount: 0m);

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.NotEligible);
        _loyalty.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task AwardForOrderAsync_OrderNotCompleted_IsNotEligible(OrderStatus status)
    {
        // Arrange
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 5_000_000m, discount: 0m);
        order.Status = status;

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.NotEligible);
        customer.LoyaltyPoints.Should().Be(0);
        _loyalty.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task AwardForOrderAsync_GoodsBelowOnePoint_AddsNoLedgerRow()
    {
        // Arrange
        var customer = AddCustomer(points: 10);
        var order = CompletedOrder(customer.Id, subtotal: 99_999m, discount: 0m);

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.NoPoints);
        _loyalty.Entries.Should().BeEmpty();
        customer.LoyaltyPoints.Should().Be(10);
    }

    [Fact]
    public async Task AwardForOrderAsync_UnknownCustomer_IsNotEligible()
    {
        // Arrange
        var order = CompletedOrder(Guid.NewGuid(), subtotal: 5_000_000m, discount: 0m);

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.NotEligible);
    }

    [Fact]
    public async Task AwardForOrderAsync_UsesGoodsAfterDiscountsAndBeforeVat()
    {
        // Arrange: total 1,980,000 includes 180,000 VAT; goods after discount are 1,800,000.
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);
        order.TaxAmount = 180_000m;
        order.TotalAmount = 1_980_000m;

        // Act
        var change = await _service.AwardForOrderAsync(order);

        // Assert
        change.PointsChange.Should().Be(18);
    }

    [Fact]
    public async Task ReverseForReturnAsync_PartialThenFinalReturn_Deducts9ThenAnother9()
    {
        // Arrange: the reference sale earned 18 points.
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);
        await _service.AwardForOrderAsync(order);
        var firstReturn = Guid.NewGuid();
        var secondReturn = Guid.NewGuid();

        // Act: one item back (900,000 goods kept), then the other (0 kept).
        var first = await _service.ReverseForReturnAsync(order, firstReturn, remainingGoodsAmount: 900_000m);
        var second = await _service.ReverseForReturnAsync(order, secondReturn, remainingGoodsAmount: 0m);

        // Assert
        first.Outcome.Should().Be(LoyaltyOutcome.Applied);
        first.PointsChange.Should().Be(-9);
        first.BalanceAfter.Should().Be(9);
        second.PointsChange.Should().Be(-9);
        second.BalanceAfter.Should().Be(0);
        customer.LoyaltyPoints.Should().Be(0);
        _loyalty.Entries.Select(e => e.ReferenceKey).Should().Contain($"reverse:{firstReturn}").And.Contain($"reverse:{secondReturn}");
    }

    [Fact]
    public async Task ReverseForReturnAsync_SameReturnTwice_ChangesNothingTheSecondTime()
    {
        // Arrange
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);
        await _service.AwardForOrderAsync(order);
        var returnId = Guid.NewGuid();
        await _service.ReverseForReturnAsync(order, returnId, 900_000m);

        // Act
        var repeat = await _service.ReverseForReturnAsync(order, returnId, 900_000m);

        // Assert
        repeat.Outcome.Should().Be(LoyaltyOutcome.AlreadyApplied);
        customer.LoyaltyPoints.Should().Be(9);
        _loyalty.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReverseForReturnAsync_OrderThatNeverEarnedPoints_DeductsNothing()
    {
        // Arrange: a Processing order refunded before completion earned no points.
        var customer = AddCustomer(points: 100);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 0m);
        order.Status = OrderStatus.Processing;

        // Act
        var change = await _service.ReverseForReturnAsync(order, Guid.NewGuid(), remainingGoodsAmount: 0m);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.NoPoints);
        customer.LoyaltyPoints.Should().Be(100);
        _loyalty.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReverseForReturnAsync_BalanceSmallerThanDeduction_NeverGoesNegative()
    {
        // Arrange: the order earned 18 but the balance was reduced elsewhere to 5.
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);
        await _service.AwardForOrderAsync(order);
        customer.LoyaltyPoints = 5;

        // Act
        var change = await _service.ReverseForReturnAsync(order, Guid.NewGuid(), remainingGoodsAmount: 0m);

        // Assert
        change.PointsChange.Should().Be(-5);
        customer.LoyaltyPoints.Should().Be(0);
        change.BalanceAfter.Should().Be(0);
    }

    [Fact]
    public async Task ReverseForReturnAsync_DropsTierWhenPointsFallBelowThreshold()
    {
        // Arrange: 495 points plus 10 earned by this order = 505 (Silver).
        var customer = AddCustomer(points: 495);
        var order = CompletedOrder(customer.Id, subtotal: 1_000_000m, discount: 0m);
        await _service.AwardForOrderAsync(order);
        customer.Tier.Should().Be(CustomerTier.Silver);

        // Act
        await _service.ReverseForReturnAsync(order, Guid.NewGuid(), remainingGoodsAmount: 0m);

        // Assert
        customer.LoyaltyPoints.Should().Be(495);
        customer.Tier.Should().Be(CustomerTier.Standard);
    }

    [Fact]
    public async Task ReverseForReturnAsync_RemainingGoodsStillEarnSamePoints_DeductsNothing()
    {
        // Arrange
        var customer = AddCustomer(points: 0);
        var order = CompletedOrder(customer.Id, subtotal: 2_000_000m, discount: 200_000m);
        await _service.AwardForOrderAsync(order);

        // Act: nothing leaves, the whole 1,800,000 is still kept.
        var change = await _service.ReverseForReturnAsync(order, Guid.NewGuid(), remainingGoodsAmount: 1_800_000m);

        // Assert
        change.Outcome.Should().Be(LoyaltyOutcome.NoPoints);
        customer.LoyaltyPoints.Should().Be(18);
    }

    [Fact]
    public async Task GetSummaryAsync_SilverCustomer_ShowsNextTierAndMissingPoints()
    {
        // Arrange
        var customer = AddCustomer(points: 700);

        // Act
        var result = await _service.GetSummaryAsync(customer.Id, RoleType.Manager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Points.Should().Be(700);
        result.Data.Tier.Should().Be("Silver");
        result.Data.TierDiscountPercent.Should().Be(2);
        result.Data.NextTier.Should().Be("Gold");
        result.Data.PointsToNextTier.Should().Be(800);
    }

    [Fact]
    public async Task GetSummaryAsync_PlatinumCustomer_HasNoNextTier()
    {
        // Arrange
        var customer = AddCustomer(points: 6_000);

        // Act
        var result = await _service.GetSummaryAsync(customer.Id, RoleType.Admin);

        // Assert
        result.Data!.NextTier.Should().BeNull();
        result.Data.PointsToNextTier.Should().BeNull();
        result.Data.TierDiscountPercent.Should().Be(10);
    }

    [Fact]
    public async Task GetSummaryAsync_UnknownCustomer_ReturnsNotFound()
    {
        // Act
        var result = await _service.GetSummaryAsync(Guid.NewGuid(), RoleType.Admin);

        // Assert
        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task GetSummaryAsync_InactiveCustomerForSales_ReturnsNotFound()
    {
        // Arrange
        var customer = AddCustomer(points: 10);
        customer.IsActive = false;

        // Act
        var forSales = await _service.GetSummaryAsync(customer.Id, RoleType.SalesStaff);
        var forManager = await _service.GetSummaryAsync(customer.Id, RoleType.Manager);

        // Assert
        forSales.Found.Should().BeFalse();
        forManager.IsSuccess.Should().BeTrue();
    }

    private Customer AddCustomer(int points)
    {
        var customer = new Customer
        {
            FullName = "Khách A",
            Phone = "09" + Random.Shared.Next(10_000_000, 99_999_999),
            LoyaltyPoints = points,
            Tier = LoyaltyRules.TierFor(points)
        };
        _loyalty.Customers[customer.Id] = customer;
        _customers.Customers[customer.Id] = customer;
        return customer;
    }

    private static Order CompletedOrder(Guid? customerId, decimal subtotal, decimal discount) => new()
    {
        OrderCode = "ORD-202611-0001",
        CustomerId = customerId,
        Status = OrderStatus.Completed,
        Subtotal = subtotal,
        DiscountAmount = discount,
        TotalAmount = subtotal - discount
    };
}
