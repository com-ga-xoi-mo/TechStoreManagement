using System.Globalization;
using TechStore.Client.Models.Vouchers;

namespace TechStore.Client.UnitTests.Models.Vouchers;

public sealed class VoucherPresentationTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StatusOf_ActiveVoucherWithUsesLeft_IsActive()
    {
        // Arrange
        var voucher = Voucher();

        // Act and Assert
        Assert.Equal(VoucherStatus.Active, VoucherPresentation.StatusOf(voucher, Now));
    }

    [Fact]
    public void StatusOf_LastUseHeld_IsExhausted()
    {
        // Arrange
        var voucher = Voucher(usageLimit: 5, used: 4, reserved: 1);

        // Act and Assert
        Assert.Equal(VoucherStatus.Exhausted, VoucherPresentation.StatusOf(voucher, Now));
    }

    [Fact]
    public void StatusOf_OneUseStillFree_IsActive()
    {
        // Arrange
        var voucher = Voucher(usageLimit: 5, used: 3, reserved: 1);

        // Act and Assert
        Assert.Equal(VoucherStatus.Active, VoucherPresentation.StatusOf(voucher, Now));
    }

    [Fact]
    public void StatusOf_ExpiryNotInTheFuture_IsExpired()
    {
        // Arrange
        var atNow = Voucher(expiresAt: Now);
        var past = Voucher(expiresAt: Now.AddSeconds(-1));
        var oneSecondLeft = Voucher(expiresAt: Now.AddSeconds(1));

        // Act and Assert
        Assert.Equal(VoucherStatus.Expired, VoucherPresentation.StatusOf(atNow, Now));
        Assert.Equal(VoucherStatus.Expired, VoucherPresentation.StatusOf(past, Now));
        Assert.Equal(VoucherStatus.Active, VoucherPresentation.StatusOf(oneSecondLeft, Now));
    }

    [Fact]
    public void StatusOf_SwitchedOffWins_OverExpiredAndExhausted()
    {
        // Arrange
        var voucher = Voucher(isActive: false, expiresAt: Now.AddDays(-3), usageLimit: 1, used: 1);

        // Act and Assert
        Assert.Equal(VoucherStatus.Inactive, VoucherPresentation.StatusOf(voucher, Now));
    }

    [Fact]
    public void StatusOf_ExpiredWins_OverExhausted()
    {
        // Arrange
        var voucher = Voucher(expiresAt: Now.AddDays(-1), usageLimit: 1, used: 1);

        // Act and Assert
        Assert.Equal(VoucherStatus.Expired, VoucherPresentation.StatusOf(voucher, Now));
    }

    [Theory]
    [InlineData(VoucherStatus.Active, "Đang hoạt động")]
    [InlineData(VoucherStatus.Inactive, "Đã tắt")]
    [InlineData(VoucherStatus.Expired, "Hết hạn")]
    [InlineData(VoucherStatus.Exhausted, "Hết lượt")]
    public void StatusLabel_IsVietnamese(VoucherStatus status, string expected) =>
        Assert.Equal(expected, VoucherPresentation.StatusLabel(status));

    [Theory]
    [InlineData("Percentage", 10, null, "10%")]
    [InlineData("Percentage", 20, 150000d, "20% · tối đa 150.000 ₫")]
    [InlineData("Percentage", 12.5, null, "12,5%")]
    [InlineData("FixedAmount", 500000, null, "500.000 ₫")]
    [InlineData("FixedAmount", 500000, 100000d, "500.000 ₫")]
    public void DiscountLabel_ShowsValueAndCap(string type, double value, double? cap, string expected)
    {
        // Arrange
        var voucher = Voucher(type: type, value: (decimal)value, maxDiscount: cap is null ? null : (decimal)cap);

        // Act and Assert
        Assert.Equal(expected, VoucherPresentation.DiscountLabel(voucher));
    }

    [Theory]
    [InlineData(0, "Không yêu cầu")]
    [InlineData(2000000, "2.000.000 ₫")]
    public void MinOrderLabel_ShowsRequirementOrNone(double minOrder, string expected) =>
        Assert.Equal(expected, VoucherPresentation.MinOrderLabel(Voucher(minOrder: (decimal)minOrder)));

    [Theory]
    [InlineData(50, 3, 0, "3 / 50 lượt")]
    [InlineData(50, 3, 1, "3 đã dùng · 1 đang giữ / 50 lượt")]
    [InlineData(1, 0, 0, "0 / 1 lượt")]
    public void UsageLabel_ShowsUsedHeldAndLimit(int limit, int used, int held, string expected) =>
        Assert.Equal(expected, VoucherPresentation.UsageLabel(Voucher(usageLimit: limit, used: used, reserved: held)));

    [Fact]
    public void ExpiryLabel_UsesLocalDate()
    {
        // Arrange
        var voucher = Voucher(expiresAt: new DateTime(2026, 12, 31, 10, 0, 0, DateTimeKind.Utc));
        var expected = voucher.ExpiresAt.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));

        // Act and Assert
        Assert.Equal(expected, VoucherPresentation.ExpiryLabel(voucher));
    }

    internal static VoucherItem Voucher(
        string type = "Percentage",
        decimal value = 10m,
        decimal minOrder = 0m,
        decimal? maxDiscount = null,
        int usageLimit = 50,
        int used = 0,
        int reserved = 0,
        DateTime? expiresAt = null,
        bool isActive = true,
        string code = "SALE10") => new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            code,
            "Giảm giá thử",
            type,
            value,
            minOrder,
            maxDiscount,
            usageLimit,
            used,
            reserved,
            expiresAt ?? Now.AddDays(30),
            isActive,
            Now.AddDays(-10));
}
