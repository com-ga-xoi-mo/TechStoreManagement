using TechStore.Client.Models.Vouchers;

namespace TechStore.Client.UnitTests.Models.Vouchers;

public sealed class VoucherRowTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void From_ActiveVoucher_CarriesDisplayStringsAndOffersDeactivate()
    {
        // Arrange
        var voucher = VoucherPresentationTests.Voucher(type: "Percentage", value: 20m, maxDiscount: 150000m, minOrder: 2000000m, used: 3, reserved: 1);

        // Act
        var row = VoucherRow.From(voucher, Now);

        // Assert
        Assert.Same(voucher, row.Voucher);
        Assert.Equal("SALE10", row.Code);
        Assert.Equal("Giảm giá thử", row.Description);
        Assert.Equal("20% · tối đa 150.000 ₫", row.DiscountLabel);
        Assert.Equal("2.000.000 ₫", row.MinOrderLabel);
        Assert.Equal("3 đã dùng · 1 đang giữ / 50 lượt", row.UsageLabel);
        Assert.Equal("Đang hoạt động", row.StatusLabel);
        Assert.Equal("Success", row.StatusTone);
        Assert.True(row.CanDeactivate);
        Assert.False(row.CanActivate);
    }

    [Theory]
    [InlineData(false, 30, 0, "Đã tắt", "Neutral", true)]
    [InlineData(true, -1, 0, "Hết hạn", "Danger", false)]
    [InlineData(true, 30, 50, "Hết lượt", "Warning", false)]
    public void From_ByState_PicksLabelToneAndAction(bool isActive, int daysToExpiry, int used, string label, string tone, bool canActivate)
    {
        // Arrange
        var voucher = VoucherPresentationTests.Voucher(isActive: isActive, expiresAt: Now.AddDays(daysToExpiry), used: used);

        // Act
        var row = VoucherRow.From(voucher, Now);

        // Assert
        Assert.Equal(label, row.StatusLabel);
        Assert.Equal(tone, row.StatusTone);
        Assert.Equal(canActivate, row.CanActivate);
        Assert.Equal(!canActivate, row.CanDeactivate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_BlankDescription_ShowsDash(string? description)
    {
        // Arrange
        var voucher = VoucherPresentationTests.Voucher() with { Description = description };

        // Act and Assert
        Assert.Equal("—", VoucherRow.From(voucher, Now).Description);
    }
}
