using System.Globalization;

namespace TechStore.Client.Controls.Charts;

/// <summary>Which figures the financial trend chart plots.</summary>
public enum FinancialChartMode
{
    /// <summary>Bars: sales and refunds; line: net gross profit.</summary>
    Sales,
    /// <summary>Bars: cash received, VietQR received and refunds paid.</summary>
    CashFlow
}

public enum FinancialChartSeries
{
    Sales,
    Refunds,
    NetProfit,
    Cash,
    Qr,
    RefundPaid
}

/// <summary>One day of the trend chart. A null figure means "not computed" and is never drawn as zero.</summary>
public sealed record FinancialChartDay(
    string Label,
    decimal? Sales,
    decimal? Refunds,
    decimal? NetProfit,
    decimal? Cash,
    decimal? Qr,
    decimal? RefundPaid,
    string SalesDescription = "",
    string CashFlowDescription = "");

public sealed record FinancialChartBar(int DayIndex, FinancialChartSeries Series, double X, double Y, double Width, double Height, decimal Value);

public sealed record FinancialChartTick(decimal Value, double Y);

public sealed record FinancialChartLinePoint(int DayIndex, double X, double Y, decimal Value);

/// <summary>A straight piece of the line between two neighbouring days that both have a value.</summary>
public sealed record FinancialChartLineSegment(int FromDayIndex, int ToDayIndex, double X1, double Y1, double X2, double Y2);

public sealed record FinancialChartLayout(
    decimal AxisMinimum,
    decimal AxisMaximum,
    decimal AxisInterval,
    double BaselineY,
    double PlotLeft,
    double PlotTop,
    double PlotWidth,
    double PlotHeight,
    double SlotWidth,
    int LabelStep,
    IReadOnlyList<double> DayCenters,
    IReadOnlyList<FinancialChartTick> Ticks,
    IReadOnlyList<FinancialChartBar> Bars,
    IReadOnlyList<FinancialChartLinePoint> LinePoints,
    IReadOnlyList<FinancialChartLineSegment> LineSegments)
{
    public static FinancialChartLayout Empty { get; } = new(0m, 0m, 0m, 0, 0, 0, 0, 0, 0, 1,
        Array.Empty<double>(), Array.Empty<FinancialChartTick>(), Array.Empty<FinancialChartBar>(),
        Array.Empty<FinancialChartLinePoint>(), Array.Empty<FinancialChartLineSegment>());

    public bool IsEmpty => DayCenters.Count == 0;

    /// <summary>True when the x label of the day at <paramref name="dayIndex"/> is drawn.</summary>
    public bool ShowsLabel(int dayIndex) => dayIndex >= 0 && dayIndex % LabelStep == 0;

    public static FinancialChartLayout Calculate(
        IReadOnlyList<FinancialChartDay> days,
        FinancialChartMode mode,
        double width,
        double height,
        double left,
        double right,
        double top,
        double bottom,
        double labelWidth = 44)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (days.Count == 0 || !double.IsFinite(width) || !double.IsFinite(height)
            || !double.IsFinite(left) || !double.IsFinite(right) || !double.IsFinite(top) || !double.IsFinite(bottom)
            || width <= left + right || height <= top + bottom)
            return Empty;

        var series = mode == FinancialChartMode.Sales
            ? new[] { FinancialChartSeries.Sales, FinancialChartSeries.Refunds }
            : [FinancialChartSeries.Cash, FinancialChartSeries.Qr, FinancialChartSeries.RefundPaid];
        var hasLine = mode == FinancialChartMode.Sales;

        decimal low = 0m, high = 0m;
        foreach (var day in days)
        {
            foreach (var kind in series) Include(Value(day, kind));
            if (hasLine) Include(Value(day, FinancialChartSeries.NetProfit));
        }

        var (axisMin, axisMax, step) = Axis(low, high);
        var plotWidth = width - left - right;
        var plotHeight = height - top - bottom;
        var span = (double)(axisMax - axisMin);
        double Y(decimal value) => top + plotHeight * (double)(axisMax - value) / span;
        var baseline = Y(0m);

        var ticks = new List<FinancialChartTick>();
        for (var value = axisMin; value <= axisMax && ticks.Count < 24; value += step)
            ticks.Add(new FinancialChartTick(value, Y(value)));

        var slot = plotWidth / days.Count;
        var centers = new double[days.Count];
        for (var i = 0; i < days.Count; i++) centers[i] = left + slot * (i + 0.5);

        var groupWidth = slot * 0.72;
        var barWidth = Math.Max(1, Math.Min(26, groupWidth / series.Length));
        var groupStart = barWidth * series.Length / 2;
        var bars = new List<FinancialChartBar>();
        for (var i = 0; i < days.Count; i++)
        {
            for (var s = 0; s < series.Length; s++)
            {
                if (Value(days[i], series[s]) is not { } value || value == 0m) continue;
                var y = Y(value);
                var barTop = Math.Min(y, baseline);
                var barHeight = Math.Max(1, Math.Abs(y - baseline));
                bars.Add(new FinancialChartBar(i, series[s], centers[i] - groupStart + s * barWidth, barTop, barWidth, barHeight, value));
            }
        }

        var points = new List<FinancialChartLinePoint>();
        var segments = new List<FinancialChartLineSegment>();
        if (hasLine)
        {
            FinancialChartLinePoint? previous = null;
            for (var i = 0; i < days.Count; i++)
            {
                if (days[i].NetProfit is not { } value)
                {
                    previous = null;
                    continue;
                }

                var point = new FinancialChartLinePoint(i, centers[i], Y(value), value);
                points.Add(point);
                if (previous is not null)
                    segments.Add(new FinancialChartLineSegment(previous.DayIndex, i, previous.X, previous.Y, point.X, point.Y));
                previous = point;
            }
        }

        var labelStep = labelWidth > 0 && slot > 0 ? Math.Max(1, (int)Math.Ceiling(labelWidth / slot)) : 1;
        return new FinancialChartLayout(axisMin, axisMax, step, baseline, left, top, plotWidth, plotHeight, slot, labelStep,
            Array.AsReadOnly(centers), ticks.AsReadOnly(), bars.AsReadOnly(), points.AsReadOnly(), segments.AsReadOnly());

        void Include(decimal? value)
        {
            if (value is not { } number) return;
            if (number < low) low = number;
            if (number > high) high = number;
        }
    }

    private static decimal? Value(FinancialChartDay day, FinancialChartSeries series) => series switch
    {
        FinancialChartSeries.Sales => day.Sales,
        FinancialChartSeries.Refunds => day.Refunds,
        FinancialChartSeries.NetProfit => day.NetProfit,
        FinancialChartSeries.Cash => day.Cash,
        FinancialChartSeries.Qr => day.Qr,
        _ => day.RefundPaid
    };

    // The axis always contains 0 and uses a 1 / 2 / 2.5 / 5 x 10^n interval, about four intervals in total.
    private static (decimal Min, decimal Max, decimal Step) Axis(decimal low, decimal high)
    {
        var range = high - low;
        if (range <= 0m) return (0m, 4_000_000m, 1_000_000m);

        var step = NiceStep(range / 4m);
        var min = Math.Floor(low / step) * step;
        var max = Math.Ceiling(high / step) * step;
        if (max <= min) max = min + step;
        return (min, max, step);
    }

    private static decimal NiceStep(decimal raw)
    {
        var exponent = Math.Clamp((int)Math.Floor(Math.Log10((double)raw)), -8, 20);
        var magnitude = 1m;
        for (var i = 0; i < exponent; i++) magnitude *= 10m;
        for (var i = 0; i > exponent; i--) magnitude /= 10m;
        var normalized = raw / magnitude;
        var factor = normalized <= 1m ? 1m : normalized <= 2m ? 2m : normalized <= 2.5m ? 2.5m : normalized <= 5m ? 5m : 10m;
        return factor * magnitude;
    }
}

public static class FinancialChartFormat
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Short axis label: 1,5 tr, 250k, 2 tỷ.</summary>
    public static string Axis(decimal value)
    {
        var magnitude = Math.Abs(value);
        if (magnitude >= 1_000_000_000m) return $"{(value / 1_000_000_000m).ToString("0.##", Vietnamese)} tỷ";
        if (magnitude >= 1_000_000m) return $"{(value / 1_000_000m).ToString("0.##", Vietnamese)} tr";
        if (magnitude >= 1_000m) return $"{(value / 1_000m).ToString("0.#", Vietnamese)}k";
        return value.ToString("0.##", Vietnamese);
    }
}

public sealed record FinancialDonutSegment(
    string Key,
    decimal Amount,
    double Percent,
    double StartAngle,
    double SweepAngle,
    double StartX,
    double StartY,
    double EndX,
    double EndY,
    bool IsLargeArc);

public sealed record FinancialDonutLayout(
    bool IsNeutral,
    IReadOnlyList<FinancialDonutSegment> Segments,
    double CashPercent,
    double QrPercent,
    decimal? UnassignedAmount)
{
    public const string CashKey = "cash";
    public const string QrKey = "qr";

    // A sweep of exactly 360 degrees has identical start and end points, which an arc cannot draw.
    private const double FullCircleSweep = 359.9;

    /// <summary>
    /// Splits the received money into the cash and VietQR channels. Percentages are for display only.
    /// Angles are in degrees, start at 12 o'clock and run clockwise on screen (y grows downward).
    /// </summary>
    public static FinancialDonutLayout Calculate(decimal? cash, decimal? qr, decimal? total, double centerX, double centerY, double radius)
    {
        var unassigned = cash is { } c && qr is { } q && total is { } t && t > c + q ? t - c - q : (decimal?)null;
        if (cash is not { } cashAmount || qr is not { } qrAmount || cashAmount < 0m || qrAmount < 0m || cashAmount + qrAmount <= 0m
            || !double.IsFinite(centerX) || !double.IsFinite(centerY) || !double.IsFinite(radius) || radius <= 0)
            return new FinancialDonutLayout(true, Array.Empty<FinancialDonutSegment>(), 0, 0, unassigned);

        var sum = cashAmount + qrAmount;
        var cashPercent = (double)(cashAmount / sum * 100m);
        var qrPercent = (double)(qrAmount / sum * 100m);
        var segments = new List<FinancialDonutSegment>();
        var start = -90.0;
        Add(CashKey, cashAmount, cashPercent);
        Add(QrKey, qrAmount, qrPercent);
        return new FinancialDonutLayout(false, segments.AsReadOnly(), cashPercent, qrPercent, unassigned);

        void Add(string key, decimal amount, double percent)
        {
            if (amount <= 0m) return;
            var sweep = Math.Min(FullCircleSweep, percent / 100 * 360);
            var end = start + sweep;
            var (x1, y1) = Point(start);
            var (x2, y2) = Point(end);
            segments.Add(new FinancialDonutSegment(key, amount, percent, start, sweep, x1, y1, x2, y2, sweep > 180));
            start += percent / 100 * 360;
        }

        (double X, double Y) Point(double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return (centerX + radius * Math.Cos(radians), centerY + radius * Math.Sin(radians));
        }
    }
}
