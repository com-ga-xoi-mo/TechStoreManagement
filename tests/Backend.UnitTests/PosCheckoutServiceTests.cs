using FluentAssertions;
using Microsoft.Extensions.Options;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class PosCheckoutServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
    private static readonly AuthenticatedUser SalesA = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), RoleType.SalesStaff, 0);
    private static readonly AuthenticatedUser SalesB = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), RoleType.SalesStaff, 0);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeProductRepository _products = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakePosSessionRepository _sessions = new();
    private readonly FakeInventoryFulfillmentService _fulfillment = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    // These tests check the cash flow with round amounts, so VAT is 0 here; the formula itself is covered by
    // OrderPricingHelperTests and the VAT path through checkout by CheckoutAsync_WithVat_ChargesAndCreditsTheTaxedTotal.
    private readonly PricingOptions _pricing = new() { VatRatePercent = 0m };
    private readonly PosCheckoutService _service;
    private readonly PosSession _shift;

    public PosCheckoutServiceTests()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        var orderService = new OrderService(_orders, _products, _customers, _fulfillment, _unitOfWork, time, Options.Create(_pricing));
        _service = new PosCheckoutService(orderService, _orders, _sessions, _fulfillment, _unitOfWork, time);
        _shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
    }

    [Fact]
    public async Task CheckoutAsync_ExactCash_CompletesOrderCreditsShiftAndSellsStockInOneTransaction()
    {
        // Arrange
        var product = AddProduct(price: 10_000_000m);
        var request = RequestFor(product, tendered: 10_000_000m);

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Replayed.Should().BeFalse();
        var order = result.Data.Order;
        order.Status.Should().Be("Completed");
        order.PaymentMethod.Should().Be("Cash");
        order.TotalAmount.Should().Be(10_000_000m);
        order.CashTendered.Should().Be(10_000_000m);
        order.ChangeGiven.Should().Be(0m);
        order.PosSessionId.Should().Be(_shift.Id);
        order.PaidAt.Should().Be(Now);
        order.CompletedAt.Should().Be(Now);
        order.CashierUserId.Should().Be(SalesA.Id);
        _shift.CashSalesTotal.Should().Be(10_000_000m);
        _fulfillment.CallLog.Should().Equal("reserve", "confirm");
        _fulfillment.LastConfirmedLines!.Should().OnlyContain(l => l.OrderItemId.HasValue);
        _fulfillment.LastPerformedBy.Should().Be(SalesA.Id);
        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
        _unitOfWork.RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckoutAsync_CashAboveTotal_ReturnsChangeAndCreditsOnlyTheTotal()
    {
        // Arrange
        var product = AddProduct(price: 9_500_000m);

        // Act
        var result = await _service.CheckoutAsync(RequestFor(product, tendered: 10_000_000m), SalesA);

        // Assert
        result.Data!.Order.ChangeGiven.Should().Be(500_000m);
        _shift.CashSalesTotal.Should().Be(9_500_000m);
    }

    [Fact]
    public async Task CheckoutAsync_WithVat_ChargesAndCreditsTheTaxedTotal()
    {
        // Arrange: 10% VAT on a 10,000,000 catalog price (prices exclude VAT).
        _pricing.VatRatePercent = 10m;
        var product = AddProduct(price: 10_000_000m);

        // Act
        var short_ = await _service.CheckoutAsync(RequestFor(product, tendered: 10_999_999m), SalesA);
        var paid = await _service.CheckoutAsync(RequestFor(product, tendered: 11_000_000m), SalesA);

        // Assert
        short_.ValidationErrors.Should().ContainKey("cashTendered");
        var order = paid.Data!.Order;
        order.Subtotal.Should().Be(10_000_000m);
        order.TaxRate.Should().Be(10m);
        order.TaxAmount.Should().Be(1_000_000m);
        order.TotalAmount.Should().Be(11_000_000m);
        order.ChangeGiven.Should().Be(0m);
        order.Items.Single().TaxAmount.Should().Be(1_000_000m);
        _shift.CashSalesTotal.Should().Be(11_000_000m);
    }

    [Fact]
    public async Task CheckoutAsync_SalesLineDiscountAboveLimit_IsRejected()
    {
        // Arrange: the Sales discount limit also applies at the counter.
        var product = AddProduct(serialTracked: false, price: 1_000_000m, sku: "CASE");
        var request = RequestFor(product, tendered: 1_000_000m);
        request.Items![0].DiscountAmount = 100_001m;

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKey("discount");
        AssertNothingStored();
    }

    [Fact]
    public async Task CheckoutAsync_CashBelowTotal_ReturnsValidationErrorAndTouchesNothing()
    {
        // Arrange
        var product = AddProduct(price: 10_000_000m);

        // Act
        var result = await _service.CheckoutAsync(RequestFor(product, tendered: 9_999_999m), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("cashTendered");
        AssertNothingStored();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.005)]
    public async Task CheckoutAsync_InvalidCashAmount_ReturnsValidationError(double tendered)
    {
        // Arrange
        var product = AddProduct();

        // Act
        var result = await _service.CheckoutAsync(RequestFor(product, tendered: (decimal)tendered), SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKey("cashTendered");
        AssertNothingStored();
    }

    [Fact]
    public async Task CheckoutAsync_MissingKeyOrShift_ReturnsValidationErrors()
    {
        // Arrange
        var request = RequestFor(AddProduct(), tendered: 30_000_000m);
        request.CheckoutKey = null;
        request.PosSessionId = Guid.Empty;

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKeys("checkoutKey", "posSessionId");
        AssertNothingStored();
    }

    [Fact]
    public async Task CheckoutAsync_EmptyCart_ReturnsValidationErrorAndTouchesNothing()
    {
        // Arrange
        var request = new PosCheckoutRequest
        {
            PosSessionId = _shift.Id, CheckoutKey = Guid.NewGuid(), CashTendered = 1m, Items = []
        };

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        AssertNothingStored();
    }

    [Fact]
    public async Task CheckoutAsync_SameDeviceTwice_ReturnsValidationErrorOnSecondLine()
    {
        // Arrange
        var product = AddProduct();
        var serial = Guid.NewGuid();
        var request = RequestFor(product, tendered: 99_000_000m);
        request.Items = [Tracked(product, serial), Tracked(product, serial)];

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[1].serialImeiId");
        AssertNothingStored();
    }

    [Fact]
    public async Task CheckoutAsync_InactiveCustomer_ReturnsValidationError()
    {
        // Arrange
        var customer = new Customer { FullName = "Nguyen Van A", Phone = "0901234567", IsActive = false };
        _customers.Customers[customer.Id] = customer;
        var request = RequestFor(AddProduct(), tendered: 30_000_000m);
        request.CustomerId = customer.Id;

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKey("customerId");
        AssertNothingStored();
    }

    [Fact]
    public async Task CheckoutAsync_ClosedShift_ReturnsConflictAndStoresNothing()
    {
        // Arrange
        _shift.Status = PosSessionStatus.Closed;

        // Act
        var result = await _service.CheckoutAsync(RequestFor(AddProduct(), tendered: 30_000_000m), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _orders.AddedOrders.Should().BeEmpty();
        _fulfillment.ReserveCallCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckoutAsync_AnotherCashiersShift_ReturnsNotFoundAndStoresNothing()
    {
        // Act
        var result = await _service.CheckoutAsync(RequestFor(AddProduct(), tendered: 30_000_000m), SalesB);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
        _orders.AddedOrders.Should().BeEmpty();
        _shift.CashSalesTotal.Should().Be(0m);
    }

    [Fact]
    public async Task CheckoutAsync_UnknownShift_ReturnsNotFound()
    {
        // Arrange
        var request = RequestFor(AddProduct(), tendered: 30_000_000m);
        request.PosSessionId = Guid.NewGuid();

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
    }

    [Fact]
    public async Task CheckoutAsync_SameKeyTwice_ReturnsStoredOrderAndAppliesNothingTwice()
    {
        // Arrange
        var product = AddProduct(price: 10_000_000m);
        var request = RequestFor(product, tendered: 10_000_000m);

        // Act
        var first = await _service.CheckoutAsync(request, SalesA);
        var second = await _service.CheckoutAsync(request, SalesA);

        // Assert
        second.IsSuccess.Should().BeTrue();
        second.Data!.Replayed.Should().BeTrue();
        second.Data.Order.Id.Should().Be(first.Data!.Order.Id);
        _orders.AddedOrders.Should().ContainSingle();
        _shift.CashSalesTotal.Should().Be(10_000_000m);
        _fulfillment.ReserveCallCount.Should().Be(1);
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckoutAsync_SameKeyFromAnotherCashier_IsNotAReplay()
    {
        // Arrange
        var other = _sessions.AddSession(SalesB.Id);
        var key = Guid.NewGuid();
        var product = AddProduct(price: 1_000_000m);
        var first = RequestFor(product, tendered: 1_000_000m);
        first.CheckoutKey = key;
        var second = RequestFor(product, tendered: 1_000_000m);
        second.CheckoutKey = key;
        second.PosSessionId = other.Id;

        // Act
        await _service.CheckoutAsync(first, SalesA);
        var result = await _service.CheckoutAsync(second, SalesB);

        // Assert
        result.Data!.Replayed.Should().BeFalse();
        _orders.AddedOrders.Should().HaveCount(2);
    }

    [Fact]
    public async Task CheckoutAsync_CheckoutKeyIndexLostRace_ReturnsTheStoredOrder()
    {
        // Arrange: the order carrying the key is already visible when the unique index fires.
        _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("uq_orders_cashier_checkout_key"));
        var product = AddProduct(price: 1_000_000m);

        // Act
        var result = await _service.CheckoutAsync(RequestFor(product, tendered: 1_000_000m), SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Replayed.Should().BeTrue();
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckoutAsync_DeviceNoLongerAvailable_ReturnsConflictNamingTheLineAndCreditsNothing()
    {
        // Arrange
        _fulfillment.ReserveResult = FulfillmentResult.Failed(new FulfillmentProblem(0, "Device is not in stock."));

        // Act
        var result = await _service.CheckoutAsync(RequestFor(AddProduct(), tendered: 30_000_000m), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        result.ConflictReason.Should().Contain("Line 1").And.Contain("not in stock");
        _shift.CashSalesTotal.Should().Be(0m);
        _fulfillment.ConfirmCallCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckoutAsync_SaleConfirmationFails_RollsBackAndCreditsNothing()
    {
        // Arrange
        _fulfillment.ConfirmConflict = new FulfillmentProblem(0, "Device is not reserved.");

        // Act
        var result = await _service.CheckoutAsync(RequestFor(AddProduct(), tendered: 30_000_000m), SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _shift.CashSalesTotal.Should().Be(0m);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckoutAsync_DatabaseFailsOnSave_RollsBackAndRethrows()
    {
        // Arrange
        _unitOfWork.ExceptionToThrowOnSave = new InvalidOperationException("disk full");

        // Act
        var act = () => _service.CheckoutAsync(RequestFor(AddProduct(), tendered: 30_000_000m), SalesA);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckoutAsync_OrderCodeCollision_RetriesInANewTransaction()
    {
        // Arrange
        _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("idx_orders_code"));
        _unitOfWork.OnRollback = () => { _orders.Orders.Clear(); _orders.AddedOrders.Clear(); };
        var product = AddProduct(price: 1_000_000m);

        // Act
        var result = await _service.CheckoutAsync(RequestFor(product, tendered: 1_000_000m), SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.BeginTransactionCount.Should().Be(2);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckoutAsync_NonTrackedProductWithQuantity_PricesFromCatalogAndKeepsLineDiscount()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, price: 350_000m, sku: "CASE1");
        var request = RequestFor(product, tendered: 1_000_000m);
        request.Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 3, DiscountAmount = 50_000m }];

        // Act
        var result = await _service.CheckoutAsync(request, SalesA);

        // Assert
        result.Data!.Order.TotalAmount.Should().Be(1_000_000m);
        result.Data.Order.ChangeGiven.Should().Be(0m);
        _shift.CashSalesTotal.Should().Be(1_000_000m);
    }

    private void AssertNothingStored()
    {
        _orders.AddedOrders.Should().BeEmpty();
        _shift.CashSalesTotal.Should().Be(0m);
        _fulfillment.CallLog.Should().BeEmpty();
        _unitOfWork.BeginTransactionCount.Should().Be(0);
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    private Product AddProduct(bool serialTracked = true, decimal price = 30_000_000m, string sku = "IP15P")
    {
        var product = new Product
        {
            Name = "iPhone 15 Pro",
            Sku = sku,
            BasePrice = price,
            CostPrice = price / 2,
            IsSerialTracked = serialTracked,
            IsActive = true
        };
        _products.Products[product.Id] = product;
        return product;
    }

    private static CreateOrderItemRequest Tracked(Product product, Guid serial) => new()
    {
        ProductId = product.Id,
        SerialImeiId = serial
    };

    private PosCheckoutRequest RequestFor(Product product, decimal tendered) => new()
    {
        PosSessionId = _shift.Id,
        CheckoutKey = Guid.NewGuid(),
        CashTendered = tendered,
        Items = [product.IsSerialTracked ? Tracked(product, Guid.NewGuid()) : new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 }]
    };
}
