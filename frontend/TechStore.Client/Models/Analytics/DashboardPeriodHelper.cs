using System;

namespace TechStore.Client.Models.Analytics;

public static class DashboardPeriodHelper
{
    private static readonly TimeZoneInfo HoChiMinhTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public static DateTimeOffset ToHoChiMinhTime(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, HoChiMinhTimeZone);

    public static (DateTimeOffset Start, DateTimeOffset EndExclusive) GetPeriodRange(
        DashboardPeriod period,
        DateTimeOffset? now = null)
    {
        var localNow = ToHoChiMinhTime(now ?? DateTimeOffset.UtcNow);
        var todayStart = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, localNow.Offset);

        return period switch
        {
            DashboardPeriod.Today => (todayStart, todayStart.AddDays(1)),
            DashboardPeriod.Last7Days => (todayStart.AddDays(-6), todayStart.AddDays(1)),
            DashboardPeriod.ThisMonth =>
                (new DateTimeOffset(localNow.Year, localNow.Month, 1, 0, 0, 0, localNow.Offset),
                 new DateTimeOffset(localNow.Year, localNow.Month, 1, 0, 0, 0, localNow.Offset).AddMonths(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    public static (DateTimeOffset Start, DateTimeOffset EndExclusive) GetComparisonRange(
        DateTimeOffset currentStart,
        DateTimeOffset currentEndExclusive)
    {
        var duration = currentEndExclusive - currentStart;
        return (currentStart - duration, currentStart);
    }
}
