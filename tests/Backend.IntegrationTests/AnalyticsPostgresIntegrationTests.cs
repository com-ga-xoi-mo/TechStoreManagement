using Microsoft.EntityFrameworkCore;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Backend.IntegrationTests;

/// <summary>
/// Opt-in integration checks. Configure TECHSTORE_ANALYTICS_TEST_POSTGRES to a dedicated,
/// disposable PostgreSQL database created for tests only; this suite replaces its schema.
/// </summary>
public sealed class AnalyticsPostgresIntegrationTests
{
    [PostgresFact]
    public async Task DashboardRepository_AggregatesAndReconcilesAgainstPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("TECHSTORE_ANALYTICS_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("PostgresFact should skip when the database variable is absent.");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) || !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing PostgreSQL integration test: database name must identify a dedicated test database.");
        if (Environment.GetEnvironmentVariable("TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET") != "YES")
            throw new InvalidOperationException("Refusing to reset PostgreSQL schema without TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET=YES.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var nowUtc = DateTime.UtcNow;
        var category = new Category { Name = "Integration", Slug = $"integration-{Guid.NewGuid():N}" };
        var cashier = new User { Username = $"analytics-{Guid.NewGuid():N}", Email = $"analytics-{Guid.NewGuid():N}@test.local", FullName = "Integration cashier" };
        var product = new Product { Category = category, Name = "Analytics phone", Sku = $"AN-{Guid.NewGuid():N}", Brand = "Test", BasePrice = 100m, CostPrice = 50m, IsActive = true };
        var customer = new Customer { FullName = "Snapshot customer", Phone = $"9{Random.Shared.Next(100000000, 999999999)}" };
        db.AddRange(category, cashier, product, customer);
        await db.SaveChangesAsync();

        var completed = new Order
        {
            OrderCode = $"AN-{Guid.NewGuid():N}", CashierUserId = cashier.Id, CustomerId = customer.Id,
            Status = OrderStatus.Completed, TotalAmount = 100m, Subtotal = 100m,
            CreatedAt = nowUtc.AddDays(-1), CompletedAt = nowUtc.AddMinutes(-5),
            Items = [new OrderItem { ProductId = product.Id, ProductName = "Historical phone", VariantName = "Blue", Sku = product.Sku, UnitPrice = 100m, TotalPrice = 100m }]
        };
        var refunded = new Order
        {
            OrderCode = $"AN-{Guid.NewGuid():N}", CashierUserId = cashier.Id, Status = OrderStatus.Refunded,
            TotalAmount = 200m, Subtotal = 200m, CreatedAt = nowUtc.AddMinutes(-4), CompletedAt = nowUtc.AddMinutes(-3)
        };
        db.AddRange(completed, refunded);
        db.InventoryStocks.Add(new InventoryStock { ProductId = product.Id, Quantity = 5, ReservedQuantity = 2, MinStockAlert = 5 });
        await db.SaveChangesAsync();

        var repository = new AnalyticsRepository(db);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var asOf = await repository.GetSnapshotTimeUtcAsync();
        var criteria = new DashboardAnalyticsCriteria("last7Days", nowUtc.AddDays(-7), asOf);
        var actual = await repository.GetDashboardAsync(criteria);
        var sqlRevenue = await db.Orders.AsNoTracking().Where(order => order.Status == OrderStatus.Completed && order.CompletedAt >= criteria.FromUtc && order.CompletedAt < criteria.AsOfUtc).SumAsync(order => (decimal?)order.TotalAmount) ?? 0m;

        Assert.Equal(100m, actual.Revenue);
        Assert.Equal(2, actual.OrderCount);
        Assert.Equal(1, actual.ActiveProductCount);
        Assert.Equal(sqlRevenue, actual.DailyRevenue.Sum(row => row.Revenue));
        Assert.Equal(1, actual.LowStockCount);
        Assert.Single(actual.LowStockItems);
        Assert.Equal("Historical phone", actual.RecentOrders.Single(row => row.Id == completed.Id).FirstProductName);
        await tx.RollbackAsync();

        Console.WriteLine($"PostgreSQL {await db.Database.SqlQueryRaw<string>("SELECT version() AS \"Value\"").SingleAsync()}; asOf={asOf:O}; revenue={actual.Revenue}; dailyTotal={actual.DailyRevenue.Sum(row => row.Revenue)}");
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TECHSTORE_ANALYTICS_TEST_POSTGRES")))
            Skip = "UNVERIFIED: set TECHSTORE_ANALYTICS_TEST_POSTGRES to a dedicated disposable PostgreSQL test database.";
    }
}
