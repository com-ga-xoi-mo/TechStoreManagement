using Microsoft.EntityFrameworkCore;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Common.Data;

namespace TechStore.UnitTests;

public sealed class AnalyticsQueryTranslationTests
{
    [Fact]
    public void DailyRevenueQuery_TranslatesToDatabaseGroupingAndUtcBounds()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only")
            .Options;
        using var db = new AppDbContext(options);
        var fromUtc = new DateTime(2026, 10, 1, 17, 0, 0, DateTimeKind.Utc);
        var asOfUtc = new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);

        var query = db.Database.SqlQuery<DashboardDailyRevenueSqlRow>($"""
            SELECT ((completed_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   SUM(total_amount) AS "Revenue"
            FROM orders
            WHERE status = {"Completed"}
              AND completed_at >= {fromUtc}
              AND completed_at < {asOfUtc}
            GROUP BY ((completed_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date
            ORDER BY "Date"
            """);

        var sql = query.ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("completed_at >=", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM(total_amount)", sql, StringComparison.OrdinalIgnoreCase);
    }
}
