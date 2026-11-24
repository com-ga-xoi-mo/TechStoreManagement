using Microsoft.EntityFrameworkCore;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Analytics.Services;
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
        var partial = new Order
        {
            OrderCode = $"AN-{Guid.NewGuid():N}", CashierUserId = cashier.Id, Status = OrderStatus.PartiallyRefunded,
            TotalAmount = 70m, Subtotal = 70m, CreatedAt = nowUtc.AddMinutes(-4), CompletedAt = nowUtc.AddMinutes(-3)
        };
        var undelivered = new Order
        {
            OrderCode = $"AN-{Guid.NewGuid():N}", CashierUserId = cashier.Id, Status = OrderStatus.Refunded,
            TotalAmount = 500m, Subtotal = 500m, PaidAt = nowUtc.AddMinutes(-3), CreatedAt = nowUtc.AddMinutes(-4)
        };
        db.AddRange(completed, refunded, partial, undelivered);
        db.InventoryStocks.Add(new InventoryStock { ProductId = product.Id, Quantity = 5, ReservedQuantity = 2, MinStockAlert = 5 });
        await db.SaveChangesAsync();

        var repository = new AnalyticsRepository(db);
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead))
        {
            var asOf = await repository.GetSnapshotTimeUtcAsync();
            var criteria = new DashboardAnalyticsCriteria("last7Days", nowUtc.AddDays(-7), asOf);
            var actual = await repository.GetDashboardAsync(criteria);
            Assert.Equal(370m, actual.Revenue);
            Assert.Equal(4, actual.OrderCount);
            Assert.Equal(1, actual.ActiveProductCount);
            Assert.Equal(actual.Revenue, actual.DailyRevenue.Sum(row => row.Revenue));
            Assert.Equal(1, actual.LowStockCount);
            Assert.Single(actual.LowStockItems);
            Assert.Equal("Historical phone", actual.RecentOrders.Single(row => row.Id == completed.Id).FirstProductName);
            await tx.RollbackAsync();
        }

        // The Vietnam calendar interval 12–14 October is [11 Oct 17:00Z, 14 Oct 17:00Z).
        var start = new DateTime(2024, 10, 11, 17, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(3);
        var createdBefore = Complete(120m, start.AddSeconds(-1), start);
        var justBeforeEnd = Complete(75m, end.AddMinutes(-2), end.AddSeconds(-1));
        var completedAtEnd = Complete(300m, start, end);
        db.AddRange(createdBefore, justBeforeEnd, completedAtEnd,
            Complete(450m, end, end), Complete(950m, start.AddMinutes(-2), start.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var service = new AnalyticsService(repository, new UnitOfWork(db));
        var custom = await service.GetDashboardAsync(new("custom", (new(2024, 10, 12), new(2024, 10, 14))));
        Assert.NotNull(custom.Snapshot);
        Assert.Equal("custom", custom.Snapshot.Period);
        Assert.True(custom.Snapshot.AsOf > end);
        Assert.Equal(195m, custom.Snapshot.Summary.Revenue);
        Assert.Equal(2, custom.Snapshot.Summary.OrderCount);
        Assert.Equal(new[] { 120m, 0m, 75m }, custom.Snapshot.DailyRevenue.Select(row => row.Revenue));
        Assert.Equal(custom.Snapshot.Summary.Revenue, custom.Snapshot.DailyRevenue.Sum(row => row.Revenue));
        Assert.Equal(new[] { justBeforeEnd.Id, completedAtEnd.Id }, custom.Snapshot.RecentOrders.Select(row => row.Id));
        Assert.Equal(1, custom.Snapshot.Summary.ActiveProductCount);
        Assert.Equal(1, custom.Snapshot.Summary.LowStockCount);
        Assert.Equal(3, Assert.Single(custom.Snapshot.LowStockItems).AvailableQuantity);

        Order Complete(decimal total, DateTime created, DateTime settled) => new()
        {
            OrderCode = $"AN-{Guid.NewGuid():N}", CashierUserId = cashier.Id, Status = OrderStatus.Completed,
            TotalAmount = total, Subtotal = total, CreatedAt = created, PaidAt = settled, CompletedAt = settled
        };
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
