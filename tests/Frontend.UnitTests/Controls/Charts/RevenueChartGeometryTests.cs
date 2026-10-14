using TechStore.Client.Controls.Charts;
using TechStore.Client.Models.Analytics;

namespace TechStore.Client.UnitTests.Controls.Charts;

public sealed class RevenueChartGeometryTests
{
    [Fact]
    public void Calculate_EmptySeriesReturnsEmptyLayout()
    {
        var layout = RevenueChartLayout.Calculate([], 400, 230, 42, 12, 12, 34);

        Assert.Empty(layout.Points);
        Assert.Equal(0m, layout.AxisMaximum);
    }

    [Fact]
    public void Calculate_AllZeroSeriesUsesFiniteBottomAxisCoordinates()
    {
        var date = new DateOnly(2026, 10, 14);
        var layout = RevenueChartLayout.Calculate(
            [new DailyRevenuePoint(date.AddDays(-1), 0m), new DailyRevenuePoint(date, 0m)],
            400, 230, 42, 12, 12, 34);

        Assert.Equal(4_000_000m, layout.AxisMaximum);
        Assert.All(layout.Points, point =>
        {
            Assert.True(double.IsFinite(point.X));
            Assert.True(double.IsFinite(point.Y));
            Assert.InRange(point.Y, 12, 196);
        });
        Assert.True(layout.Points[0].X < layout.Points[1].X);
    }

    [Fact]
    public void Calculate_SinglePeakPointStaysInsidePlotAndUsesRoundedAxis()
    {
        var layout = RevenueChartLayout.Calculate(
            [new DailyRevenuePoint(new DateOnly(2026, 10, 14), 92_650_000m)],
            400, 230, 42, 12, 12, 34);

        Assert.Equal(96_000_000m, layout.AxisMaximum);
        Assert.Single(layout.Points);
        Assert.InRange(layout.Points[0].X, 42, 388);
        Assert.InRange(layout.Points[0].Y, 12, 196);
    }

    [Fact]
    public void Calculate_InvalidPlotSizeReturnsNoCoordinates()
    {
        var layout = RevenueChartLayout.Calculate(
            [new DailyRevenuePoint(new DateOnly(2026, 10, 14), 1m)],
            20, 20, 42, 12, 12, 34);

        Assert.Empty(layout.Points);
    }
}
