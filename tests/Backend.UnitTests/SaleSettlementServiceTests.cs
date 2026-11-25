using FluentAssertions;
using TechStore.Api.Customers.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class SaleSettlementServiceTests
{
    private static readonly DateTime PaidAt = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly FakeInventoryFulfillmentService _fulfillment = new();
    private readonly FakeVoucherService _vouchers = new();
    private readonly FakeRefundLoyalty _loyalty = new();
    private readonly SaleSettlementService _service;

    public SaleSettlementServiceTests() => _service = new SaleSettlementService(_fulfillment, _vouchers, _loyalty);

    [Fact]
    public async Task CompleteAsync_CashSale_ConfirmsStockCreditsTheCashTotalAndCompletesTheOrder()
    {
        // Arrange
        var order = NewOrder(total: 10_000_000m);
        var shift = new PosSession { Status = PosSessionStatus.Open };

        // Act
        var result = await _service.CompleteAsync(order, shift, PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Completed);
        order.PaymentMethod.Should().Be(PaymentMethod.Cash);
        order.PaidAt.Should().Be(PaidAt);
        order.CompletedAt.Should().Be(PaidAt);
        shift.CashSalesTotal.Should().Be(10_000_000m);
        shift.VietQrSalesTotal.Should().Be(0m);
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _fulfillment.LastPerformedBy.Should().Be(Actor);
        _fulfillment.LastCompletedAt.Should().Be(PaidAt);
    }

    [Fact]
    public async Task CompleteAsync_QrSale_CreditsTheQrTotalAndLeavesTheCashTotal()
    {
        // Arrange
        var order = NewOrder(total: 3_000_000m);
        var shift = new PosSession { Status = PosSessionStatus.Open, CashSalesTotal = 500_000m };

        // Act
        await _service.CompleteAsync(order, shift, PaymentMethod.VietQr, PaidAt, Actor);

        // Assert
        shift.VietQrSalesTotal.Should().Be(3_000_000m);
        shift.CashSalesTotal.Should().Be(500_000m);
        order.PaymentMethod.Should().Be(PaymentMethod.VietQr);
    }

    [Fact]
    public async Task CompleteAsync_WithVoucher_ConsumesTheHeldUseOnce()
    {
        // Arrange
        var order = NewOrder(total: 1_000_000m);
        order.VoucherId = Guid.NewGuid();

        // Act
        await _service.CompleteAsync(order, new PosSession(), PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        _vouchers.RedeemCount.Should().Be(1);
        _vouchers.CallLog.Should().Equal("redeem");
    }

    [Fact]
    public async Task CompleteAsync_WithoutVoucher_DoesNotTouchVouchers()
    {
        // Act
        await _service.CompleteAsync(NewOrder(total: 1m), new PosSession(), PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        _vouchers.CallLog.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_VoucherUseCannotBeConsumed_FailsAndLeavesTheOrderAndShiftUntouched()
    {
        // Arrange
        _vouchers.RedeemResult = new VoucherUseResult(VoucherUseOutcome.Refused, "The hold was already returned.");
        var order = NewOrder(total: 1_000_000m);
        order.VoucherId = Guid.NewGuid();
        var shift = new PosSession();

        // Act
        var result = await _service.CompleteAsync(order, shift, PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Conflict.Should().Contain("already returned");
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaidAt.Should().BeNull();
        shift.CashSalesTotal.Should().Be(0m);
        _loyalty.Earned.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_StockConfirmationFails_FailsBeforeTouchingVoucherPointsOrShift()
    {
        // Arrange
        _fulfillment.ConfirmConflict = new FulfillmentProblem(0, "Device is not reserved.");
        var order = NewOrder(total: 1_000_000m);
        order.VoucherId = Guid.NewGuid();
        order.CustomerId = Guid.NewGuid();
        var shift = new PosSession();

        // Act
        var result = await _service.CompleteAsync(order, shift, PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Conflict.Should().Contain("Line 1");
        _vouchers.CallLog.Should().BeEmpty();
        _loyalty.Earned.Should().BeEmpty();
        shift.CashSalesTotal.Should().Be(0m);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task CompleteAsync_OrderWithCustomer_EarnsPointsOnTheNetGoodsAmount()
    {
        // Arrange
        var order = NewOrder(total: 1_100_000m);
        order.Subtotal = 1_200_000m;
        order.DiscountAmount = 200_000m;
        order.CustomerId = Guid.NewGuid();

        // Act
        await _service.CompleteAsync(order, new PosSession(), PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        _loyalty.Earned.Should().ContainSingle().Which.NetAmount.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task CompleteAsync_StoresTheCostReturnedByInventoryOnEachLine()
    {
        // Arrange
        var order = NewOrder(total: 1m);
        var item = order.Items.Single();
        _fulfillment.CostsByOrderItem[item.Id] = 600_000m;

        // Act
        await _service.CompleteAsync(order, new PosSession(), PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        item.UnitCost.Should().Be(600_000m);
    }

    [Fact]
    public async Task CompleteAsync_AlreadyCompleted_ChangesNothing()
    {
        // Arrange
        var order = NewOrder(total: 1_000_000m);
        order.Status = OrderStatus.Completed;
        var shift = new PosSession();

        // Act
        var result = await _service.CompleteAsync(order, shift, PaymentMethod.Cash, PaidAt, Actor);

        // Assert
        result.IsSuccess.Should().BeTrue();
        shift.CashSalesTotal.Should().Be(0m);
        _fulfillment.CallLog.Should().BeEmpty();
        _vouchers.CallLog.Should().BeEmpty();
    }

    private static Order NewOrder(decimal total)
    {
        var order = new Order { OrderCode = "ORD-1", Subtotal = total, TotalAmount = total, Status = OrderStatus.Pending };
        order.Items.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "iPhone",
            Sku = "IP15",
            Quantity = 1,
            UnitPrice = total,
            TotalPrice = total
        });
        return order;
    }
}
