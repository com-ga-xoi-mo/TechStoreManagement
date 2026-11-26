using TechStore.Client.Controls.Charts;

namespace TechStore.Client.UnitTests.Controls.Charts;

public sealed class FinancialChartGeometryTests
{
    private static FinancialChartDay Day(string label = "01/10", decimal? sales = null, decimal? refunds = null, decimal? profit = null,
        decimal? cash = null, decimal? qr = null, decimal? refundPaid = null) =>
        new(label, sales, refunds, profit, cash, qr, refundPaid);

    private static FinancialChartLayout Sales(params FinancialChartDay[] days) =>
        FinancialChartLayout.Calculate(days, FinancialChartMode.Sales, 600, 260, 58, 12, 14, 30);

    [Fact]
    public void Calculate_EmptyDaysReturnsEmptyLayout()
    {
        // Act
        var layout = Sales();

        // Assert
        Assert.True(layout.IsEmpty);
        Assert.Empty(layout.Bars);
        Assert.Empty(layout.Ticks);
        Assert.Empty(layout.LineSegments);
    }

    [Theory]
    [InlineData(40, 260)]
    [InlineData(600, 30)]
    [InlineData(double.NaN, 260)]
    [InlineData(600, double.PositiveInfinity)]
    public void Calculate_InvalidSizeReturnsEmptyLayout(double width, double height)
    {
        // Act
        var layout = FinancialChartLayout.Calculate([Day(sales: 10m)], FinancialChartMode.Sales, width, height, 58, 12, 14, 30);

        // Assert
        Assert.True(layout.IsEmpty);
        Assert.Empty(layout.Bars);
    }

    [Fact]
    public void Calculate_PositiveOnlyAxisStartsAtZeroAndBarsRiseFromBaseline()
    {
        // Act
        var layout = Sales(Day(sales: 920_000m, refunds: 100_000m, profit: 200_000m));

        // Assert
        Assert.Equal(0m, layout.AxisMinimum);
        Assert.True(layout.AxisMaximum >= 920_000m);
        Assert.Equal(layout.PlotTop + layout.PlotHeight, layout.BaselineY, 6);
        Assert.Equal(2, layout.Bars.Count);
        Assert.All(layout.Bars, bar =>
        {
            Assert.True(bar.Height > 0);
            Assert.Equal(layout.BaselineY, bar.Y + bar.Height, 6);
        });
        Assert.Equal(layout.Ticks.Count, layout.Ticks.Select(tick => tick.Value).Distinct().Count());
        Assert.Contains(layout.Ticks, tick => tick.Value == 0m);
    }

    [Fact]
    public void Calculate_NegativeValuesExtendAxisAndBarsGoBelowTheZeroBaseline()
    {
        // Act
        var layout = Sales(Day(sales: 1_000_000m, profit: -400_000m), Day(sales: 500_000m, refunds: 2_000_000m, profit: 300_000m));

        // Assert
        Assert.True(layout.AxisMinimum <= -400_000m);
        Assert.True(layout.AxisMaximum >= 2_000_000m);
        Assert.InRange(layout.BaselineY, layout.PlotTop, layout.PlotTop + layout.PlotHeight);
        Assert.Contains(layout.Ticks, tick => tick.Value == 0m && Math.Abs(tick.Y - layout.BaselineY) < 1e-6);
        Assert.All(layout.Bars, bar => Assert.Equal(layout.BaselineY, bar.Y + bar.Height, 6));

        var layoutCash = FinancialChartLayout.Calculate([Day(cash: -300_000m, qr: 200_000m)], FinancialChartMode.CashFlow, 600, 260, 58, 12, 14, 30);
        var cashBar = Assert.Single(layoutCash.Bars, bar => bar.Series == FinancialChartSeries.Cash);
        Assert.Equal(layoutCash.BaselineY, cashBar.Y, 6);
        Assert.True(cashBar.Y + cashBar.Height > layoutCash.BaselineY);
    }

    [Fact]
    public void Calculate_AxisContainsZeroEvenWhenAllValuesAreNegative()
    {
        // Act
        var layout = Sales(Day(profit: -900m), Day(profit: -100m));

        // Assert
        Assert.Equal(0m, layout.AxisMaximum);
        Assert.True(layout.AxisMinimum <= -900m);
        Assert.Contains(layout.Ticks, tick => tick.Value == 0m);
    }

    [Fact]
    public void Calculate_SinglePointStaysInsideThePlot()
    {
        // Act
        var layout = Sales(Day(sales: 92_650_000m, profit: 1_000_000m));

        // Assert
        var center = Assert.Single(layout.DayCenters);
        Assert.InRange(center, layout.PlotLeft, layout.PlotLeft + layout.PlotWidth);
        Assert.Single(layout.LinePoints);
        Assert.Empty(layout.LineSegments);
        Assert.All(layout.Bars, bar =>
        {
            Assert.InRange(bar.X, layout.PlotLeft, layout.PlotLeft + layout.PlotWidth);
            Assert.True(bar.X + bar.Width <= layout.PlotLeft + layout.PlotWidth);
        });
        Assert.Equal(1, layout.LabelStep);
        Assert.True(layout.ShowsLabel(0));
    }

    [Fact]
    public void Calculate_AllZeroValuesUseAFiniteAxisAndDrawNoBars()
    {
        // Act
        var layout = Sales(Day(sales: 0m, refunds: 0m, profit: 0m), Day(sales: 0m, refunds: 0m, profit: 0m));

        // Assert
        Assert.Equal(0m, layout.AxisMinimum);
        Assert.True(layout.AxisMaximum > 0m);
        Assert.Empty(layout.Bars);
        Assert.Equal(2, layout.LinePoints.Count);
        Assert.All(layout.LinePoints, point =>
        {
            Assert.True(double.IsFinite(point.X));
            Assert.True(double.IsFinite(point.Y));
            Assert.Equal(layout.BaselineY, point.Y, 6);
        });
        Assert.All(layout.Ticks, tick => Assert.True(double.IsFinite(tick.Y)));
    }

    [Fact]
    public void Calculate_NullPointBreaksTheLineAndProducesNoBar()
    {
        // Act
        var layout = Sales(
            Day(sales: 10m, profit: 5m),
            Day(sales: null, profit: null),
            Day(sales: 30m, profit: 8m),
            Day(sales: 40m, profit: 9m));

        // Assert
        Assert.Equal(3, layout.LinePoints.Count);
        var segment = Assert.Single(layout.LineSegments);
        Assert.Equal(2, segment.FromDayIndex);
        Assert.Equal(3, segment.ToDayIndex);
        Assert.DoesNotContain(layout.Bars, bar => bar.DayIndex == 1);
    }

    [Fact]
    public void Calculate_CashFlowModeDrawsThreeBarsPerDayAndNoLine()
    {
        // Act
        var layout = FinancialChartLayout.Calculate(
            [Day(cash: 300m, qr: 200m, refundPaid: 50m, profit: 99m), Day(cash: 100m, qr: null, refundPaid: 0m)],
            FinancialChartMode.CashFlow, 600, 260, 58, 12, 14, 30);

        // Assert
        Assert.Equal(
            [FinancialChartSeries.Cash, FinancialChartSeries.Qr, FinancialChartSeries.RefundPaid, FinancialChartSeries.Cash],
            layout.Bars.Select(bar => bar.Series));
        Assert.Empty(layout.LinePoints);
        Assert.Empty(layout.LineSegments);
        Assert.All(layout.Bars.Where(bar => bar.DayIndex == 0), bar => Assert.True(bar.X + bar.Width <= layout.DayCenters[0] + layout.SlotWidth / 2));
    }

    [Fact]
    public void Calculate_ThirtyOneDaysSkipLabelsSoTheyNeverOverlap()
    {
        // Arrange
        var days = Enumerable.Range(1, 31).Select(day => Day($"{day:00}/10", sales: day * 1000m, profit: day * 100m)).ToArray();

        // Act
        var layout = FinancialChartLayout.Calculate(days, FinancialChartMode.Sales, 700, 260, 58, 12, 14, 30, labelWidth: 44);

        // Assert
        Assert.Equal(31, layout.DayCenters.Count);
        Assert.True(layout.LabelStep > 1);
        Assert.True(layout.LabelStep * layout.SlotWidth >= 44);
        var shown = Enumerable.Range(0, 31).Where(layout.ShowsLabel).ToArray();
        Assert.Equal(0, shown[0]);
        Assert.All(shown.Zip(shown.Skip(1)), pair => Assert.True((pair.Second - pair.First) * layout.SlotWidth >= 44));
    }

    [Fact]
    public void Axis_FormatsShortVietnameseLabels()
    {
        Assert.Equal("0", FinancialChartFormat.Axis(0m));
        Assert.Equal("500", FinancialChartFormat.Axis(500m));
        Assert.Equal("2,5k", FinancialChartFormat.Axis(2_500m));
        Assert.Equal("1,5 tr", FinancialChartFormat.Axis(1_500_000m));
        Assert.Equal("-2 tr", FinancialChartFormat.Axis(-2_000_000m));
        Assert.Equal("3 tỷ", FinancialChartFormat.Axis(3_000_000_000m));
    }

    [Fact]
    public void Donut_SplitsTwoChannelsClockwiseFromTwelveOClock()
    {
        // Act
        var donut = FinancialDonutLayout.Calculate(300m, 100m, 400m, 100, 100, 50);

        // Assert
        Assert.False(donut.IsNeutral);
        Assert.Equal(75, donut.CashPercent, 6);
        Assert.Equal(25, donut.QrPercent, 6);
        Assert.Null(donut.UnassignedAmount);
        var cash = donut.Segments[0];
        var qr = donut.Segments[1];
        Assert.Equal(FinancialDonutLayout.CashKey, cash.Key);
        Assert.Equal(-90, cash.StartAngle, 6);
        Assert.Equal(270, cash.SweepAngle, 6);
        Assert.True(cash.IsLargeArc);
        Assert.Equal(100, cash.StartX, 6);
        Assert.Equal(50, cash.StartY, 6);
        Assert.Equal(90, qr.SweepAngle, 6);
        Assert.False(qr.IsLargeArc);
        Assert.Equal(cash.EndX, qr.StartX, 6);
        Assert.Equal(cash.EndY, qr.StartY, 6);
    }

    [Fact]
    public void Donut_ChannelWithZeroKeepsASingleFullRingSegment()
    {
        // Act
        var donut = FinancialDonutLayout.Calculate(250m, 0m, 250m, 100, 100, 50);

        // Assert
        Assert.False(donut.IsNeutral);
        var segment = Assert.Single(donut.Segments);
        Assert.Equal(FinancialDonutLayout.CashKey, segment.Key);
        Assert.Equal(100, donut.CashPercent, 6);
        Assert.Equal(0, donut.QrPercent, 6);
        Assert.True(segment.SweepAngle < 360);
        Assert.True(segment.IsLargeArc);
        Assert.True(double.IsFinite(segment.EndX) && double.IsFinite(segment.EndY));
    }

    [Theory]
    [InlineData(null, 100, 100)]
    [InlineData(100, null, 100)]
    [InlineData(0, 0, 0)]
    [InlineData(null, null, null)]
    public void Donut_NullChannelOrNoMoneyIsNeutralWithoutShares(int? cash, int? qr, int? total)
    {
        // Act
        var donut = FinancialDonutLayout.Calculate(cash, qr, total, 100, 100, 50);

        // Assert
        Assert.True(donut.IsNeutral);
        Assert.Empty(donut.Segments);
        Assert.Equal(0, donut.CashPercent);
        Assert.Equal(0, donut.QrPercent);
    }

    [Theory]
    [InlineData(500, 100, 100, true)]
    [InlineData(100, 100, 100, false)]
    [InlineData(100, 100, 150, false)]
    public void Donut_ReportsUnassignedAmountOnlyWhenTotalExceedsBothChannels(int total, int cash, int qr, bool unassigned)
    {
        // Act
        var donut = FinancialDonutLayout.Calculate(cash, qr, total, 100, 100, 50);

        // Assert
        if (unassigned) Assert.Equal(total - cash - qr, donut.UnassignedAmount);
        else Assert.Null(donut.UnassignedAmount);
    }

    [Fact]
    public void Donut_UnassignedAmountNeedsBothChannels()
    {
        Assert.Null(FinancialDonutLayout.Calculate(null, 100m, 500m, 100, 100, 50).UnassignedAmount);
        Assert.Null(FinancialDonutLayout.Calculate(100m, 100m, null, 100, 100, 50).UnassignedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void Donut_InvalidRadiusIsNeutral(double radius)
    {
        // Act
        var donut = FinancialDonutLayout.Calculate(100m, 100m, 200m, 100, 100, radius);

        // Assert
        Assert.True(donut.IsNeutral);
        Assert.Empty(donut.Segments);
    }
}
