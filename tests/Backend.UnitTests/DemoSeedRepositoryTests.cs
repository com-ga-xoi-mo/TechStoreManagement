using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class DemoSeedRepositoryTests
{
    [Fact]
    public async Task ReconcileAsync_ShouldSeedFullFixtureAndRemainIdempotent()
    {
        // Arrange
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var seeder = new DataSeeder(new DemoSeedRepository(db), new FakeUnitOfWork(db));

        // Act
        await seeder.SeedAsync();
        var first = await CountsAsync(db);
        await seeder.SeedAsync();
        var second = await CountsAsync(db);

        // Assert
        second.Should().Be(first);
        first.Categories.Should().Be(5);
        first.Products.Should().Be(20);
        first.Customers.Should().Be(10);
        first.Orders.Should().Be(20);
        first.Serials.Should().Be(50);
        first.PurchaseReceipts.Should().Be(10);
        first.Sales.Should().Be(12);
        first.VietQr.Should().Be(6);

        var serialStatuses = await db.SerialImeis.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count);
        serialStatuses[SerialImeiStatus.Sold].Should().Be(12);
        serialStatuses[SerialImeiStatus.Reserved].Should().Be(2);
        serialStatuses[SerialImeiStatus.InStock].Should().Be(36);
        await AssertFixtureInvariantsAsync(db);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAnyRoleAlreadyExists_ShouldStillAddMissingRows()
    {
        // Arrange
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var adminRole = new TechStore.Api.Identity.Entities.Role { Id = Guid.NewGuid(), Name = RoleType.Admin };
        var admin = new TechStore.Api.Identity.Entities.User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Username = "admin",
            Email = "admin@techstore.vn", FullName = "User edited admin profile",
            PasswordHash = "preserve-this-password-hash", IsActive = true
        };
        var existingCustomer = new TechStore.Api.Customers.Entities.Customer
        {
            Id = Guid.NewGuid(), FullName = "User edited customer profile", Phone = "0901234567",
            Email = "private@example.test", LoyaltyPoints = 150, Tier = CustomerTier.Standard, IsActive = true
        };
        db.Roles.Add(adminRole);
        db.Users.Add(admin);
        db.UserRoles.Add(new TechStore.Api.Identity.Entities.UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        db.Customers.Add(existingCustomer);
        await db.SaveChangesAsync();
        var seeder = new DataSeeder(new DemoSeedRepository(db), new FakeUnitOfWork(db));

        // Act
        await seeder.SeedAsync();

        // Assert
        (await db.Roles.CountAsync()).Should().Be(4);
        (await db.Products.CountAsync()).Should().Be(20);
        (await db.Orders.CountAsync()).Should().Be(20);
        (await db.Users.SingleAsync(x => x.Username == "admin")).PasswordHash.Should().Be("preserve-this-password-hash");
        (await db.Users.SingleAsync(x => x.Username == "admin")).FullName.Should().Be("User edited admin profile");
        (await db.Customers.SingleAsync(x => x.Phone == "0901234567")).FullName.Should().Be("User edited customer profile");
        (await db.Customers.SingleAsync(x => x.Phone == "0901234567")).Email.Should().Be("private@example.test");
    }

    [Fact]
    public async Task ReconcileAsync_ShouldReuseLegacyNaturalKeysAndKeepTheirIdentifiers()
    {
        // Arrange: Sprint 1 rows have generated IDs instead of the current deterministic seed IDs.
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var categorySeeds = DemoSeedManifest.Categories.Take(2).ToArray();
        var categories = categorySeeds.ToDictionary(x => x.Slug, x => new TechStore.Api.Catalog.Entities.Category
        {
            Id = Guid.NewGuid(), Slug = x.Slug, Name = x.Name, IsActive = true
        });
        var products = DemoSeedManifest.Products.Take(5).ToArray().ToDictionary(x => x.Sku, x => new TechStore.Api.Catalog.Entities.Product
        {
            Id = Guid.NewGuid(), CategoryId = categories[x.CategorySlug].Id, Name = x.Name, Sku = x.Sku,
            Brand = x.Brand, BasePrice = x.Price, CostPrice = x.Cost, Specs = x.Specs,
            IsSerialTracked = x.SerialTracked, IsActive = true
        });
        db.Categories.AddRange(categories.Values);
        db.Products.AddRange(products.Values);
        foreach (var seed in DemoSeedManifest.Products.Take(5).Where(x => x.VariantSku != null))
            db.ProductVariants.Add(new TechStore.Api.Catalog.Entities.ProductVariant
            {
                Id = Guid.NewGuid(), ProductId = products[seed.Sku].Id, Sku = seed.VariantSku!,
                VariantName = seed.Name, Price = seed.Price, Specs = "{}", IsActive = true
            });
        foreach (var seed in DemoSeedManifest.Customers.Take(2))
            db.Customers.Add(new TechStore.Api.Customers.Entities.Customer
            {
                Id = Guid.NewGuid(), FullName = seed.Name, Phone = seed.Phone, Email = seed.Email,
                LoyaltyPoints = seed.LoyaltyPoints, Tier = seed.Tier, IsActive = true
            });
        db.Suppliers.Add(new TechStore.Api.Inventory.Entities.Supplier
        {
            Id = Guid.NewGuid(), Name = "FPT Synnex Distribution", Phone = "02873006666", IsActive = true
        });
        await db.SaveChangesAsync();
        var categoryIds = categories.ToDictionary(x => x.Key, x => x.Value.Id);
        var productIds = products.ToDictionary(x => x.Key, x => x.Value.Id);
        var variantIds = await db.ProductVariants.ToDictionaryAsync(x => x.Sku, x => x.Id);
        var customerIds = await db.Customers.ToDictionaryAsync(x => x.Phone, x => x.Id);
        var supplierId = await db.Suppliers.Select(x => x.Id).SingleAsync();

        // Act
        await new DataSeeder(new DemoSeedRepository(db), new FakeUnitOfWork(db)).SeedAsync();

        // Assert
        foreach (var (slug, id) in categoryIds)
            (await db.Categories.SingleAsync(x => x.Slug == slug)).Id.Should().Be(id);
        foreach (var (sku, id) in productIds)
            (await db.Products.SingleAsync(x => x.Sku == sku)).Id.Should().Be(id);
        foreach (var (sku, id) in variantIds)
            (await db.ProductVariants.SingleAsync(x => x.Sku == sku)).Id.Should().Be(id);
        foreach (var (phone, id) in customerIds)
            (await db.Customers.SingleAsync(x => x.Phone == phone)).Id.Should().Be(id);
        (await db.Suppliers.SingleAsync(x => x.Name == "FPT Synnex Distribution")).Id.Should().Be(supplierId);
    }

    [Fact]
    public async Task ReconcileAsync_WhenFixtureRowConflicts_ShouldNotPersistPartialRepair()
    {
        // Arrange
        var options = CreateOptions();
        await using (var db = new AppDbContext(options))
        {
            var seeder = new DataSeeder(new DemoSeedRepository(db), new FakeUnitOfWork(db));
            await seeder.SeedAsync();
            var firstDemoCustomer = await db.Customers.SingleAsync(x => x.Phone == DemoSeedManifest.Customers[2].Phone);
            var loyalty = await db.CustomerLoyaltyPoints.SingleAsync(x => x.CustomerId == firstDemoCustomer.Id && x.Reason == "Initial Sprint 2 demo loyalty balance");
            db.CustomerLoyaltyPoints.Remove(loyalty);
            var order = await db.Orders.SingleAsync(x => x.OrderCode == "DEMO-202610-0001");
            order.TotalAmount = 1m;
            await db.SaveChangesAsync();

            var act = () => seeder.SeedAsync();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DEMO-202610-0001*");
        }

        await using var verify = new AppDbContext(options);
        (await verify.CustomerLoyaltyPoints.CountAsync(x => x.Reason == "Initial Sprint 2 demo loyalty balance")).Should().Be(7);
        (await verify.Orders.Where(x => x.OrderCode == "DEMO-202610-0001").Select(x => x.TotalAmount).SingleAsync()).Should().Be(1m);
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;

    private static async Task<SeedCounts> CountsAsync(AppDbContext db) => new(
        await db.Categories.CountAsync(), await db.Products.CountAsync(), await db.Customers.CountAsync(),
        await db.Orders.CountAsync(), await db.SerialImeis.CountAsync(),
        await db.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.PurchaseReceipt),
        await db.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.Sale),
        await db.VietQrTransactions.CountAsync());

    private static async Task AssertFixtureInvariantsAsync(AppDbContext db)
    {
        foreach (var product in await db.Products.Where(x => x.Sku.StartsWith("DEMO-") && x.IsSerialTracked).ToListAsync())
        {
            var stock = await db.InventoryStocks.SingleAsync(x => x.ProductId == product.Id);
            var activeSerials = await db.SerialImeis.CountAsync(x => x.ProductId == product.Id &&
                (x.Status == SerialImeiStatus.InStock || x.Status == SerialImeiStatus.Reserved));
            var reservedSerials = await db.SerialImeis.CountAsync(x => x.ProductId == product.Id && x.Status == SerialImeiStatus.Reserved);
            stock.Quantity.Should().Be(activeSerials);
            stock.ReservedQuantity.Should().Be(reservedSerials);
        }

        (await db.PurchaseOrderItems.SumAsync(x => x.ReceivedQuantity)).Should().Be(50);
        (await db.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.PurchaseReceipt).SumAsync(x => x.QuantityChange)).Should().Be(50);
        (await db.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Sale).SumAsync(x => x.QuantityChange)).Should().Be(-12);
        (await db.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Sale && x.OrderItemId != null)
            .Select(x => x.OrderItemId).Distinct().CountAsync()).Should().Be(12);
        foreach (var stock in await db.InventoryStocks.Where(x => x.Product.Sku.StartsWith("DEMO-") && x.Product.IsSerialTracked).ToListAsync())
        {
            var ledger = await db.InventoryMovements.Where(x => x.InventoryStockId == stock.Id)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync();
            var runningQuantity = 0;
            foreach (var movement in ledger)
            {
                movement.QuantityChange.Should().NotBe(0);
                runningQuantity += movement.QuantityChange;
                movement.QuantityAfter.Should().Be(runningQuantity);
            }
            runningQuantity.Should().Be(stock.Quantity);
        }

        var completedOrders = await db.Orders.Where(x => x.OrderCode.StartsWith("DEMO-") && x.Status == OrderStatus.Completed).ToListAsync();
        completedOrders.Should().OnlyContain(x => x.PaidAt == x.CompletedAt && x.CompletedAt != null);
        var completedSerialIds = await db.OrderItems.Where(x => x.Order.OrderCode.StartsWith("DEMO-") && x.Order.Status == OrderStatus.Completed)
            .Select(x => x.SerialImeiId).ToListAsync();
        completedSerialIds.Should().OnlyContain(x => x.HasValue);
        (await db.SerialImeis.CountAsync(x => completedSerialIds.Contains(x.Id) && x.Status == SerialImeiStatus.Sold && x.WarrantyStartAt != null)).Should().Be(12);

        var processingOrders = await db.Orders.Where(x => x.OrderCode.StartsWith("DEMO-") && x.Status == OrderStatus.Processing).ToListAsync();
        processingOrders.Should().OnlyContain(x => x.PaidAt != null && x.CompletedAt == null);
        (await db.OrderItems.CountAsync(x => x.Order.OrderCode.StartsWith("DEMO-") && x.Order.Status == OrderStatus.Processing &&
            x.SerialImei!.Status == SerialImeiStatus.Reserved)).Should().Be(2);
        (await db.Orders.CountAsync(x => x.OrderCode.StartsWith("DEMO-") &&
            (x.Status == OrderStatus.Pending || x.Status == OrderStatus.Cancelled) && x.PaidAt == null)).Should().Be(6);
        (await db.OrderItems.CountAsync(x => x.Order.OrderCode.StartsWith("DEMO-") &&
            (x.Order.Status == OrderStatus.Pending || x.Order.Status == OrderStatus.Cancelled) && x.SerialImeiId != null)).Should().Be(0);

        var qr = await db.VietQrTransactions.Where(x => x.TransactionCode.StartsWith("DQR-DEMO-")).ToListAsync();
        qr.Should().OnlyContain(x => x.Status == VietQrStatus.Confirmed && x.ConfirmedAt != null && x.Amount > 0);
        var paidOrders = await db.Orders.Where(x => x.PosSessionId == DemoSeedManifest.Id("pos-session:demo-october-2026") && x.PaidAt != null).ToListAsync();
        var session = await db.PosSessions.SingleAsync(x => x.Id == DemoSeedManifest.Id("pos-session:demo-october-2026"));
        session.CashSalesTotal.Should().Be(paidOrders.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.TotalAmount));
        session.VietQrSalesTotal.Should().Be(paidOrders.Where(x => x.PaymentMethod == PaymentMethod.VietQr).Sum(x => x.TotalAmount));

        foreach (var customer in await db.Customers.Where(x => x.Phone.StartsWith("09300000")).ToListAsync())
        {
            var latest = await db.CustomerLoyaltyPoints.Where(x => x.CustomerId == customer.Id)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstAsync();
            latest.BalanceAfter.Should().Be(customer.LoyaltyPoints);
        }
        (await db.Vouchers.SingleAsync(x => x.Code == "DEMO-OCT26")).UsedCount.Should().Be(0);
    }

    private sealed record SeedCounts(int Categories, int Products, int Customers, int Orders, int Serials,
        int PurchaseReceipts, int Sales, int VietQr);
}
