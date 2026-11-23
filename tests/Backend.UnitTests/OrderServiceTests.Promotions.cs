using FluentAssertions;
using TechStore.Api.Customers.Entities;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// M4 pricing: one order-level promotion (VIP, voucher or manual) and the Sales discount limit (option A).
public partial class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_VipDiscountForGoldCustomer_DiscountsTheGoodsAndStoresTheSource()
    {
        // Arrange: 2 x 1,000,000, Gold = 5%, then VAT 10% on what is left.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var customer = AddCustomer(tier: CustomerTier.Gold);
        var request = RequestOf(QuantityItem(product.Id, 2));
        request.CustomerId = customer.Id;
        request.ApplyVipDiscount = true;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.Subtotal.Should().Be(2_000_000m);
        dto.DiscountAmount.Should().Be(100_000m);
        dto.TaxAmount.Should().Be(190_000m);
        dto.TotalAmount.Should().Be(2_090_000m);
        dto.DiscountSource.Should().Be("Vip");
        var stored = _orders.AddedOrders.Single();
        stored.DiscountSource.Should().Be(OrderDiscountSource.Vip);
        stored.VoucherId.Should().BeNull();
        stored.Items.Sum(i => i.OrderDiscountAmount).Should().Be(100_000m);
    }

    [Fact]
    public async Task CreateAsync_VipDiscount_IsTakenFromGoodsAfterLineDiscounts()
    {
        // Arrange: 2,000,000 minus a 200,000 line discount leaves 1,800,000; Platinum = 10%.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var customer = AddCustomer(tier: CustomerTier.Platinum);
        var request = RequestOf(QuantityItem(product.Id, 2, discount: 200_000m));
        request.CustomerId = customer.Id;
        request.ApplyVipDiscount = true;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.Data!.Subtotal.Should().Be(1_800_000m);
        result.Data.DiscountAmount.Should().Be(180_000m);
    }

    [Theory]
    [InlineData(CustomerTier.Standard)]
    public async Task CreateAsync_VipDiscountWithoutAVipTier_IsRejected(CustomerTier tier)
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var customer = AddCustomer(tier: tier);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.CustomerId = customer.Id;
        request.ApplyVipDiscount = true;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("applyVipDiscount");
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_VipDiscountForAWalkInGuest_IsRejected()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.ApplyVipDiscount = true;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("applyVipDiscount");
    }

    [Fact]
    public async Task CreateAsync_ValidVoucherCode_DiscountsTheGoodsAndLinksTheVoucher()
    {
        // Arrange: a 10% voucher typed in lower case.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var voucher = AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var request = RequestOf(QuantityItem(product.Id, 2));
        request.VoucherCode = " sale10 ";

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.DiscountAmount.Should().Be(200_000m);
        dto.TaxAmount.Should().Be(180_000m);
        dto.TotalAmount.Should().Be(1_980_000m);
        dto.DiscountSource.Should().Be("Voucher");
        var stored = _orders.AddedOrders.Single();
        stored.VoucherId.Should().Be(voucher.Id);
        stored.DiscountSource.Should().Be(OrderDiscountSource.Voucher);
    }

    [Fact]
    public async Task CreateAsync_VoucherCode_PricingNeverHoldsOrConsumesAUse()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var voucher = AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = "SALE10";

        // Act
        await _service.CreateAsync(request, ActingUser);

        // Assert: holding and consuming belong to the payment flow, not to pricing.
        voucher.UsedCount.Should().Be(0);
        voucher.ReservedCount.Should().Be(0);
        _vouchers.Redemptions.Should().BeEmpty();
        _vouchers.ForUpdateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("inactive")]
    [InlineData("expired")]
    [InlineData("below-minimum")]
    [InlineData("no-uses-left")]
    public async Task CreateAsync_VoucherThatCannotBeUsed_IsRejectedUnderVoucherCode(string problem)
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var voucher = AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var code = voucher.Code;
        switch (problem)
        {
            case "unknown": code = "NOPE"; break;
            case "inactive": voucher.IsActive = false; break;
            case "expired": voucher.ExpiresAt = Now; break;
            case "below-minimum": voucher.MinOrderAmount = 5_000_000m; break;
            case "no-uses-left": voucher.UsageLimit = 3; voucher.UsedCount = 2; voucher.ReservedCount = 1; break;
        }

        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = code;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("voucherCode");
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_VoucherMinimumIsCheckedAgainstGoodsAfterLineDiscounts()
    {
        // Arrange: 2,000,000 minus a 600,000 line discount is 1,400,000, below a 1,500,000 minimum.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var voucher = AddVoucher("SALE10", VoucherType.Percentage, 10m);
        voucher.MinOrderAmount = 1_500_000m;
        var request = RequestOf(QuantityItem(product.Id, 2, discount: 600_000m));
        request.VoucherCode = "SALE10";

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("voucherCode");
    }

    [Fact]
    public async Task CreateAsync_FixedVoucherAboveTheGoods_NeverMakesTheOrderNegative()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 300_000m, name: "Cáp", sku: "CAP");
        AddVoucher("BIG", VoucherType.FixedAmount, 500_000m);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = "BIG";

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.Data!.DiscountAmount.Should().Be(300_000m);
        result.Data.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAsync_VoucherAndVipTogether_IsRejected()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var customer = AddCustomer(tier: CustomerTier.Gold);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.CustomerId = customer.Id;
        request.VoucherCode = "SALE10";
        request.ApplyVipDiscount = true;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("orderDiscount");
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ManualDiscountAndVoucherTogether_IsRejected()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = "SALE10";
        request.OrderDiscountAmount = 50_000m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("orderDiscount");
    }

    [Fact]
    public async Task CreateAsync_ZeroManualDiscountWithVoucher_CountsAsOnlyTheVoucher()
    {
        // Arrange: a client that always sends orderDiscountAmount = 0 must still be able to use a voucher.
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = "SALE10";
        request.OrderDiscountAmount = 0m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DiscountSource.Should().Be("Voucher");
    }

    [Fact]
    public async Task CreateAsync_ManualDiscount_StoresManualAsTheSource()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.OrderDiscountAmount = 50_000m;

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.Data!.DiscountSource.Should().Be("Manual");
    }

    [Fact]
    public async Task CreateAsync_NoPromotion_StoresNoneAsTheSource()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");

        // Act
        var result = await _service.CreateAsync(RequestOf(QuantityItem(product.Id, 1)), ActingUser);

        // Assert
        result.Data!.DiscountSource.Should().Be("None");
    }

    [Fact]
    public async Task CreateAsync_SalesWithPlatinumVipDiscount_IsNotLimitedByTheSalesCap()
    {
        // Arrange: 10,000,000 gross, 500,000 (5%) line discount, then Platinum VIP 10% of 9,500,000.
        var product = AddProduct(serialTracked: false, basePrice: 10_000_000m, name: "Laptop", sku: "LAP");
        var customer = AddCustomer(tier: CustomerTier.Platinum);
        var request = RequestOf(QuantityItem(product.Id, 1, discount: 500_000m));
        request.CustomerId = customer.Id;
        request.ApplyVipDiscount = true;

        // Act
        var result = await _service.CreateAsync(request, SalesUser);

        // Assert: 5% + 9.5% is above 10% of the gross, yet only the 5% staff discount counts.
        result.IsSuccess.Should().BeTrue();
        result.Data!.DiscountAmount.Should().Be(950_000m);
    }

    [Fact]
    public async Task CreateAsync_SalesWithVoucherAndLineDiscountAboveTheCap_IsRejected()
    {
        // Arrange: the line discount alone is 11% of the gross.
        var product = AddProduct(serialTracked: false, basePrice: 10_000_000m, name: "Laptop", sku: "LAP");
        AddVoucher("SALE10", VoucherType.Percentage, 10m);
        var request = RequestOf(QuantityItem(product.Id, 1, discount: 1_100_000m));
        request.VoucherCode = "SALE10";

        // Act
        var result = await _service.CreateAsync(request, SalesUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("discount");
        _orders.AddedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_SalesWithVoucherAboveTenPercent_IsAllowed()
    {
        // Arrange: a 20% store voucher is policy, not a staff decision.
        var product = AddProduct(serialTracked: false, basePrice: 10_000_000m, name: "Laptop", sku: "LAP");
        AddVoucher("BIG20", VoucherType.Percentage, 20m);
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = "BIG20";

        // Act
        var result = await _service.CreateAsync(request, SalesUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DiscountAmount.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task CreateAsync_SalesWithManualDiscountAboveTheCap_IsStillRejected()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 10_000_000m, name: "Laptop", sku: "LAP");
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.OrderDiscountAmount = 1_100_000m;

        // Act
        var result = await _service.CreateAsync(request, SalesUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("discount");
    }

    [Fact]
    public async Task CreateAsync_VoucherCodeTooLong_IsRejectedByTheValidator()
    {
        // Arrange
        var product = AddProduct(serialTracked: false, basePrice: 1_000_000m, name: "Tai nghe", sku: "TN");
        var request = RequestOf(QuantityItem(product.Id, 1));
        request.VoucherCode = new string('A', 51);

        // Act
        var result = await _service.CreateAsync(request, ActingUser);

        // Assert
        result.ValidationErrors.Should().ContainKey("voucherCode");
    }

    private Voucher AddVoucher(string code, VoucherType type, decimal value)
    {
        var voucher = new Voucher
        {
            Code = code,
            DiscountType = type,
            DiscountValue = value,
            UsageLimit = 100,
            ExpiresAt = Now.AddDays(30)
        };
        _vouchers.Add(voucher);
        return voucher;
    }
}
