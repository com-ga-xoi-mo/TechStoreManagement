using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Controls.Charts;

public sealed record RevenueChartCoordinate(double X, double Y);

public sealed record RevenueChartLayout(decimal AxisMaximum, decimal AxisInterval,
    IReadOnlyList<RevenueChartCoordinate> Points)
{
    public static RevenueChartLayout Calculate(
        IReadOnlyList<DailyRevenuePoint> values,
        double width,
        double height,
        double left,
        double right,
        double top,
        double bottom)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0 || width <= left + right || height <= top + bottom)
            return new RevenueChartLayout(0m, 0m, Array.Empty<RevenueChartCoordinate>());

        var maxRevenue = Math.Max(0m, values.Max(point => point.Revenue));
        var interval = maxRevenue == 0m
            ? 1_000_000m
            : Math.Ceiling(maxRevenue / 4m / 1_000_000m) * 1_000_000m;
        var axisMaximum = interval * 4m;
        var plotWidth = width - left - right;
        var plotHeight = height - top - bottom;
        var points = new RevenueChartCoordinate[values.Count];

        for (var index = 0; index < values.Count; index++)
        {
            var x = left + plotWidth * (index + 0.5) / values.Count;
            var y = top + plotHeight * (double)(1m - Math.Max(0m, values[index].Revenue) / axisMaximum);
            points[index] = new RevenueChartCoordinate(x, y);
        }

        return new RevenueChartLayout(axisMaximum, interval, Array.AsReadOnly(points));
    }
}
