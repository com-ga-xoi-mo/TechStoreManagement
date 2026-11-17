using FluentAssertions;
using TechStore.Api.Orders.Services;

namespace TechStore.UnitTests;

// The one money formula (M3 Vy): line discount -> order discount -> VAT, whole-đồng rounding, line allocation.
public class OrderPricingHelperTests
{
    [Fact]
    public void Calculate_M4ReconciliationExample_Returns1980000()
    {
        // Arrange: 2 x 1,000,000, order discount 200,000, VAT 10%.
        var lines = new[]
        {
            OrderPricingHelper.PriceLine(1_000_000m, 1, 0m),
            OrderPricingHelper.PriceLine(1_000_000m, 1, 0m)
        };

        // Act
        var pricing = OrderPricingHelper.Calculate(lines, 200_000m, 10m);

        // Assert
        pricing.Totals.Should().Be(new OrderTotals(2_000_000m, 200_000m, 180_000m, 1_980_000m));
        pricing.Lines.Should().Equal(new LineAllocation(100_000m, 90_000m), new LineAllocation(100_000m, 90_000m));
    }

    [Fact]
    public void Calculate_LineDiscountAndOrderDiscount_SubtractsLineDiscountOnlyOnce()
    {
        // Arrange: 3 x 350,000 with a 50,000 line discount.
        var line = OrderPricingHelper.PriceLine(350_000m, 3, 50_000m);

        // Act
        var pricing = OrderPricingHelper.Calculate([line], 100_000m, 10m);

        // Assert
        line.TotalPrice.Should().Be(1_000_000m);
        pricing.Totals.Subtotal.Should().Be(1_000_000m);
        pricing.Totals.DiscountAmount.Should().Be(100_000m);
        pricing.Totals.TaxAmount.Should().Be(90_000m);
        pricing.Totals.TotalAmount.Should().Be(990_000m);
    }

    [Theory]
    [InlineData(1_234_565, 123_457, 1_358_022)] // 123,456.5 -> half rounds away from zero
    [InlineData(1_234_564, 123_456, 1_358_020)] // 123,456.4 -> down
    [InlineData(29_990_000, 2_999_000, 32_989_000)]
    public void Calculate_Tax_RoundsToWholeDong(int amount, int expectedTax, int expectedTotal)
    {
        // Act
        var pricing = OrderPricingHelper.Calculate([OrderPricingHelper.PriceLine(amount, 1, 0m)], 0m, 10m);

        // Assert
        pricing.Totals.TaxAmount.Should().Be(expectedTax);
        pricing.Totals.TotalAmount.Should().Be(expectedTotal);
        pricing.Lines.Single().TaxAmount.Should().Be(expectedTax);
    }

    [Fact]
    public void Calculate_UnevenOrderDiscount_GivesRemainderToLargestRemainderThenEarlierLine()
    {
        // Arrange: three equal lines share 100,000 -> 33,333.33 each.
        var lines = Enumerable.Range(0, 3).Select(_ => OrderPricingHelper.PriceLine(100_000m, 1, 0m)).ToArray();

        // Act
        var pricing = OrderPricingHelper.Calculate(lines, 100_000m, 0m);

        // Assert
        pricing.Lines.Select(l => l.OrderDiscountAmount).Should().Equal(33_334m, 33_333m, 33_333m);
        pricing.Totals.TaxAmount.Should().Be(0m);
        pricing.Totals.TotalAmount.Should().Be(200_000m);
    }

    [Fact]
    public void Calculate_WeightedAllocation_LargestRemainderWinsOverEarlierLine()
    {
        // Arrange: weights 1 : 2 sharing 100 -> 33.33 and 66.67, so the second line takes the extra đồng.
        var lines = new[]
        {
            OrderPricingHelper.PriceLine(1_000m, 1, 0m),
            OrderPricingHelper.PriceLine(2_000m, 1, 0m)
        };

        // Act
        var pricing = OrderPricingHelper.Calculate(lines, 100m, 0m);

        // Assert
        pricing.Lines.Select(l => l.OrderDiscountAmount).Should().Equal(33m, 67m);
    }

    [Fact]
    public void Calculate_TaxIsSharedByTaxableAmountAfterOrderDiscount()
    {
        // Arrange: 600,000 + 400,000, order discount 100,000 -> 60,000/40,000; taxable 540,000/360,000.
        var lines = new[]
        {
            OrderPricingHelper.PriceLine(600_000m, 1, 0m),
            OrderPricingHelper.PriceLine(400_000m, 1, 0m)
        };

        // Act
        var pricing = OrderPricingHelper.Calculate(lines, 100_000m, 8m);

        // Assert
        pricing.Totals.TaxAmount.Should().Be(72_000m);
        pricing.Lines.Should().Equal(new LineAllocation(60_000m, 43_200m), new LineAllocation(40_000m, 28_800m));
    }

    [Fact]
    public void Calculate_FullyDiscountedLine_GetsNoShare()
    {
        // Arrange
        var lines = new[]
        {
            OrderPricingHelper.PriceLine(500_000m, 1, 500_000m),
            OrderPricingHelper.PriceLine(1_000_000m, 1, 0m)
        };

        // Act
        var pricing = OrderPricingHelper.Calculate(lines, 100_000m, 10m);

        // Assert
        pricing.Lines[0].Should().Be(new LineAllocation(0m, 0m));
        pricing.Lines[1].Should().Be(new LineAllocation(100_000m, 90_000m));
        pricing.Totals.TotalAmount.Should().Be(990_000m);
    }

    [Fact]
    public void Calculate_WholeOrderDiscounted_HasNoTax()
    {
        // Act
        var pricing = OrderPricingHelper.Calculate([OrderPricingHelper.PriceLine(300_000m, 1, 0m)], 300_000m, 10m);

        // Assert
        pricing.Totals.Should().Be(new OrderTotals(300_000m, 300_000m, 0m, 0m));
        pricing.Lines.Single().Should().Be(new LineAllocation(300_000m, 0m));
    }

    [Theory]
    [InlineData(7, 12_345, 3_333)]
    [InlineData(5, 999_999, 1)]
    [InlineData(11, 1, 77_777)]
    [InlineData(3, 0, 1_234)]
    public void Calculate_AnyOrder_SharesAddUpExactly(int lineCount, int orderDiscount, int seed)
    {
        // Arrange: uneven prices, quantities and line discounts.
        var random = new Random(seed);
        var lines = Enumerable.Range(0, lineCount).Select(_ =>
        {
            var price = random.Next(1_000, 3_000_000);
            var quantity = random.Next(1, 4);
            return OrderPricingHelper.PriceLine(price, quantity, random.Next(0, price / 10));
        }).ToArray();
        var discount = Math.Min(orderDiscount, lines.Sum(l => l.TotalPrice));

        // Act
        var pricing = OrderPricingHelper.Calculate(lines, discount, 10m);

        // Assert
        pricing.Lines.Sum(l => l.OrderDiscountAmount).Should().Be(pricing.Totals.DiscountAmount);
        pricing.Lines.Sum(l => l.TaxAmount).Should().Be(pricing.Totals.TaxAmount);
        pricing.Lines.Should().OnlyContain(l => l.OrderDiscountAmount >= 0 && l.TaxAmount >= 0
            && l.OrderDiscountAmount == decimal.Truncate(l.OrderDiscountAmount) && l.TaxAmount == decimal.Truncate(l.TaxAmount));
        pricing.Totals.TotalAmount.Should().Be(pricing.Totals.Subtotal - pricing.Totals.DiscountAmount + pricing.Totals.TaxAmount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_000_001)]
    public void Calculate_OrderDiscountOutsideZeroToSubtotal_Throws(int orderDiscount)
    {
        // Arrange
        var lines = new[] { OrderPricingHelper.PriceLine(2_000_000m, 1, 0m) };

        // Act
        var act = () => OrderPricingHelper.Calculate(lines, orderDiscount, 10m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Calculate_TaxRateOutsideZeroToHundred_Throws(double rate)
    {
        // Act
        var act = () => OrderPricingHelper.Calculate([OrderPricingHelper.PriceLine(1_000m, 1, 0m)], 0m, (decimal)rate);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(10, 0, 0, true)]
    [InlineData(10_000_000, 500_000, 500_000, true)]
    [InlineData(10_000_000, 600_000, 500_000, false)]
    [InlineData(10_000_000, 0, 1_000_001, false)]
    public void IsWithinDiscountLimit_ComparesTotalDiscountWithPercentOfGross(int gross, int lineDiscounts, int orderDiscount, bool expected)
    {
        // Act
        var allowed = OrderPricingHelper.IsWithinDiscountLimit(gross, lineDiscounts + orderDiscount, 10m);

        // Assert
        allowed.Should().Be(expected);
    }
}
