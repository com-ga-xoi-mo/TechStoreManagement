using FluentAssertions;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

// M4 Vy: a voucher applies only while active, unexpired, above its minimum order and with a use left.
public class VoucherRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_PercentageVoucher_DiscountsGoodsByPercent()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.Percentage, 10m);

        // Act
        var result = VoucherRules.Evaluate(voucher, 2_000_000m, Now);

        // Assert
        result.IsEligible.Should().BeTrue();
        result.Amount.Should().Be(200_000m);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_PercentageVoucherWithCap_NeverExceedsMaxDiscount()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.Percentage, 20m, maxDiscount: 150_000m);

        // Act
        var result = VoucherRules.Evaluate(voucher, 1_000_000m, Now);

        // Assert
        result.Amount.Should().Be(150_000m);
    }

    [Fact]
    public void Evaluate_PercentageHalfDong_RoundsAwayFromZero()
    {
        // Arrange: 10% of 1,234,565 = 123,456.5.
        var voucher = NewVoucher(VoucherType.Percentage, 10m);

        // Act
        var result = VoucherRules.Evaluate(voucher, 1_234_565m, Now);

        // Assert
        result.Amount.Should().Be(123_457m);
    }

    [Fact]
    public void Evaluate_FixedVoucherLargerThanGoods_CapsAtGoodsValue()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.FixedAmount, 500_000m);

        // Act
        var result = VoucherRules.Evaluate(voucher, 300_000m, Now);

        // Assert
        result.IsEligible.Should().BeTrue();
        result.Amount.Should().Be(300_000m);
    }

    [Fact]
    public void Evaluate_FixedVoucherSmallerThanGoods_GivesItsValue()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.FixedAmount, 150_000m);

        // Act
        var result = VoucherRules.Evaluate(voucher, 2_000_000m, Now);

        // Assert
        result.Amount.Should().Be(150_000m);
    }

    [Fact]
    public void Evaluate_InactiveVoucher_IsNotEligible()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.Percentage, 10m);
        voucher.IsActive = false;

        // Act
        var result = VoucherRules.Evaluate(voucher, 2_000_000m, Now);

        // Assert
        result.IsEligible.Should().BeFalse();
        result.Amount.Should().Be(0m);
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Evaluate_ExpiredVoucher_IsNotEligible(int minutesAfterNow)
    {
        // Arrange: expiring now or earlier is expired.
        var voucher = NewVoucher(VoucherType.Percentage, 10m);
        voucher.ExpiresAt = Now.AddMinutes(minutesAfterNow);

        // Act
        var result = VoucherRules.Evaluate(voucher, 2_000_000m, Now);

        // Assert
        result.IsEligible.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_GoodsBelowMinimumOrder_IsNotEligible()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.Percentage, 10m);
        voucher.MinOrderAmount = 2_000_000m;

        // Act
        var result = VoucherRules.Evaluate(voucher, 1_500_000m, Now);

        // Assert
        result.IsEligible.Should().BeFalse();
        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void Evaluate_GoodsEqualToMinimumOrder_IsEligible()
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.Percentage, 10m);
        voucher.MinOrderAmount = 2_000_000m;

        // Act
        var result = VoucherRules.Evaluate(voucher, 2_000_000m, Now);

        // Assert
        result.IsEligible.Should().BeTrue();
    }

    [Theory]
    [InlineData(10, 0, 0, true)]
    [InlineData(10, 9, 0, true)]
    [InlineData(10, 10, 0, false)]
    [InlineData(10, 6, 4, false)]
    [InlineData(10, 5, 4, true)]
    public void Evaluate_UsesConsumedOrHeld_RequireAtLeastOneFreeUse(int limit, int used, int reserved, bool expectedEligible)
    {
        // Arrange
        var voucher = NewVoucher(VoucherType.Percentage, 10m);
        voucher.UsageLimit = limit;
        voucher.UsedCount = used;
        voucher.ReservedCount = reserved;

        // Act
        var result = VoucherRules.Evaluate(voucher, 2_000_000m, Now);

        // Assert
        result.IsEligible.Should().Be(expectedEligible);
    }

    private static Voucher NewVoucher(VoucherType type, decimal value, decimal? maxDiscount = null) => new()
    {
        Code = "SALE",
        DiscountType = type,
        DiscountValue = value,
        MaxDiscountAmount = maxDiscount,
        UsageLimit = 100,
        ExpiresAt = Now.AddDays(30),
        IsActive = true
    };
}
