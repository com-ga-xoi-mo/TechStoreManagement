using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Orders.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public partial class OrderServiceTests
{
    // ------------------------------------------------------------------
    // OrderStatusTransitions
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Processing)]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Completed)]
    public void StatusTransitions_AllowedPair_ReturnsTrue(OrderStatus from, OrderStatus to)
    {
        // Act
        var allowed = OrderStatusTransitions.IsAllowed(from, to);

        // Assert
        allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Completed, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Processing)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Refunded)]
    [InlineData(OrderStatus.Completed, OrderStatus.Refunded)]
    [InlineData(OrderStatus.Refunded, OrderStatus.Completed)]
    public void StatusTransitions_DisallowedPair_ReturnsFalse(OrderStatus from, OrderStatus to)
    {
        // Act
        var allowed = OrderStatusTransitions.IsAllowed(from, to);

        // Assert
        allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("Processing", OrderStatus.Processing)]
    [InlineData("completed", OrderStatus.Completed)]
    [InlineData("CANCELLED", OrderStatus.Cancelled)]
    [InlineData("  Completed  ", OrderStatus.Completed)]
    public void StatusTransitions_RequestableTarget_ParsesCaseInsensitively(string text, OrderStatus expected)
    {
        // Act
        var parsed = OrderStatusTransitions.TryParseTarget(text, out var target);

        // Assert
        parsed.Should().BeTrue();
        target.Should().Be(expected);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Refunded")]
    [InlineData("PartiallyRefunded")]
    [InlineData("foo")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void StatusTransitions_NonRequestableTarget_ReturnsFalse(string? text)
    {
        // Act
        var parsed = OrderStatusTransitions.TryParseTarget(text, out _);

        // Assert
        parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData("Cash", true)]
    [InlineData("vietqr", true)]
    [InlineData("CARD", true)]
    [InlineData("Bitcoin", false)]
    [InlineData("1", false)]
    [InlineData("-1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EnumParser_PaymentMethod_AcceptsOnlyNames(string? text, bool expected)
    {
        // Act
        var parsed = OrderEnumParser.TryParse<PaymentMethod>(text, out _);

        // Assert
        parsed.Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // OrderPricingHelper
    // ------------------------------------------------------------------

    [Fact]
    public void Pricing_VariantSelected_UsesVariantPrice()
    {
        // Arrange
        var product = new Product { BasePrice = 30_000_000m };
        var variant = new ProductVariant { Price = 29_990_000m };

        // Act
        var unitPrice = OrderPricingHelper.GetUnitPrice(product, variant);
        var line = OrderPricingHelper.PriceLine(unitPrice, 1, 0m);
        var totals = OrderPricingHelper.Calculate([line]);

        // Assert
        unitPrice.Should().Be(29_990_000m);
        line.TotalPrice.Should().Be(29_990_000m);
        totals.TotalAmount.Should().Be(29_990_000m);
    }

    [Fact]
    public void Pricing_NoVariant_UsesProductBasePriceAndLineDiscount()
    {
        // Arrange
        var product = new Product { BasePrice = 350_000m };

        // Act
        var unitPrice = OrderPricingHelper.GetUnitPrice(product, null);
        var line = OrderPricingHelper.PriceLine(unitPrice, 3, 50_000m);

        // Assert
        unitPrice.Should().Be(350_000m);
        line.TotalPrice.Should().Be(1_000_000m);
        line.DiscountAmount.Should().Be(50_000m);
    }

    [Fact]
    public void Pricing_MultipleLines_SumsSubtotalWithZeroDiscountAndTax()
    {
        // Arrange
        var lines = new[]
        {
            OrderPricingHelper.PriceLine(29_990_000m, 1, 0m),
            OrderPricingHelper.PriceLine(350_000m, 3, 50_000m),
            OrderPricingHelper.PriceLine(1_000m, 2, 0m)
        };

        // Act
        var totals = OrderPricingHelper.Calculate(lines);

        // Assert
        totals.Subtotal.Should().Be(30_992_000m);
        totals.DiscountAmount.Should().Be(0m);
        totals.TaxAmount.Should().Be(0m);
        totals.TotalAmount.Should().Be(30_992_000m);
    }

    // ------------------------------------------------------------------
    // OrderCodeGenerator
    // ------------------------------------------------------------------

    [Fact]
    public void OrderCode_FirstOrderOfMonth_StartsAtOne()
    {
        // Arrange
        var now = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var code = OrderCodeGenerator.Next(now, null);

        // Assert
        code.Should().Be("ORD-202611-0001");
    }

    [Fact]
    public void OrderCode_ExistingSequence_Continues()
    {
        // Arrange
        var now = new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var code = OrderCodeGenerator.Next(now, "ORD-202610-0007");

        // Assert
        code.Should().Be("ORD-202610-0008");
    }

    [Fact]
    public void OrderCode_PreviousMonthCode_RestartsSequence()
    {
        // Arrange
        var now = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var code = OrderCodeGenerator.Next(now, "ORD-202610-0099");

        // Assert
        code.Should().Be("ORD-202611-0001");
    }

    [Theory]
    [InlineData("ORD-202610-ABCD")]
    [InlineData("garbage")]
    [InlineData("")]
    public void OrderCode_MalformedLastCode_IsIgnored(string lastCode)
    {
        // Arrange
        var now = new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var code = OrderCodeGenerator.Next(now, lastCode);

        // Assert
        code.Should().Be("ORD-202610-0001");
    }

    [Fact]
    public void OrderCode_Prefix_UsesUtcYearAndMonth()
    {
        // Act
        var prefix = OrderCodeGenerator.GetPrefix(new DateTime(2027, 1, 31, 23, 59, 0, DateTimeKind.Utc));

        // Assert
        prefix.Should().Be("ORD-202701-");
    }

    // ------------------------------------------------------------------
    // CreateOrderValidator
    // ------------------------------------------------------------------

    private static CreateOrderRequest ValidRequest() => new()
    {
        PaymentMethod = "Cash",
        Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), SerialImeiId = Guid.NewGuid() }]
    };

    [Fact]
    public void CreateOrderValidator_ValidRequest_HasNoErrors()
    {
        // Act
        var errors = CreateOrderValidator.Validate(ValidRequest());

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void CreateOrderValidator_NullRequest_ReportsRequestError()
    {
        // Act
        var errors = CreateOrderValidator.Validate(null);

        // Assert
        errors.Should().ContainKey("request");
    }

    [Fact]
    public void CreateOrderValidator_EmptyItems_ReportsItemsError()
    {
        // Arrange
        var request = ValidRequest();
        request.Items = [];

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items");
    }

    [Fact]
    public void CreateOrderValidator_MissingItems_ReportsItemsError()
    {
        // Arrange
        var request = ValidRequest();
        request.Items = null;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items");
    }

    [Fact]
    public void CreateOrderValidator_TooManyLines_ReportsItemsError()
    {
        // Arrange
        var request = ValidRequest();
        request.Items = Enumerable.Range(0, CreateOrderValidator.MaxLines + 1)
            .Select(_ => new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 })
            .ToList();

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items");
    }

    [Theory]
    [InlineData("Bitcoin")]
    [InlineData("1")]
    public void CreateOrderValidator_UnknownPaymentMethod_ReportsPaymentMethodError(string method)
    {
        // Arrange
        var request = ValidRequest();
        request.PaymentMethod = method;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("paymentMethod");
    }

    [Fact]
    public void CreateOrderValidator_OmittedPaymentMethod_IsAllowed()
    {
        // Arrange
        var request = ValidRequest();
        request.PaymentMethod = null;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void CreateOrderValidator_NegativeDiscount_ReportsLineError()
    {
        // Arrange
        var request = ValidRequest();
        request.Items![0].DiscountAmount = -1m;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items[0].discountAmount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(10_001)]
    public void CreateOrderValidator_QuantityOutOfRange_ReportsLineError(int quantity)
    {
        // Arrange
        var request = ValidRequest();
        request.Items![0].Quantity = quantity;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items[0].quantity");
    }

    [Fact]
    public void CreateOrderValidator_MissingProductId_ReportsLineError()
    {
        // Arrange
        var request = ValidRequest();
        request.Items![0].ProductId = null;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items[0].productId");
    }

    [Fact]
    public void CreateOrderValidator_DuplicateDevice_ReportsErrorOnSecondLine()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest { ProductId = Guid.NewGuid(), SerialImeiId = deviceId },
                new CreateOrderItemRequest { ProductId = Guid.NewGuid(), SerialImeiId = deviceId }
            ]
        };

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("items[1].serialImeiId");
        errors.Should().NotContainKey("items[0].serialImeiId");
    }

    [Fact]
    public void CreateOrderValidator_SeveralProblems_AreReportedTogether()
    {
        // Arrange
        var request = ValidRequest();
        request.CustomerId = Guid.Empty;
        request.PaymentMethod = "Bitcoin";
        request.Items![0].DiscountAmount = -5m;

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Keys.Should().BeEquivalentTo("customerId", "paymentMethod", "items[0].discountAmount");
    }

    [Fact]
    public void CreateOrderValidator_NotesTooLong_ReportsNotesError()
    {
        // Arrange
        var request = ValidRequest();
        request.Notes = new string('x', CreateOrderValidator.MaxNotesLength + 1);

        // Act
        var errors = CreateOrderValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("notes");
    }
}
