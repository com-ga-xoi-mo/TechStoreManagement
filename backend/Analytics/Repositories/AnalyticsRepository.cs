using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;

namespace TechStore.Api.Analytics.Repositories;

public sealed class AnalyticsRepository(AppDbContext dbContext) : IAnalyticsRepository
{
    private const int ListLimit = 5;

    public async Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT statement_timestamp()";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return DateTime.SpecifyKind((DateTime)(result ?? throw new InvalidOperationException("Database did not return a snapshot timestamp.")), DateTimeKind.Utc);
    }

    public async Task<DashboardAnalyticsReadModel> GetDashboardAsync(
        DashboardAnalyticsCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var orderCount = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.CreatedAt >= criteria.FromUtc && order.CreatedAt < criteria.ToExclusiveUtc, cancellationToken);
        var activeProductCount = await dbContext.Products.AsNoTracking()
            .CountAsync(product => product.IsActive, cancellationToken);

        var lowStockQuery = dbContext.InventoryStocks.AsNoTracking()
            .Where(stock => stock.Quantity <= stock.MinStockAlert);
        var lowStockCount = await lowStockQuery.CountAsync(cancellationToken);
        var lowStockItems = await lowStockQuery
            .OrderBy(stock => stock.Quantity)
            .ThenBy(stock => stock.Product.Name)
            .ThenBy(stock => stock.Variant == null ? null : stock.Variant.VariantName)
            .ThenBy(stock => stock.Id)
            .Take(ListLimit)
            .Select(stock => new DashboardLowStockRow(
                stock.Id, stock.ProductId, stock.Product.Name, stock.VariantId,
                stock.Variant == null ? null : stock.Variant.VariantName,
                stock.Quantity, stock.ReservedQuantity, stock.MinStockAlert))
            .ToListAsync(cancellationToken);

        // The current provider cannot translate DateOnly grouping reliably. Keep the
        // time predicate sargable and perform the UTC+07 date grouping in PostgreSQL.
        var dailySqlRows = await dbContext.Database.SqlQuery<DashboardDailyRevenueSqlRow>($"""
                SELECT ((completed_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                       SUM(total_amount) AS "Revenue"
                FROM orders
                WHERE status IN ('Completed', 'PartiallyRefunded', 'Refunded')
                  AND completed_at >= {criteria.FromUtc}
                  AND completed_at < {criteria.ToExclusiveUtc}
                GROUP BY ((completed_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date
                ORDER BY "Date"
                """)
            .ToListAsync(cancellationToken);
        var dailyRevenue = dailySqlRows.Select(row => new DashboardDailyRevenueRow(row.Date, row.Revenue)).ToList();

        var recentOrderRows = await dbContext.Orders.AsNoTracking()
            .Where(order => order.CreatedAt >= criteria.FromUtc && order.CreatedAt < criteria.ToExclusiveUtc)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(ListLimit)
            .Select(order => new
            {
                order.Id,
                order.OrderCode,
                CustomerName = order.CustomerId == null ? null : order.Customer!.FullName,
                FirstItem = order.Items.OrderBy(item => item.Id).Select(item => new { item.ProductName, item.VariantName }).FirstOrDefault(),
                ItemLineCount = order.Items.Count,
                order.TotalAmount,
                Status = order.Status.ToString(),
                order.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var recentOrders = recentOrderRows.Select(row => new DashboardRecentOrderRow(
            row.Id, row.OrderCode, row.CustomerName, row.FirstItem?.ProductName,
            row.FirstItem?.VariantName, row.ItemLineCount, row.TotalAmount, row.Status, row.CreatedAt)).ToList();

        return new DashboardAnalyticsReadModel(dailyRevenue.Sum(r => r.Revenue), orderCount, activeProductCount, lowStockCount,
            dailyRevenue, lowStockItems, recentOrders);
    }
}
