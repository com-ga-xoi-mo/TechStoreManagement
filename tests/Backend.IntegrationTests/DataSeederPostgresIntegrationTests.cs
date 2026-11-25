using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Common.Storage;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.Backend.IntegrationTests;

public sealed class DataSeederPostgresIntegrationTests(ITestOutputHelper output)
{
    [SeedPostgresFact]
    public async Task EmptyDatabase_ShouldSeedLifecycleConsistentDataset_AndRerunWithoutEffects()
    {
        await using var fixture = await SeedPostgresSchema.CreateAsync();
        await using var host = new SeedPostgresApi(fixture);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            await SeedAsync(fixture, db);
            await AssertExpandedDatasetAsync(db);
        }

        var firstSnapshot = await CaptureCountsAsync(fixture);
        var firstCatalog = await CaptureCatalogAsync(fixture);
        await using (var db = fixture.CreateContext())
        {
            await SeedAsync(fixture, db);
            await AssertExpandedDatasetAsync(db);
        }
        var secondSnapshot = await CaptureCountsAsync(fixture);
        Assert.Equal(firstSnapshot, secondSnapshot);
        Assert.Equal(firstCatalog, await CaptureCatalogAsync(fixture));
        Assert.Equal(20, fixture.CaptureFiles().Length);

        foreach (var (username, password, role) in new[]
        {
            ("admin", "Admin@123", RoleType.Admin),
            ("cashier1", "Cashier@123", RoleType.SalesStaff),
            ("warehouse1", "Warehouse@123", RoleType.WarehouseStaff),
            ("manager1", "Manager@123", RoleType.Manager)
        })
        {
            Assert.Equal(role, (await LoginAsync(client, username, password)).User.Role);
        }
        await AssertCoversAsync(fixture, client);

        await AssertAnalyticsReferenceAsync(client);
    }

    [SeedPostgresFact]
    public async Task SprintOneBaseline_ShouldKeepLegacyIdentifiersAndCredentialsWhileExpanding()
    {
        await using var fixture = await SeedPostgresSchema.CreateAsync();
        Guid adminId;
        string adminHash;
        Dictionary<string, Guid> legacyProductIds;
        Dictionary<string, Guid> legacyCategoryIds;
        var legacyTime = DemoSeedManifest.AnchorUtc.AddMonths(-2);
        var oldCover = "/uploads/legacy/cover-only.png";
        var galleryKey = "legacy/gallery.png";
        var galleryId = Guid.NewGuid();
        using var oldImageStream = typeof(DemoSeedManifest).Assembly.GetManifestResourceStream("DemoProductImages.smartphones.png")!;
        using var oldImageBuffer = new MemoryStream();
        await oldImageStream.CopyToAsync(oldImageBuffer);
        var oldImage = oldImageBuffer.ToArray();
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            (adminId, adminHash, legacyProductIds, legacyCategoryIds) = await InsertSprintOneBaselineAsync(db);
            var coverOnly = await db.Products.SingleAsync(x => x.Sku == "IP15PM-256");
            coverOnly.ImageUrl = oldCover;
            coverOnly.CostPrice = 123456m;
            coverOnly.CreatedAt = legacyTime;
            var gallery = await db.Products.SingleAsync(x => x.Sku == "S24U-256");
            db.Set<ProductImage>().Add(new()
            {
                Id = galleryId, ProductId = gallery.Id, ObjectKey = galleryKey,
                Url = await fixture.Storage.PutAsync(galleryKey, oldImage, "image/png"),
                ContentType = "image/png", SizeBytes = oldImage.Length, CreatedAt = legacyTime
            });
            await db.SaveChangesAsync();
            await SeedAsync(fixture, db);
        }

        await using (var db = fixture.CreateContext())
        {
            var admin = await db.Users.SingleAsync(x => x.Username == "admin");
            Assert.Equal(adminId, admin.Id);
            Assert.Equal(adminHash, admin.PasswordHash);
            foreach (var (sku, id) in legacyProductIds)
                Assert.Equal(id, await db.Products.Where(x => x.Sku == sku).Select(x => x.Id).SingleAsync());
            foreach (var (slug, id) in legacyCategoryIds)
                Assert.Equal(id, await db.Categories.Where(x => x.Slug == slug).Select(x => x.Id).SingleAsync());
            var coverOnly = await db.Products.Include(x => x.Images).SingleAsync(x => x.Sku == "IP15PM-256");
            Assert.Equal(oldCover, coverOnly.ImageUrl);
            Assert.Empty(coverOnly.Images);
            Assert.Equal(123456m, coverOnly.CostPrice);
            Assert.Equal(legacyTime, coverOnly.CreatedAt);
            var gallery = await db.Products.Include(x => x.Images).SingleAsync(x => x.Sku == "S24U-256");
            Assert.Null(gallery.ImageUrl);
            var image = Assert.Single(gallery.Images);
            Assert.Equal(galleryId, image.Id);
            Assert.Equal(legacyTime, image.CreatedAt);
            Assert.Equal("/uploads/" + galleryKey, image.Url);
            Assert.Equal(oldImage, await File.ReadAllBytesAsync(Path.Combine(fixture.StoragePath, galleryKey)));
            Assert.Equal(19, await db.Set<ProductImage>().CountAsync());
            Assert.Equal(19, fixture.CaptureFiles().Length);
            await AssertExpandedDatasetAsync(db);
        }
    }

    [SeedPostgresFact]
    public async Task PartialSeed_ShouldAddMissingChild_AndRollbackWhenNaturalKeyConflicts()
    {
        await using var fixture = await SeedPostgresSchema.CreateAsync();
        Guid missingCoverId;
        string removedObjectKey;
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            await SeedAsync(fixture, db);
            var firstDemoCustomer = await db.Customers.SingleAsync(x => x.Phone == DemoSeedManifest.Customers[2].Phone);
            var loyalty = await db.CustomerLoyaltyPoints.SingleAsync(x => x.CustomerId == firstDemoCustomer.Id && x.Reason == "Initial Sprint 2 demo loyalty balance");
            db.CustomerLoyaltyPoints.Remove(loyalty);
            var conflictingOrder = await db.Orders.SingleAsync(x => x.OrderCode == "DEMO-202610-0001");
            conflictingOrder.TotalAmount = 1m;
            var product = await db.Products.Include(x => x.Images).SingleAsync(x => x.Sku == "DEMO-TAB-IPAD-AIR");
            var image = Assert.Single(product.Images);
            missingCoverId = image.Id;
            removedObjectKey = image.ObjectKey;
            db.Set<ProductImage>().Remove(image);
            product.ImageUrl = null;
            await db.SaveChangesAsync();
            Assert.True(await fixture.Storage.TryDeleteAsync(removedObjectKey));
        }

        var before = await CaptureCatalogAsync(fixture);
        var counts = await CaptureCountsAsync(fixture);
        var files = fixture.CaptureFiles();
        await using (var db = fixture.CreateContext())
        {
            var seeder = CreateSeeder(fixture, db);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
            Assert.Contains("DEMO-202610-0001", exception.Message, StringComparison.Ordinal);
        }

        Assert.Equal(before, await CaptureCatalogAsync(fixture));
        Assert.Equal(counts, await CaptureCountsAsync(fixture));
        await using (var db = fixture.CreateContext())
        {
            Assert.Equal(7, await db.CustomerLoyaltyPoints.CountAsync(x => x.Reason == "Initial Sprint 2 demo loyalty balance"));
            var order = await db.Orders.SingleAsync(x => x.OrderCode == "DEMO-202610-0001");
            Assert.Equal(1m, order.TotalAmount);
            order.TotalAmount = 16990000m;
            await db.SaveChangesAsync();
            await SeedAsync(fixture, db);
            Assert.True(await db.CustomerLoyaltyPoints.AnyAsync(x => x.Reason == "Initial Sprint 2 demo loyalty balance"));
            await AssertExpandedDatasetAsync(db);
            var image = await db.Set<ProductImage>().SingleAsync(x => x.Id == missingCoverId);
            Assert.NotEqual(removedObjectKey, image.ObjectKey);
            Assert.Equal(image.Url, await db.Products.Where(x => x.Id == image.ProductId).Select(x => x.ImageUrl).SingleAsync());
        }
        var restored = fixture.CaptureFiles();
        Assert.Equal(files.Length + 1, restored.Length);
        Assert.All(files, file => Assert.Contains(file, restored));
    }

    private static Task SeedAsync(SeedPostgresSchema fixture, AppDbContext db) =>
        CreateSeeder(fixture, db).SeedAsync();

    private static IDataSeeder CreateSeeder(SeedPostgresSchema fixture, AppDbContext db) =>
        new DataSeeder(new DemoSeedRepository(db), new UnitOfWork(db), fixture.Storage);

    private static async Task<LoginResponseDto> LoginAsync(HttpClient client, string username, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username, Password = password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        return login;
    }

    private static async Task AssertCoversAsync(SeedPostgresSchema fixture, HttpClient client)
    {
        await using var db = fixture.CreateContext();
        var products = await db.Products.AsNoTracking().Include(p => p.Images).ToListAsync();
        Assert.Equal(20, products.Count);
        foreach (var product in products)
        {
            var seed = DemoSeedManifest.Products.Single(x => x.Sku == product.Sku);
            Assert.Equal(seed.Cost, product.CostPrice);
            var stored = Assert.Single(product.Images);
            Assert.Equal(DemoSeedManifest.AnchorUtc, stored.CreatedAt);
            var detail = await client.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}");
            Assert.NotNull(detail);
            var image = Assert.Single(detail.Images);
            Assert.Equal(stored.Id, image.Id);
            Assert.Null(image.VariantId);
            Assert.Equal(0, image.SortOrder);
            Assert.Equal(image.Url, detail.ImageUrl);
            Assert.Equal(stored.Url, image.Url);
            Assert.Equal("/uploads/" + stored.ObjectKey, image.Url);
            using var response = await client.GetAsync(image.Url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("image/png", image.ContentType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(image.SizeBytes, bytes.Length);
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(fixture.StoragePath, stored.ObjectKey)), bytes);
        }

        foreach (var (sku, parent) in new[]
        {
            ("IP15PM-256-NT", "IP15PM-256"),
            ("S24U-256-GR", "S24U-256"),
            ("MBP14-M3-512-SG", "MBP14-M3-512")
        })
        {
            Assert.Equal(parent, await db.ProductVariants.Where(v => v.Sku == sku).Select(v => v.Product.Sku).SingleAsync());
        }
    }

    private static async Task<string> CaptureCatalogAsync(SeedPostgresSchema fixture)
    {
        await using var db = fixture.CreateContext();
        var catalog = new
        {
            Categories = await db.Categories.OrderBy(x => x.Id).Select(x => new { x.Id, x.CreatedAt, x.UpdatedAt }).ToListAsync(),
            Products = await db.Products.OrderBy(x => x.Id).Select(x => new { x.Id, x.Sku, x.CreatedAt, x.UpdatedAt, x.CostPrice, x.ImageUrl }).ToListAsync(),
            Variants = await db.ProductVariants.OrderBy(x => x.Id).Select(x => new { x.Id, x.ProductId, x.CreatedAt, x.UpdatedAt }).ToListAsync(),
            Users = await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.CreatedAt, x.UpdatedAt, x.Username, x.PasswordHash }).ToListAsync(),
            Images = await db.Set<ProductImage>().OrderBy(x => x.Id).Select(x => new
            {
                x.Id, x.ProductId, x.VariantId, x.CreatedAt, x.UpdatedAt, x.Url, x.ObjectKey, x.SortOrder, x.ContentType, x.SizeBytes
            }).ToListAsync(),
            Orders = await db.Orders.OrderBy(x => x.Id).Select(x => new { x.Id, x.CreatedAt, x.PaidAt, x.CompletedAt, x.TotalAmount }).ToListAsync(),
            Files = fixture.CaptureFiles()
        };
        // Compare credential hashes without including them in assertion output.
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(catalog)));
    }

    private static async Task AssertExpandedDatasetAsync(AppDbContext db)
    {
        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.True(await db.Categories.CountAsync() >= 5);
        Assert.True(await db.Products.CountAsync() >= 20);
        Assert.True(await db.Customers.CountAsync() >= 10);
        // The schema checks (expiry after creation, uses within the limit) accept every demo voucher state.
        Assert.Equal(DemoSeedManifest.Vouchers.Count, await db.Vouchers.CountAsync(x => x.Code.StartsWith("DEMO-")));
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
        Assert.Equal(10, await db.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.PurchaseReceipt && x.PurchaseOrderItemId != null && x.InventoryStock.Product.IsSerialTracked));
        Assert.Equal(12, await db.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.Sale && x.OrderItemId != null && x.InventoryStock.Product.IsSerialTracked));
        Assert.Equal(6, await db.VietQrTransactions.CountAsync(x => x.TransactionCode.StartsWith("DQR-DEMO-")));
        Assert.Equal(9, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000") && x.Status == SerialImeiStatus.Sold && x.WarrantyStartAt != null && x.WarrantyEndAt != null));
        Assert.Equal(2, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000") && x.Status == SerialImeiStatus.Reserved));
        Assert.Equal(35, await db.SerialImeis.CountAsync(x => x.Imei != null && x.Imei.StartsWith("490000000") && x.Status == SerialImeiStatus.InStock));
        await AssertLifecycleLinksAsync(db);
        await AssertAfterSaleDemoAsync(db);
    }

    private static async Task AssertAfterSaleDemoAsync(AppDbContext db)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Returns)
            .SingleAsync(o => o.OrderCode == "M4-202610-0001");
        var item = Assert.Single(order.Items);
        Assert.Equal(21, await db.Orders.CountAsync());
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal((2, 1_980_000m, 600_000m), (item.Quantity, order.TotalAmount, item.UnitCost!.Value));
        var refund = await db.Set<ReturnCase>().Include(x => x.Lines).SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal("DEMO-M4-20261026-01", refund.BankReference);
        Assert.Equal(order.TotalAmount, order.Returns.Sum(x => x.RefundAmount));
        Assert.Equal(2, Assert.Single(refund.Lines).Quantity);
        var restocked = Assert.Single(order.Returns, x => x.IsRestocked);
        Assert.Equal(1, restocked.Quantity);
        Assert.Equal(1, Assert.Single(order.Returns, x => x.Disposition == "Pending").Quantity);
        var movement = await db.InventoryMovements.SingleAsync(m => m.OrderReturnId == restocked.Id);
        Assert.Equal(restocked.RefundedAt.AddDays(1), movement.CreatedAt);
        Assert.Equal((InventoryMovementType.CustomerReturn, 3, 1), (movement.MovementType, movement.QuantityAfter, movement.QuantityChange));
        var warranty = await db.Set<WarrantyCase>().AsNoTracking().Include(x => x.Events).SingleAsync(x => x.CaseCode == "BH-202610-0001");
        Assert.Equal(WarrantyCaseStatus.ReturnedToCustomer, warranty.Status);
        Assert.Equal("BH-202610-0001", warranty.CaseCode);
        Assert.False(string.IsNullOrWhiteSpace(warranty.ContactName));
        Assert.False(string.IsNullOrWhiteSpace(warranty.ContactPhone));
        Assert.Collection(warranty.Events.OrderBy(x => x.CreatedAt),
            opened => Assert.Equal((null, WarrantyCaseStatus.Received, warranty.OpenedById, warranty.OpenedAt),
                (opened.FromStatus, opened.ToStatus, opened.ActorId, opened.CreatedAt)),
            closed => Assert.Equal(((WarrantyCaseStatus?)WarrantyCaseStatus.Received, WarrantyCaseStatus.ReturnedToCustomer, warranty.ClosedById!.Value, warranty.ClosedAt!.Value),
                (closed.FromStatus, closed.ToStatus, closed.ActorId, closed.CreatedAt)));
        Assert.Equal(2, await db.Set<InventoryOperation>().CountAsync(x => x.Kind == "Warranty" &&
            (x.Id == warranty.ReceiveOperationId || x.Id == warranty.ResolutionOperationId)));
        Assert.Equal(9, await db.Set<InventoryOperation>().CountAsync());
        var demoCases = await db.Set<WarrantyCase>().AsNoTracking().Include(x => x.Events)
            .Where(x => x.CaseCode != "BH-202610-0001").OrderBy(x => x.CaseCode).ToListAsync();
        Assert.Equal(new[] { WarrantyCaseStatus.Received, WarrantyCaseStatus.Repairing,
            WarrantyCaseStatus.PendingReplacement, WarrantyCaseStatus.Replaced }, demoCases.Select(x => x.Status));
        Assert.Equal(new[] { 1, 2, 2, 3 }, demoCases.Select(x => x.Events.Count));
        var replaced = demoCases[^1];
        Assert.NotNull(replaced.MovementId);
        Assert.Equal(replaced.OrderItemId, await db.SerialImeis.Where(d => d.Id == replaced.ReplacementDeviceId)
            .Select(d => d.SaleOrderItemId).SingleAsync());
        Assert.Single(await db.Set<InventoryOperation>().Where(x => x.Id == restocked.InspectionId && x.MovementId == movement.Id).ToListAsync());
    }

    private static async Task AssertLifecycleLinksAsync(AppDbContext db)
    {
        var sessionId = DemoSeedManifest.Id("pos-session:demo-october-2026");
        var session = await db.PosSessions.AsNoTracking().SingleAsync(x => x.Id == sessionId);
        var actors = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Username, x => x.Id);
        var orders = await db.Orders.AsNoTracking().Where(x => x.OrderCode.StartsWith("DEMO-202610-"))
            .Include(x => x.Items).ThenInclude(x => x.SerialImei).ToListAsync();
        var vietQr = await db.VietQrTransactions.AsNoTracking().Where(x => x.PosSessionId == sessionId).ToListAsync();
        var stocks = await db.InventoryStocks.AsNoTracking().ToListAsync();
        var ledger = await db.InventoryMovements.AsNoTracking().ToListAsync();
        var receipts = await db.PurchaseOrderItems.AsNoTracking().Include(x => x.PurchaseOrder)
            .Where(x => x.PurchaseOrder.PoNumber == "PO-DEMO-202610-001").ToListAsync();

        Assert.Equal(PosSessionStatus.Closed, session.Status);
        Assert.Equal(actors["cashier1"], session.CashierUserId);
        foreach (var receipt in receipts)
        {
            Assert.Equal(PurchaseOrderStatus.Received, receipt.PurchaseOrder.Status);
            Assert.Equal(actors["warehouse1"], receipt.PurchaseOrder.ReceivedByUserId);
            var stock = Assert.Single(stocks, x => x.ProductId == receipt.ProductId && x.VariantId == receipt.VariantId);
            var movement = Assert.Single(ledger, x => x.PurchaseOrderItemId == receipt.Id);
            Assert.Equal(InventoryMovementType.PurchaseReceipt, movement.MovementType);
            Assert.Equal(stock.Id, movement.InventoryStockId);
            Assert.Equal(actors["warehouse1"], movement.PerformedByUserId);
            Assert.Equal(receipt.ReceivedQuantity, movement.QuantityChange);
            Assert.Equal(receipt.ReceivedQuantity, movement.QuantityAfter);

            var balance = receipt.ReceivedQuantity;
            foreach (var sale in ledger.Where(x => x.InventoryStockId == stock.Id && (x.MovementType is InventoryMovementType.Sale or InventoryMovementType.WarrantyReplacement))
                         .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            {
                Assert.Equal(--balance, sale.QuantityAfter);
            }
            Assert.Equal(stock.Quantity, balance);
        }

        foreach (var order in orders)
        {
            Assert.Equal(session.Id, order.PosSessionId);
            Assert.Equal(session.CashierUserId, order.CashierUserId);
            Assert.InRange(order.CreatedAt, session.OpenedAt, session.ClosedAt!.Value);
            var item = Assert.Single(order.Items);
            Assert.Equal(item.TotalPrice, order.TotalAmount);
            var payment = vietQr.Where(x => x.OrderId == order.Id);
            var sales = ledger.Where(x => x.OrderItemId == item.Id && x.MovementType == InventoryMovementType.Sale);
            if (order.Status == OrderStatus.Completed && order.PaymentMethod == PaymentMethod.VietQr)
            {
                var qr = Assert.Single(payment);
                Assert.Equal("DQR-" + order.OrderCode, qr.TransactionCode);
                Assert.Equal(VietQrStatus.Confirmed, qr.Status);
                Assert.Equal(order.PaidAt, qr.ConfirmedAt);
                Assert.Equal(order.TotalAmount, qr.Amount);
                Assert.Equal(actors["admin"], qr.ConfirmedByUserId);
            }
            else
            {
                Assert.Empty(payment);
            }

            if (order.Status == OrderStatus.Completed)
            {
                Assert.Equal(order.PaidAt, order.CompletedAt);
                Assert.NotNull(order.CompletedAt);
                Assert.NotNull(item.SerialImei);
                Assert.Equal(item.ProductId, item.SerialImei.ProductId);
                Assert.Equal(item.VariantId, item.SerialImei.VariantId);
                var warranty = await db.Set<WarrantyCase>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.OrderItemId == item.Id && x.DeviceId == item.SerialImeiId && x.Status != WarrantyCaseStatus.ReturnedToCustomer);
                var expectedStatus = warranty?.Status switch
                {
                    null => SerialImeiStatus.Sold,
                    WarrantyCaseStatus.Replaced => SerialImeiStatus.Defective,
                    _ => SerialImeiStatus.UnderRepair
                };
                Assert.Equal(expectedStatus, item.SerialImei.Status);
                Assert.Equal(order.CompletedAt, item.SerialImei.WarrantyStartAt);
                Assert.Equal(order.CompletedAt.Value.AddMonths(12), item.SerialImei.WarrantyEndAt);
                var sale = Assert.Single(sales);
                var stock = Assert.Single(stocks, x => x.ProductId == item.ProductId && x.VariantId == item.VariantId);
                Assert.Equal(stock.Id, sale.InventoryStockId);
                Assert.Equal(InventoryMovementType.Sale, sale.MovementType);
                Assert.Equal(-item.Quantity, sale.QuantityChange);
                Assert.Equal(order.CashierUserId, sale.PerformedByUserId);
                Assert.Equal(order.CompletedAt, sale.CreatedAt);
            }
            else
            {
                Assert.Empty(sales);
                Assert.Null(order.CompletedAt);
                if (order.Status == OrderStatus.Processing)
                {
                    Assert.NotNull(order.PaidAt);
                    Assert.NotNull(item.SerialImei);
                    Assert.Equal(item.ProductId, item.SerialImei.ProductId);
                    Assert.Equal(item.VariantId, item.SerialImei.VariantId);
                    Assert.Equal(SerialImeiStatus.Reserved, item.SerialImei.Status);
                    Assert.Null(item.SerialImei.WarrantyStartAt);
                    Assert.Null(item.SerialImei.WarrantyEndAt);
                }
                else
                {
                    Assert.Contains(order.Status, new[] { OrderStatus.Pending, OrderStatus.Cancelled });
                    Assert.Null(order.PaidAt);
                    Assert.Null(item.SerialImeiId);
                }
            }
        }

        var paid = orders.Where(x => x.PaidAt.HasValue).ToArray();
        Assert.Equal(paid.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.TotalAmount), session.CashSalesTotal);
        Assert.Equal(paid.Where(x => x.PaymentMethod == PaymentMethod.VietQr).Sum(x => x.TotalAmount), session.VietQrSalesTotal);
        Assert.Equal(vietQr.Sum(x => x.Amount), session.VietQrSalesTotal);
        Assert.Equal(0m, session.CardSalesTotal);
        Assert.Equal(session.OpeningBalance + session.CashSalesTotal, session.ClosingBalance);
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


    private async Task AssertAnalyticsReferenceAsync(HttpClient client)
    {
        var anchor = new DateTime(2026, 10, 18, 5, 0, 0, DateTimeKind.Utc);
        var end = new DateOnly(2026, 10, 18);
        var week = new decimal[] { 14_480_000, 35_980_000, 0, 14_980_000, 22_980_000, 19_680_000, 27_980_000 };
        var recent = new (int Number, decimal Total, string Status, DateTime CreatedAt)[]
        {
            (11, 16_990_000, "Completed", anchor.AddMinutes(-21)),
            (6, 10_990_000, "Completed", anchor.AddMinutes(-21)),
            (1, 16_990_000, "Completed", anchor.AddDays(-1).AddMinutes(-21)),
            (7, 2_690_000, "Completed", anchor.AddDays(-1).AddMinutes(-21)),
            (13, 16_990_000, "Processing", anchor.AddDays(-1).AddHours(-13))
        };
        var lowStock = new[]
        {
            ("DEMO-CHARGER-ANKER-735", "Anker 735 Charger 65W"),
            ("DEMO-HEADSET-JABRA-EVOLVE", "Jabra Evolve2 65 Flex"),
            ("DEMO-WEBCAM-LOGI-BRIO", "Logitech Brio 4K Webcam"),
            ("DEMO-MOUSE-LOGI-MX3", "Logitech MX Master 3S"),
            ("DEMO-CABLE-USB-C", "USB-C 100W Braided Cable")
        }.Select(x => new AdminDashboardLowStockDto(DemoSeedManifest.Id($"stock:{x.Item1}"),
            DemoSeedManifest.Id($"product:{x.Item1}"), x.Item2, null, null, 0, 0, 0, 3)).ToArray();

        var periods = new (string Period, string Query, DateOnly Start, decimal Revenue, int Count, decimal[] Daily)[]
        {
            ("today", "today", end, 27_980_000, 2, [27_980_000]),
            ("last7Days", "last7Days", end.AddDays(-6), 136_080_000, 20, week),
            ("thisMonth", "thisMonth", new(2026, 10, 1), 136_080_000, 20, [..new decimal[11], ..week]),
            ("custom", "custom&from=2026-10-12&to=2026-10-18", end.AddDays(-6), 136_080_000, 20, week)
        };

        foreach (var (period, query, start, revenue, count, daily) in periods)
        {
            using var response = await client.GetAsync($"/api/analytics/dashboard?period={query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var actual = await response.Content.ReadFromJsonAsync<AdminDashboardDto>();
            Assert.NotNull(actual);
            Assert.Equal(period, actual.Period);
            Assert.Equal(anchor, actual.AsOf);
            Assert.Equal(start, actual.StartDate);
            Assert.Equal(end, actual.EndDate);
            Assert.Equal("UTC+07:00", actual.TimeZone);
            Assert.Equal(new AdminDashboardSummaryDto(revenue, count, 20, 8, null, null), actual.Summary);
            Assert.Equal(daily.Select((amount, index) => new AdminDashboardDailyRevenueDto(start.AddDays(index), amount)),
                actual.DailyRevenue);
            Assert.Equal(actual.Summary.Revenue, actual.DailyRevenue.Sum(x => x.Revenue));
            Assert.Equal(lowStock, actual.LowStockItems);
            Assert.Equal(recent.Take(Math.Min(count, 5)).Select(x => ($"DEMO-202610-{x.Number:0000}", x.Total, x.Status, x.CreatedAt)),
                actual.RecentOrders.Select(x => (x.OrderCode, x.TotalAmount, x.Status, x.CreatedAt)));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Status = (int)response.StatusCode,
                actual.Period, actual.StartDate, actual.EndDate, actual.AsOf, actual.TimeZone, actual.Summary,
                actual.DailyRevenue,
                RecentOrders = actual.RecentOrders.Select(x => x.OrderCode),
                LowStock = actual.LowStockItems.Select(x => new { x.ProductName, x.Quantity, x.ReservedQuantity, x.AvailableQuantity, x.MinStockAlert })
            }));
        }
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
    public string ConnectionString { get; }
    public string StoragePath { get; }
    public IObjectStorage Storage { get; }

    private SeedPostgresSchema(string adminConnectionString, string schema, string schemaConnectionString)
    {
        _adminConnectionString = adminConnectionString;
        _schema = schema;
        ConnectionString = schemaConnectionString;
        StoragePath = Path.Combine(Path.GetTempPath(), "techstore-" + schema);
        Storage = new LocalObjectStorage(new() { Local = new() { RootPath = StoragePath } }, StoragePath,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalObjectStorage>.Instance);
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

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public string[] CaptureFiles() => Directory.Exists(StoragePath)
        ? Directory.GetFiles(StoragePath, "*.png", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path =>
            Path.GetRelativePath(StoragePath, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray()
        : [];

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (Directory.Exists(StoragePath)) Directory.Delete(StoragePath, recursive: true);
        }
    }
}

internal sealed class SeedPostgresApi(SeedPostgresSchema fixture) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("ConnectionStrings:DefaultConnection", fixture.ConnectionString);
        builder.UseSetting("Jwt:SecretKey", "Seed_HTTP_fixture_key_for_tests_only_at_least_64_characters_2026");
        builder.UseSetting("Jwt:Issuer", "TechStore.Seed.HttpTests");
        builder.UseSetting("Jwt:Audience", "TechStore.Seed.HttpTests.Client");
        builder.UseSetting("ObjectStorage:Provider", "Local");
        builder.UseSetting("ObjectStorage:Local:RootPath", fixture.StoragePath);
        builder.UseSetting("ObjectStorage:Local:RequestPath", "/uploads");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAnalyticsRepository>();
            services.AddScoped<AnalyticsRepository>();
            services.AddScoped<IAnalyticsRepository, AnchoredAnalyticsRepository>();
        });
    }

    private sealed class AnchoredAnalyticsRepository(AnalyticsRepository inner) : IAnalyticsRepository
    {
        public Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(DemoSeedManifest.AnchorUtc);

        public Task<DashboardAnalyticsReadModel> GetDashboardAsync(
            DashboardAnalyticsCriteria criteria, CancellationToken cancellationToken = default) =>
            inner.GetDashboardAsync(criteria, cancellationToken);

        public Task<FinancialReportReadModel> GetFinancialReportAsync(
            FinancialReportCriteria criteria, CancellationToken cancellationToken = default) =>
            inner.GetFinancialReportAsync(criteria, cancellationToken);
    }
}
