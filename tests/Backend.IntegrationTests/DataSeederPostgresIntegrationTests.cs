using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Backend.IntegrationTests;

public sealed class DataSeederPostgresIntegrationTests
{
    [SeedPostgresFact]
    public async Task EmptyDatabase_ShouldSeedLifecycleConsistentDataset_AndRerunWithoutEffects()
    {
        await using var fixture = await SeedPostgresSchema.CreateAsync();
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            await SeedAsync(db);
            await AssertExpandedDatasetAsync(db);
        }

        var firstSnapshot = await CaptureCountsAsync(fixture);
        await using (var db = fixture.CreateContext())
        {
            await SeedAsync(db);
            await AssertExpandedDatasetAsync(db);
        }
        var secondSnapshot = await CaptureCountsAsync(fixture);
        Assert.Equal(firstSnapshot, secondSnapshot);

        await AssertAnalyticsReferenceAsync(fixture);
    }

    [SeedPostgresFact]
    public async Task SprintOneBaseline_ShouldKeepLegacyIdentifiersAndCredentialsWhileExpanding()
    {
        await using var fixture = await SeedPostgresSchema.CreateAsync();
        Guid adminId;
        string adminHash;
        Dictionary<string, Guid> legacyProductIds;
        Dictionary<string, Guid> legacyCategoryIds;
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            (adminId, adminHash, legacyProductIds, legacyCategoryIds) = await InsertSprintOneBaselineAsync(db);
            await SeedAsync(db);

            var admin = await db.Users.SingleAsync(x => x.Username == "admin");
            Assert.Equal(adminId, admin.Id);
            Assert.Equal(adminHash, admin.PasswordHash);
            foreach (var (sku, id) in legacyProductIds)
                Assert.Equal(id, await db.Products.Where(x => x.Sku == sku).Select(x => x.Id).SingleAsync());
            foreach (var (slug, id) in legacyCategoryIds)
                Assert.Equal(id, await db.Categories.Where(x => x.Slug == slug).Select(x => x.Id).SingleAsync());
            await AssertExpandedDatasetAsync(db);
        }
    }

    [SeedPostgresFact]
    public async Task PartialSeed_ShouldAddMissingChild_AndRollbackWhenNaturalKeyConflicts()
    {
        await using var fixture = await SeedPostgresSchema.CreateAsync();
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            await SeedAsync(db);
            var firstDemoCustomer = await db.Customers.SingleAsync(x => x.Phone == DemoSeedManifest.Customers[2].Phone);
            var loyalty = await db.CustomerLoyaltyPoints.SingleAsync(x => x.CustomerId == firstDemoCustomer.Id && x.Reason == "Initial Sprint 2 demo loyalty balance");
            db.CustomerLoyaltyPoints.Remove(loyalty);
            var conflictingOrder = await db.Orders.SingleAsync(x => x.OrderCode == "DEMO-202610-0001");
            conflictingOrder.TotalAmount = 1m;
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
        {
            var seeder = CreateSeeder(db);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
            Assert.Contains("DEMO-202610-0001", exception.Message, StringComparison.Ordinal);
            Assert.Equal(7, await db.CustomerLoyaltyPoints.CountAsync(x => x.Reason == "Initial Sprint 2 demo loyalty balance"));
            Assert.Equal(1m, await db.Orders.Where(x => x.OrderCode == "DEMO-202610-0001").Select(x => x.TotalAmount).SingleAsync());
        }

        await using (var db = fixture.CreateContext())
        {
            var order = await db.Orders.SingleAsync(x => x.OrderCode == "DEMO-202610-0001");
            order.TotalAmount = 16990000m;
            await db.SaveChangesAsync();
            await SeedAsync(db);
            Assert.True(await db.CustomerLoyaltyPoints.AnyAsync(x => x.Reason == "Initial Sprint 2 demo loyalty balance"));
            await AssertExpandedDatasetAsync(db);
        }
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        await CreateSeeder(db).SeedAsync();
    }

    private static IDataSeeder CreateSeeder(AppDbContext db) =>
        new DataSeeder(new DemoSeedRepository(db), new UnitOfWork(db));

    private static async Task AssertExpandedDatasetAsync(AppDbContext db)
    {
        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.True(await db.Categories.CountAsync() >= 5);
        Assert.True(await db.Products.CountAsync() >= 20);
        Assert.True(await db.Customers.CountAsync() >= 10);
        Assert.True(await db.Orders.CountAsync() >= 20);
        Assert.True(await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.Length == 15) >= 50);
        Assert.True(await db.SerialImeis.Where(x => x.Imei != null).Select(x => x.Imei).Distinct().CountAsync() >= 50);
        Assert.Equal(50, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000")));
        Assert.Equal(20, await db.Orders.CountAsync(x => x.OrderCode.StartsWith("DEMO-202610-")));
        Assert.Equal(12, await db.Orders.CountAsync(x => x.OrderCode.StartsWith("DEMO-") && x.Status == OrderStatus.Completed));
        Assert.Equal(2, await db.Orders.CountAsync(x => x.OrderCode.StartsWith("DEMO-") && x.Status == OrderStatus.Processing));
        Assert.Equal(3, await db.Orders.CountAsync(x => x.OrderCode.StartsWith("DEMO-") && x.Status == OrderStatus.Pending));
        Assert.Equal(3, await db.Orders.CountAsync(x => x.OrderCode.StartsWith("DEMO-") && x.Status == OrderStatus.Cancelled));

        var trackedProducts = await db.Products.Where(x => x.Sku.StartsWith("DEMO-") && x.IsSerialTracked).ToListAsync();
        foreach (var product in trackedProducts)
        {
            var stock = await db.InventoryStocks.SingleAsync(x => x.ProductId == product.Id);
            var activeSerialCount = await db.SerialImeis.CountAsync(x => x.ProductId == product.Id &&
                (x.Status == SerialImeiStatus.InStock || x.Status == SerialImeiStatus.Reserved));
            var reservedSerialCount = await db.SerialImeis.CountAsync(x => x.ProductId == product.Id && x.Status == SerialImeiStatus.Reserved);
            Assert.Equal(stock.Quantity, activeSerialCount);
            Assert.Equal(stock.ReservedQuantity, reservedSerialCount);
        }

        Assert.Equal(50, await db.SerialImeis.CountAsync(x => x.Imei != null && System.Text.RegularExpressions.Regex.IsMatch(x.Imei, "^[0-9]{15}$")));
        Assert.Equal(10, await db.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.PurchaseReceipt && x.PurchaseOrderItemId != null));
        Assert.Equal(12, await db.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.Sale && x.OrderItemId != null));
        Assert.Equal(6, await db.VietQrTransactions.CountAsync(x => x.TransactionCode.StartsWith("DQR-DEMO-")));
        Assert.Equal(12, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000") && x.Status == SerialImeiStatus.Sold && x.WarrantyStartAt != null && x.WarrantyEndAt != null));
        Assert.Equal(2, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000") && x.Status == SerialImeiStatus.Reserved));
        Assert.Equal(36, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000") && x.Status == SerialImeiStatus.InStock));
    }

    private static async Task<Counts> CaptureCountsAsync(SeedPostgresSchema fixture)
    {
        await using var db = fixture.CreateContext();
        return new Counts(
            await db.Roles.CountAsync(), await db.Categories.CountAsync(), await db.Products.CountAsync(),
            await db.ProductVariants.CountAsync(), await db.Customers.CountAsync(), await db.Orders.CountAsync(),
            await db.InventoryStocks.CountAsync(), await db.InventoryMovements.CountAsync(),
            await db.SerialImeis.CountAsync(), await db.VietQrTransactions.CountAsync(),
            await db.CustomerLoyaltyPoints.CountAsync());
    }

    private static async Task AssertAnalyticsReferenceAsync(SeedPostgresSchema fixture)
    {
        await using var db = fixture.CreateContext();
        var repository = new AnalyticsRepository(db);
        var asOf = DemoSeedManifest.AnchorUtc;
        var todayFrom = new DateTime(2026, 10, 17, 17, 0, 0, DateTimeKind.Utc);
        var weekFrom = new DateTime(2026, 10, 11, 17, 0, 0, DateTimeKind.Utc);
        var monthFrom = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);

        var today = await repository.GetDashboardAsync(new DashboardAnalyticsCriteria("today", todayFrom, asOf));
        var week = await repository.GetDashboardAsync(new DashboardAnalyticsCriteria("last7Days", weekFrom, asOf));
        var month = await repository.GetDashboardAsync(new DashboardAnalyticsCriteria("thisMonth", monthFrom, asOf));

        Assert.Equal(20, week.ActiveProductCount);
        Assert.Equal(27_980_000m, today.Revenue);
        Assert.Equal(136_080_000m, week.Revenue);
        Assert.Equal(136_080_000m, month.Revenue);
        Assert.Equal(2, today.OrderCount);
        Assert.Equal(20, week.OrderCount);
        Assert.Equal(7, week.LowStockCount);
        Assert.Equal(5, week.LowStockItems.Count);
        var dailyExpected = new[] { 14_480_000m, 35_980_000m, 0m, 14_980_000m, 22_980_000m, 19_680_000m, 27_980_000m };
        var dailyByDate = week.DailyRevenue.ToDictionary(x => x.Date, x => x.Revenue);
        var dailyWithZeroFill = Enumerable.Range(0, 7)
            .Select(offset => dailyByDate.GetValueOrDefault(new DateOnly(2026, 10, 12).AddDays(offset)))
            .ToArray();
        Assert.Equal(dailyExpected, dailyWithZeroFill);
        Assert.Equal(136_080_000m, dailyWithZeroFill.Sum());
        Assert.Contains(week.RecentOrders, x => x.Status == nameof(OrderStatus.Processing));
        Assert.Equal(5, week.RecentOrders.Count);
        Assert.Equal(
            new[] { "DEMO-202610-0011", "DEMO-202610-0006", "DEMO-202610-0001", "DEMO-202610-0007", "DEMO-202610-0013" },
            week.RecentOrders.Select(x => x.OrderCode));
        Assert.Equal(
            new[] { "Anker 735 Charger 65W", "Jabra Evolve2 65 Flex", "Logitech Brio 4K Webcam", "Logitech MX Master 3S", "USB-C 100W Braided Cable" },
            week.LowStockItems.Select(x => x.ProductName));
        Assert.Equal(20, week.OrderCount);
        Assert.Equal(136_080_000m, month.Revenue);
    }

    private static async Task<(Guid AdminId, string AdminHash, Dictionary<string, Guid> ProductIds, Dictionary<string, Guid> CategoryIds)> InsertSprintOneBaselineAsync(AppDbContext db)
    {
        var categorySeeds = DemoSeedManifest.Categories.Take(2).ToArray();
        var categories = categorySeeds.ToDictionary(
            x => x.Slug,
            x => new Category { Id = Guid.NewGuid(), Slug = x.Slug, Name = x.Name, IsActive = true },
            StringComparer.OrdinalIgnoreCase);
        db.Categories.AddRange(categories.Values);

        var productSeeds = DemoSeedManifest.Products.Take(5).ToArray();
        var products = productSeeds.ToDictionary(x => x.Sku, x => new Product
        {
            Id = Guid.NewGuid(), CategoryId = categories[x.CategorySlug].Id, Name = x.Name, Sku = x.Sku,
            Brand = x.Brand, BasePrice = x.Price, CostPrice = x.Cost, Specs = x.Specs,
            IsSerialTracked = x.SerialTracked, IsActive = true
        }, StringComparer.OrdinalIgnoreCase);
        db.Products.AddRange(products.Values);
        foreach (var seed in productSeeds.Where(x => x.VariantSku != null))
        {
            db.ProductVariants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(), ProductId = products[seed.Sku].Id, VariantName = seed.Name,
                Sku = seed.VariantSku!, Price = seed.Price, Specs = "{}", IsActive = true
            });
        }

        foreach (var seed in DemoSeedManifest.Customers.Take(2))
            db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(), FullName = seed.Name, Phone = seed.Phone, Email = seed.Email,
                DateOfBirth = seed.DateOfBirth, LoyaltyPoints = seed.LoyaltyPoints, Tier = seed.Tier,
                IsActive = true, CreatedAt = seed.CreatedAt
            });

        var roles = Enum.GetValues<RoleType>().ToDictionary(x => x, x => new Role { Id = Guid.NewGuid(), Name = x });
        db.Roles.AddRange(roles.Values);
        var admin = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Username = "admin",
            Email = "admin@techstore.vn", FullName = "Legacy Admin", PasswordHash = "legacy-password-hash",
            IsActive = true
        };
        var cashier = new User { Id = Guid.NewGuid(), Username = "cashier1", Email = "cashier1@techstore.vn", FullName = "Legacy Cashier", PasswordHash = "cashier-hash", IsActive = true };
        var warehouse = new User { Id = Guid.NewGuid(), Username = "warehouse1", Email = "warehouse1@techstore.vn", FullName = "Legacy Warehouse", PasswordHash = "warehouse-hash", IsActive = true };
        db.Users.AddRange(admin, cashier, warehouse);
        db.UserRoles.AddRange(
            new UserRole { UserId = admin.Id, RoleId = roles[RoleType.Admin].Id, AssignedAt = DemoSeedManifest.AnchorUtc.AddDays(-90) },
            new UserRole { UserId = cashier.Id, RoleId = roles[RoleType.SalesStaff].Id, AssignedAt = DemoSeedManifest.AnchorUtc.AddDays(-90) },
            new UserRole { UserId = warehouse.Id, RoleId = roles[RoleType.WarehouseStaff].Id, AssignedAt = DemoSeedManifest.AnchorUtc.AddDays(-90) });
        db.Suppliers.Add(new Supplier { Id = Guid.NewGuid(), Name = "FPT Synnex Distribution", Phone = "02873006666", IsActive = true });
        await db.SaveChangesAsync();
        return (admin.Id, admin.PasswordHash, products.ToDictionary(x => x.Key, x => x.Value.Id), categories.ToDictionary(x => x.Key, x => x.Value.Id));
    }

    private sealed record Counts(int Roles, int Categories, int Products, int Variants, int Customers, int Orders,
        int Stocks, int Movements, int Serials, int VietQr, int Loyalty);
}

internal sealed class SeedPostgresFactAttribute : FactAttribute
{
    public SeedPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TECHSTORE_SEED_TEST_POSTGRES")))
            Skip = "UNVERIFIED: set TECHSTORE_SEED_TEST_POSTGRES to a dedicated PostgreSQL test database to run seed verification.";
    }
}

internal sealed class SeedPostgresSchema : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _schema;
    private readonly string _schemaConnectionString;

    private SeedPostgresSchema(string adminConnectionString, string schema, string schemaConnectionString)
    {
        _adminConnectionString = adminConnectionString;
        _schema = schema;
        _schemaConnectionString = schemaConnectionString;
    }

    public static async Task<SeedPostgresSchema> CreateAsync()
    {
        var rawConnection = Environment.GetEnvironmentVariable("TECHSTORE_SEED_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(rawConnection)) throw new InvalidOperationException("PostgreSQL seed verification is unverified because TECHSTORE_SEED_TEST_POSTGRES is not set.");
        if (Environment.GetEnvironmentVariable("TECHSTORE_SEED_TEST_ALLOW_SCHEMA") != "YES")
            throw new InvalidOperationException("Set TECHSTORE_SEED_TEST_ALLOW_SCHEMA=YES to allow creation and removal of an isolated test schema.");

        var builder = new NpgsqlConnectionStringBuilder(rawConnection);
        if (string.IsNullOrWhiteSpace(builder.Database) || !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing seed verification: PostgreSQL database name must identify a dedicated test database.");

        var schema = $"seedtest_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(rawConnection))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await command.ExecuteNonQueryAsync();
        }
        builder.SearchPath = schema;
        return new SeedPostgresSchema(rawConnection, schema, builder.ConnectionString);
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_schemaConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }
}
