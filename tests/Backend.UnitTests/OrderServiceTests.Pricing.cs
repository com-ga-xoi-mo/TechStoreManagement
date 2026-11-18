using FluentAssertions;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// M3 pricing in CreateAsync: order discount, VAT, line allocation and discount permission.
public partial class OrderServiceTests
{
    private static readonly AuthenticatedUser SalesUser = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), RoleType.SalesStaff, 0);

    private static CreateOrderItemRequest QuantityItem(Guid productId, int quantity, decimal discount = 0m) => new()
    {
        ProductId = productId,
        Quantity = quantity,
        DiscountAmount = discount
    };

    [Fact]
    public async Task CreateAsync_OrderDiscountAndVat_StoresTotalsRateAndLineShares()
    {
        // Arrange: the M4 reconciliation example, 2 x 1,000,000, order discount 200,000, VAT 10%.
        var first = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe A", sku: "TN-A");
        var second = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe B", sku: "TN-B");
        var request = RequestOf(QuantityItem(first.Id, 1), QuantityItem(second.Id, 1));
        request.OrderDiscountAmount = 200_000m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.Subtotal.Should().Be(2_000_000m);
        dto.DiscountAmount.Should().Be(200_000m);
        dto.TaxRate.Should().Be(10m);
        dto.TaxAmount.Should().Be(180_000m);
        dto.TotalAmount.Should().Be(1_980_000m);
        dto.Items.Should().OnlyContain(i => i.OrderDiscountAmount == 100_000m && i.TaxAmount == 90_000m);
        var stored = _orders.AddedOrders.Single();
        stored.TaxRate.Should().Be(10m);
        stored.Items.Sum(i => i.OrderDiscountAmount).Should().Be(stored.DiscountAmount);
        stored.Items.Sum(i => i.TaxAmount).Should().Be(stored.TaxAmount);
    }

    [Fact]
    public async Task CreateAsync_LineAndOrderDiscount_DoesNotSubtractLineDiscountTwice()
    {
        // Arrange: 3 x 350,000 with a 50,000 line discount, then 100,000 off the order.
        var product = AddProduct(serialTracked: false, basePrice: 350_000m, name: "Chuột", sku: "MOUSE");
        var request = RequestOf(QuantityItem(product.Id, 3, 50_000m));
        request.OrderDiscountAmount = 100_000m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        var dto = result.Data!;
        dto.Items.Single().TotalPrice.Should().Be(1_000_000m);
        dto.Subtotal.Should().Be(1_000_000m);
        dto.DiscountAmount.Should().Be(100_000m);
        dto.TaxAmount.Should().Be(90_000m);
        dto.TotalAmount.Should().Be(990_000m);
    }

    [Fact]
    public async Task CreateAsync_UsesTheConfiguredRateAtCreationTime()
    {
        // Arrange
        _pricing.VatRatePercent = 8m;
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, sku: "CABLE");

        // Act
        var result = await _service.CreateAsync(RequestOf(QuantityItem(product.Id, 1)), ActingUser);

        // Assert
        result.Data!.TaxRate.Should().Be(8m);
        result.Data.TaxAmount.Should().Be(80_000m);
        result.Data.TotalAmount.Should().Be(1_080_000m);
    }

    [Fact]
    public async Task CreateAsync_OrderDiscountAboveSubtotal_IsRejectedAndNothingStored()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 500_000m, sku: "CASE");
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.OrderDiscountAmount = 500_001m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("orderDiscountAmount");
        _orders.AddedOrders.Should().BeEmpty();
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.5)]
    public async Task CreateAsync_OrderDiscountNegativeOrFractional_IsRejected(double discount)
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 500_000m, sku: "CASE");
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.OrderDiscountAmount = (decimal)discount;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("orderDiscountAmount");
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_SalesWithinDiscountLimit_IsCreated()
    {
        // Arrange: 10,000,000 before discounts, 500,000 line + 500,000 order = 10%.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, sku: "SSD");
        var request = RequestOf(QuantityItem(product.Id, 10, 500_000m));
        request.OrderDiscountAmount = 500_000m;

        // Act
        var result = await _service.CreateAsync(request, SalesUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.CashierUserId.Should().Be(SalesUser.Id);
        result.Data.TotalAmount.Should().Be(9_900_000m);
    }

    [Fact]
    public async Task CreateAsync_SalesAboveDiscountLimit_IsRejectedUnderDiscount()
    {
        // Arrange: 600,000 line + 500,000 order = 11% of 10,000,000.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, sku: "SSD");
        var request = RequestOf(QuantityItem(product.Id, 10, 600_000m));
        request.OrderDiscountAmount = 500_000m;

        // Act
        var result = await _service.CreateAsync(request, SalesUser);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("discount");
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_SalesLineDiscountsAloneAboveLimit_AreAlsoRejected()
    {
        // Arrange: the limit covers line discounts too, not only the order discount.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, sku: "SSD");

        // Act
        var result = await _service.CreateAsync(RequestOf(QuantityItem(product.Id, 1, 200_000m)), SalesUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("discount");
    }

    [Fact]
    public async Task CreateAsync_AdminAboveSalesLimit_IsCreated()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, sku: "SSD");
        var request = RequestOf(QuantityItem(product.Id, 10, 600_000m));
        request.OrderDiscountAmount = 500_000m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DiscountAmount.Should().Be(500_000m);
    }

    [Fact]
    public async Task CreateAsync_SalesLimitFollowsConfiguration()
    {
        // Arrange
        _pricing.SalesStaffMaxDiscountPercent = 0m;
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, sku: "SSD");

        // Act
        var result = await _service.CreateAsync(RequestOf(QuantityItem(product.Id, 1, 1m)), SalesUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("discount");
    }
}
