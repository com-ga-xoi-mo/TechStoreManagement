using FluentAssertions;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Orders;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.Controls;

public sealed class OrderPresentationTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, "Chờ xử lý", StatusTone.Warning)]
    [InlineData(OrderStatus.Processing, "Đang xử lý", StatusTone.Info)]
    [InlineData(OrderStatus.Completed, "Hoàn tất", StatusTone.Success)]
    [InlineData(OrderStatus.Cancelled, "Đã hủy", StatusTone.Neutral)]
    [InlineData(OrderStatus.PartiallyRefunded, "Hoàn tiền một phần", StatusTone.Accent)]
    [InlineData(OrderStatus.Refunded, "Đã hoàn tiền", StatusTone.Danger)]
    public void Status_HasVietnameseLabelAndTone(OrderStatus status, string label, StatusTone tone)
    {
        // Act / Assert
        OrderPresentation.StatusLabel(status).Should().Be(label);
        OrderPresentation.StatusTone(status).Should().Be(tone);
    }

    [Fact]
    public void Status_EveryDefinedValueIsCovered()
    {
        // Arrange / Act / Assert
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            OrderPresentation.StatusLabel(status).Should().NotBeNullOrWhiteSpace();
            OrderPresentation.StatusTone(status);
        }
    }

    [Fact]
    public void Status_UnknownValueThrows()
    {
        // Act
        var act = () => OrderPresentation.StatusLabel((OrderStatus)999);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, "Tiền mặt")]
    [InlineData(PaymentMethod.VietQr, "VietQR")]
    [InlineData(PaymentMethod.Card, "Thẻ")]
    public void Payment_HasVietnameseLabel(PaymentMethod method, string label) =>
        OrderPresentation.PaymentLabel(method).Should().Be(label);

    [Theory]
    [InlineData("0", "0 ₫")]
    [InlineData("500", "500 ₫")]
    [InlineData("15500000", "15.500.000 ₫")]
    [InlineData("30900000", "30.900.000 ₫")]
    [InlineData("1000.5", "1.000,5 ₫")]
    [InlineData("1000.50", "1.000,5 ₫")]
    [InlineData("-100000", "-100.000 ₫")]
    public void FormatMoney_UsesVietnameseGroupingAndKeepsFraction(string amount, string expected) =>
        OrderPresentation.FormatMoney(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(expected);

    [Fact]
    public void FormatMoney_KeepsPrecisionBeyondDouble()
    {
        // Arrange: 12345678901234567.89 cannot be held exactly by a double.
        var amount = 12345678901234567.89m;

        // Act / Assert
        OrderPresentation.FormatMoney(amount).Should().Be("12.345.678.901.234.567,89 ₫");
    }

    [Theory]
    [InlineData(null, "Khách lẻ")]
    [InlineData("", "Khách lẻ")]
    [InlineData("   ", "Khách lẻ")]
    [InlineData("  Nguyễn Văn A ", "Nguyễn Văn A")]
    public void CustomerDisplay_FallsBackToWalkIn(string? name, string expected) =>
        OrderPresentation.CustomerDisplay(name).Should().Be(expected);

    [Fact]
    public void StatusActions_PendingOffersPayCompleteAndCancel()
    {
        // Act
        var actions = OrderStatusActions.For(OrderStatus.Pending);

        // Assert
        actions.Select(a => a.Target).Should().Equal(OrderStatus.Processing, OrderStatus.Completed, OrderStatus.Cancelled);
        actions.Select(a => a.Label).Should().Equal("Xác nhận thanh toán", "Hoàn tất", "Hủy đơn");
        actions.Single(a => a.Target == OrderStatus.Cancelled).IsDestructive.Should().BeTrue();
        actions.Where(a => a.Target != OrderStatus.Cancelled).Should().OnlyContain(a => !a.IsDestructive);
    }

    [Fact]
    public void StatusActions_ProcessingOffersCompleteOnly()
    {
        // Act
        var actions = OrderStatusActions.For(OrderStatus.Processing);

        // Assert
        actions.Should().ContainSingle().Which.Target.Should().Be(OrderStatus.Completed);
    }

    [Theory]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.PartiallyRefunded)]
    [InlineData(OrderStatus.Refunded)]
    public void StatusActions_OtherStatusesOfferNothing(OrderStatus status) =>
        OrderStatusActions.For(status).Should().BeEmpty();

    [Fact]
    public void DraftLine_EstimatesTotalAsPriceTimesQuantityMinusDiscount()
    {
        // Arrange
        var line = new OrderDraftLine(
            Guid.NewGuid(), "Tai nghe", "SONY-WH1000XM5", null, null, false, null, null, null,
            UnitPrice: 500_000m, Quantity: 2, DiscountAmount: 100_000m);

        // Assert
        line.GrossAmount.Should().Be(1_000_000m);
        line.EstimatedTotal.Should().Be(900_000m);
    }

    [Fact]
    public void AvailableDevice_DisplaysImeiThenSerialThenId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Assert
        new AvailableDevice(id, "358912345678902", "SN1").Display.Should().Be("358912345678902");
        new AvailableDevice(id, null, "SN1").Display.Should().Be("SN1");
        new AvailableDevice(id, " ", null).Display.Should().Be(id.ToString("D"));
    }

    [Fact]
    public void AvailableDevicePage_ReportsTruncationWhenTotalExceedsItems()
    {
        // Arrange
        var items = new[] { new AvailableDevice(Guid.NewGuid(), "1", null) };

        // Assert
        new AvailableDevicePage(items, 1).IsTruncated.Should().BeFalse();
        new AvailableDevicePage(items, 150).IsTruncated.Should().BeTrue();
    }
}
