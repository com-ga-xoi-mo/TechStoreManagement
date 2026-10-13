using FluentAssertions;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

// UC-ORD-01: CreateAsync
public partial class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_WalkInOrderWithVariant_CreatesPendingOrderPricedFromVariant()
    {
        // Arrange
        var product = AddProduct();
        var variant = AddVariant(product, 29_990_000m);
        var request = RequestOf(TrackedItem(product, variant));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.Status.Should().Be("Pending");
        dto.PaymentMethod.Should().Be("Cash");
        dto.CustomerId.Should().BeNull();
        dto.OrderCode.Should().Be("ORD-202610-0001");
        dto.CashierUserId.Should().Be(ActingUserId);
        dto.Subtotal.Should().Be(29_990_000m);
        dto.DiscountAmount.Should().Be(0m);
        dto.TaxAmount.Should().Be(0m);
        dto.TotalAmount.Should().Be(29_990_000m);
        dto.PaidAt.Should().BeNull();
        dto.CompletedAt.Should().BeNull();
        dto.Items.Should().ContainSingle().Which.UnitPrice.Should().Be(29_990_000m);
        _orders.AddedOrders.Should().ContainSingle();
        _orders.AddedOrders[0].PosSessionId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Success_UsesOneTransactionOneSaveAndNoReservation()
    {
        // Arrange
        var product = AddProduct();
        var request = RequestOf(TrackedItem(product));

        // Act
        await _service.CreateAsync(request, ActingUserId);

        // Assert
        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
        _unitOfWork.RollbackCount.Should().Be(0);
        _fulfillment.CheckCallCount.Should().Be(1);
        _fulfillment.ReserveCallCount.Should().Be(0);
        _fulfillment.ConfirmCallCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_NonTrackedLineWithDiscount_PricesFromBasePrice()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 350_000m, name: "Cable", sku: "CAB1");
        var request = RequestOf(new CreateOrderItemRequest { ProductId = product.Id, Quantity = 3, DiscountAmount = 50_000m });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Should().ContainSingle().Subject;
        item.UnitPrice.Should().Be(350_000m);
        item.Quantity.Should().Be(3);
        item.DiscountAmount.Should().Be(50_000m);
        item.TotalPrice.Should().Be(1_000_000m);
        result.Data.TotalAmount.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task CreateAsync_Lines_SnapshotNamesAndVariantSku()
    {
        // Arrange
        var product = AddProduct(name: "iPhone 15 Pro", sku: "IP15P");
        var variant = AddVariant(product, 29_990_000m, sku: "IP15P-256-NT", name: "Natural Titanium 256GB");
        var request = RequestOf(TrackedItem(product, variant));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        var stored = _orders.AddedOrders.Single().Items.Single();
        stored.ProductName.Should().Be("iPhone 15 Pro");
        stored.VariantName.Should().Be("Natural Titanium 256GB");
        stored.Sku.Should().Be("IP15P-256-NT");
        stored.UnitCost.Should().BeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_PriceComesFromTheCatalog_NotFromTheRequest()
    {
        // Arrange: the request type has no price properties, so only catalog prices can be used
        var product = AddProduct(basePrice: 12_345_000m);
        var request = RequestOf(TrackedItem(product));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.Data!.TotalAmount.Should().Be(12_345_000m);
        typeof(CreateOrderItemRequest).GetProperties().Select(p => p.Name)
            .Should().NotContain(["UnitPrice", "TotalPrice", "TotalAmount"]);
    }

    [Fact]
    public async Task CreateAsync_ExistingActiveCustomer_IsLinkedToOrder()
    {
        // Arrange
        var customer = AddCustomer();
        var product = AddProduct();
        var request = RequestOf(TrackedItem(product));
        request.CustomerId = customer.Id;

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.CustomerId.Should().Be(customer.Id);
    }

    [Fact]
    public async Task CreateAsync_UnknownCustomer_ReportsCustomerIdAndStoresNothing()
    {
        // Arrange
        var product = AddProduct();
        var request = RequestOf(TrackedItem(product));
        request.CustomerId = Guid.NewGuid();

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("customerId");
        _orders.AddedOrders.Should().BeEmpty();
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_InactiveCustomer_ReportsCustomerId()
    {
        // Arrange
        var customer = AddCustomer(active: false);
        var product = AddProduct();
        var request = RequestOf(TrackedItem(product));
        request.CustomerId = customer.Id;

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("customerId");
    }

    [Fact]
    public async Task CreateAsync_PaymentMethodAndNotes_AreParsedAndTrimmed()
    {
        // Arrange
        var product = AddProduct();
        var request = RequestOf(TrackedItem(product));
        request.PaymentMethod = "vietqr";
        request.Notes = "  gift wrap  ";

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.Data!.PaymentMethod.Should().Be("VietQr");
        result.Data.Notes.Should().Be("gift wrap");
    }

    [Fact]
    public async Task CreateAsync_UnknownProduct_ReportsProductId()
    {
        // Arrange
        var request = RequestOf(new CreateOrderItemRequest { ProductId = Guid.NewGuid(), SerialImeiId = Guid.NewGuid() });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].productId");
    }

    [Fact]
    public async Task CreateAsync_InactiveProduct_ReportsProductId()
    {
        // Arrange
        var product = AddProduct(active: false);
        var request = RequestOf(TrackedItem(product));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].productId");
    }

    [Fact]
    public async Task CreateAsync_ProductWithVariantsButNoVariantSelected_ReportsVariantId()
    {
        // Arrange
        var product = AddProduct();
        AddVariant(product, 29_990_000m);
        var request = RequestOf(TrackedItem(product));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].variantId");
    }

    [Fact]
    public async Task CreateAsync_VariantOfAnotherProduct_ReportsVariantId()
    {
        // Arrange
        var product = AddProduct();
        AddVariant(product, 29_990_000m);
        var other = AddProduct(name: "Other", sku: "OTH");
        var foreignVariant = AddVariant(other, 1m, sku: "OTH-1");
        var request = RequestOf(TrackedItem(product, foreignVariant));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].variantId");
    }

    [Fact]
    public async Task CreateAsync_InactiveVariant_ReportsVariantId()
    {
        // Arrange
        var product = AddProduct();
        var variant = AddVariant(product, 29_990_000m, active: false);
        var request = RequestOf(TrackedItem(product, variant));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].variantId");
    }

    [Fact]
    public async Task CreateAsync_VariantSuppliedForProductWithoutVariants_ReportsVariantId()
    {
        // Arrange
        var product = AddProduct();
        var request = RequestOf(new CreateOrderItemRequest
        {
            ProductId = product.Id,
            VariantId = Guid.NewGuid(),
            SerialImeiId = Guid.NewGuid()
        });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].variantId");
    }

    [Fact]
    public async Task CreateAsync_SerialTrackedLineWithoutDevice_ReportsSerialImeiId()
    {
        // Arrange
        var product = AddProduct();
        var request = RequestOf(new CreateOrderItemRequest { ProductId = product.Id });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].serialImeiId");
    }

    [Fact]
    public async Task CreateAsync_SerialTrackedLineWithQuantityTwo_ReportsQuantity()
    {
        // Arrange
        var product = AddProduct();
        var item = TrackedItem(product);
        item.Quantity = 2;

        // Act
        var result = await _service.CreateAsync(RequestOf(item), ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].quantity");
    }

    [Fact]
    public async Task CreateAsync_NonTrackedLineWithDevice_ReportsSerialImeiId()
    {
        // Arrange
        var product = AddProduct(serialTracked: false);
        var request = RequestOf(new CreateOrderItemRequest { ProductId = product.Id, SerialImeiId = Guid.NewGuid() });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].serialImeiId");
    }

    [Fact]
    public async Task CreateAsync_SameNonTrackedProductTwice_ReportsSecondLine()
    {
        // Arrange
        var product = AddProduct(serialTracked: false);
        var request = RequestOf(
            new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 },
            new CreateOrderItemRequest { ProductId = product.Id, Quantity = 2 });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[1].productId");
        result.ValidationErrors.Should().NotContainKey("items[0].productId");
    }

    [Fact]
    public async Task CreateAsync_DiscountLargerThanLine_ReportsDiscountAmount()
    {
        // Arrange
        var product = AddProduct(basePrice: 1_000m);
        var item = TrackedItem(product);
        item.DiscountAmount = 1_001m;

        // Act
        var result = await _service.CreateAsync(RequestOf(item), ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].discountAmount");
    }

    [Fact]
    public async Task CreateAsync_DeviceAlreadySold_ReportsSerialImeiIdAndStoresNothing()
    {
        // Arrange
        var product = AddProduct();
        _fulfillment.CheckProblems.Add(new FulfillmentProblem(0, "Device is Sold; expected InStock."));
        var request = RequestOf(TrackedItem(product));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("items[0].serialImeiId");
        _orders.AddedOrders.Should().BeEmpty();
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_InsufficientStockForNonTrackedLine_ReportsQuantity()
    {
        // Arrange
        var product = AddProduct(serialTracked: false);
        _fulfillment.CheckProblems.Add(
            new FulfillmentProblem(0, "Insufficient available stock (1 available).", FulfillmentProblemKind.InsufficientStock));
        var request = RequestOf(new CreateOrderItemRequest { ProductId = product.Id, Quantity = 5 });

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].quantity");
    }

    [Fact]
    public async Task CreateAsync_MissingStockRow_ReportsProductId()
    {
        // Arrange
        var product = AddProduct();
        _fulfillment.CheckProblems.Add(
            new FulfillmentProblem(0, "Stock is not initialized for this product or variant.", FulfillmentProblemKind.StockNotInitialized));
        var request = RequestOf(TrackedItem(product));

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].productId");
    }

    [Fact]
    public async Task CreateAsync_SeveralProblems_AreReportedTogether()
    {
        // Arrange
        var product = AddProduct();
        var item = TrackedItem(product);
        item.DiscountAmount = -1m;
        var request = RequestOf(item);
        request.CustomerId = Guid.NewGuid();
        request.PaymentMethod = "Bitcoin";

        // Act
        var result = await _service.CreateAsync(request, ActingUserId);

        // Assert
        result.ValidationErrors!.Keys.Should().BeEquivalentTo("customerId", "paymentMethod", "items[0].discountAmount");
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ReportsItems()
    {
        // Act
        var result = await _service.CreateAsync(new CreateOrderRequest { Items = [] }, ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items");
    }

    [Fact]
    public async Task CreateAsync_SequenceContinuesFromLastCodeOfTheMonth()
    {
        // Arrange
        _orders.LastOrderCode = "ORD-202610-0007";
        var product = AddProduct();

        // Act
        var result = await _service.CreateAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Assert
        result.Data!.OrderCode.Should().Be("ORD-202610-0008");
    }

    [Fact]
    public async Task CreateAsync_CodeCollisionOnce_RetriesWithFreshCode()
    {
        // Arrange
        var product = AddProduct();
        _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("idx_orders_code"));

        // Act
        var result = await _service.CreateAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.OrderCode.Should().Be("ORD-202610-0002");
        _unitOfWork.BeginTransactionCount.Should().Be(2);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_CodeCollisionEveryAttempt_ReturnsConflict()
    {
        // Arrange
        var product = AddProduct();
        for (var i = 0; i < 5; i++)
        {
            _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("idx_orders_code"));
        }

        // Act
        var result = await _service.CreateAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        _unitOfWork.RollbackCount.Should().Be(5);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_OtherDuplicateKey_ReturnsConflictWithoutRetry()
    {
        // Arrange
        var product = AddProduct();
        _unitOfWork.SaveExceptionQueue.Enqueue(new DuplicateKeyException("uq_order_items_order_serial"));

        // Act
        var result = await _service.CreateAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_UnexpectedSaveFailure_RollsBackAndRethrows()
    {
        // Arrange
        var product = AddProduct();
        _unitOfWork.SaveExceptionQueue.Enqueue(new InvalidOperationException("boom"));

        // Act
        var act = () => _service.CreateAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.RollbackCount.Should().Be(1);
    }
}
