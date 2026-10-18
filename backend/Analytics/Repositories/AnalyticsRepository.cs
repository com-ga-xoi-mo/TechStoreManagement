using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

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
        var revenueQuery = dbContext.Orders.AsNoTracking()
            .Where(order => order.Status == OrderStatus.Completed
                && order.CompletedAt >= criteria.FromUtc
                && order.CompletedAt < criteria.AsOfUtc);

        var revenue = await revenueQuery.SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        var orderCount = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.CreatedAt >= criteria.FromUtc && order.CreatedAt < criteria.AsOfUtc, cancellationToken);
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
                WHERE status = {OrderStatus.Completed.ToString()}
                  AND completed_at >= {criteria.FromUtc}
                  AND completed_at < {criteria.AsOfUtc}
                GROUP BY ((completed_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date
                ORDER BY "Date"
                """)
            .ToListAsync(cancellationToken);
        var dailyRevenue = dailySqlRows.Select(row => new DashboardDailyRevenueRow(row.Date, row.Revenue)).ToList();

        var recentOrderRows = await dbContext.Orders.AsNoTracking()
            .Where(order => order.CreatedAt >= criteria.FromUtc && order.CreatedAt < criteria.AsOfUtc)
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

        return new DashboardAnalyticsReadModel(revenue, orderCount, activeProductCount, lowStockCount,
            dailyRevenue, lowStockItems, recentOrders);
    }
}
