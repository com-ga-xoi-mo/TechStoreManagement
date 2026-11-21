using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests.Inventory;

public class PurchaseReceiptPostgresTests
{
    private sealed class LockBarrier
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;
        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class FirstLockInterceptor(LockBarrier barrier) : DbCommandInterceptor
    {
        private bool _arrived;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_arrived && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                _arrived = true;
                await barrier.ArriveAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailingSaveUnitOfWork(UnitOfWork inner) : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken ct = default) => inner.BeginTransactionAsync(ct);
        public Task BeginTransactionAsync(System.Data.IsolationLevel level, CancellationToken ct = default) => inner.BeginTransactionAsync(level, ct);
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await inner.SaveChangesAsync(ct);
            throw new InvalidOperationException("Injected save failure");
        }
        public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
    }

    private static (PurchaseReceiptService Service, AppDbContext Context) CreateService(
        PostgresDatabase database,
        IInterceptor? interceptor = null,
        bool failOnSave = false)
    {
        var context = interceptor != null ? database.CreateContext(interceptor) : database.CreateContext();
        IUnitOfWork uow = new UnitOfWork(context);
        if (failOnSave)
        {
            uow = new FailingSaveUnitOfWork((UnitOfWork)uow);
        }

        var poRepo = new PurchaseOrderRepository(context);
        var stockRepo = new InventoryStockRepository(context);
        var serialRepo = new SerialImeiRepository(context);
        var prodRepo = new ProductRepository(context);
        var suppRepo = new SupplierRepository(context);
        var service = new PurchaseReceiptService(poRepo, stockRepo, serialRepo, prodRepo, suppRepo, uow);
        return (service, context);
    }

    private static async Task<(User User, Supplier Supplier, Category Category)> SeedCommonAsync(AppDbContext context)
    {
        var user = new User
        {
            Username = "staff_" + Guid.NewGuid().ToString("N")[..8],
            Email = "staff_" + Guid.NewGuid().ToString("N")[..8] + "@example.com",
            PasswordHash = "hash",
            FullName = "Staff Member",
            IsActive = true
        };
        var supplier = new Supplier
        {
            Name = "Supplier_" + Guid.NewGuid().ToString("N")[..8],
            Phone = "0123456789",
            IsActive = true
        };
        var category = new Category
        {
            Name = "Cat_" + Guid.NewGuid().ToString("N")[..8],
            Slug = "cat-" + Guid.NewGuid().ToString("N")[..8],
            IsActive = true
        };

        context.Users.Add(user);
        context.Suppliers.Add(supplier);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return (user, supplier, category);
    }

    [PostgresFact]
    public async Task PlannedReceipt_MixedDeliveryAndCancellation_OnlyReceivedUnitsEnterStockAndCost()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seed = database.CreateContext();
        var (actor, supplier, category) = await SeedCommonAsync(seed);
        var cable = new Product { CategoryId = category.Id, Name = "Cable", Sku = "C", IsSerialTracked = false };
        var phone = new Product { CategoryId = category.Id, Name = "Phone", Sku = "P", IsSerialTracked = true };
        seed.Products.AddRange(cable, phone);
        await seed.SaveChangesAsync();
        var (service, context) = CreateService(database);
        await using var scope = context;
        var planned = await service.CreateAsync(new()
        {
            ReceiveLater = true, ClientRequestId = Guid.NewGuid(), SupplierId = supplier.Id,
            Lines = [new() { ProductId = cable.Id, Quantity = 5, UnitCost = 100 },
                     new() { ProductId = phone.Id, Quantity = 3, UnitCost = 530, WarrantyMonths = 18 }]
        }, actor.Id);
        var po = planned.Data!;
        po.Status.Should().Be(PurchaseOrderStatus.Ordered);
        po.TotalCost.Should().Be(0);
        po.ReceivedAt.Should().BeNull();
        (await seed.InventoryStocks.CountAsync()).Should().Be(0);
        var cableLine = po.Lines.Single(l => l.ProductId == cable.Id);
        var phoneLine = po.Lines.Single(l => l.ProductId == phone.Id);
        var delivery = new ReceivePurchaseDeliveryRequest
        {
            ClientRequestId = Guid.NewGuid(), SupplierDocumentNumber = "DN-01",
            Lines = [new() { LineId = cableLine.Id, Quantity = 2 },
                     new() { LineId = phoneLine.Id, Quantity = 1, Devices = [new() { SerialNumber = "TEST-SN-A" }] }]
        };
        async Task<PurchaseReceiptResult> Receive(ReceivePurchaseDeliveryRequest request, bool fail = false)
        {
            var (svc, db) = CreateService(database, failOnSave: fail);
            await using var disposable = db;
            return await svc.ReceiveAsync(po.Id, request, actor.Id);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Receive(delivery, fail: true));
        (await seed.InventoryMovements.CountAsync()).Should().Be(0);
        (await seed.SerialImeis.CountAsync()).Should().Be(0);
        var part = (await Receive(delivery)).Data!;
        part.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        part.TotalCost.Should().Be(730);
        (await Receive(delivery)).IsReplay.Should().BeTrue();

        var stale = new ReceivePurchaseDeliveryRequest { ClientRequestId = Guid.NewGuid(), Lines = delivery.Lines };
        (await Receive(stale)).IsConflict.Should().BeTrue();
        var next = new ReceivePurchaseDeliveryRequest { ClientRequestId = Guid.NewGuid(), Lines =
            [new() { LineId = cableLine.Id, ExpectedReceivedQuantity = 2, Quantity = 4 }] };
        (await Receive(next)).IsConflict.Should().BeTrue();
        next.Lines[0].Quantity = 1;
        (await Receive(next)).Data!.TotalCost.Should().Be(830);
        var (cancelService, cancelDb) = CreateService(database);
        await using var cancelScope = cancelDb;
        var cancelled = (await cancelService.CancelRemainderAsync(po.Id, actor.Id)).Data!;
        cancelled.Status.Should().Be(PurchaseOrderStatus.Cancelled);
        cancelled.CancelledByUserId.Should().Be(actor.Id);
        cancelled.CancelledAt.Should().NotBeNull();
        cancelled.TotalCost.Should().Be(830);
        (await cancelService.CancelRemainderAsync(po.Id, actor.Id)).IsReplay.Should().BeTrue();
        (await Receive(delivery)).IsReplay.Should().BeTrue();
        next.ClientRequestId = Guid.NewGuid();
        next.Lines[0].ExpectedReceivedQuantity = 3;
        (await Receive(next)).IsConflict.Should().BeTrue();
        (await seed.InventoryMovements.SumAsync(m => m.QuantityChange)).Should().Be(4);
        (await seed.InventoryStocks.SumAsync(s => s.Quantity)).Should().Be(4);
        (await seed.InventoryMovements.CountAsync(m => m.ReceiptRequestId == delivery.ClientRequestId && m.Reason!.Contains("DN-01") && m.PerformedByUserId == actor.Id)).Should().Be(2);
        var device = await seed.SerialImeis.SingleAsync();
        device.WarrantyMonths.Should().Be(18);
        device.UnitCost.Should().Be(530);
        (await new InventoryStockRepository(seed).GetAverageReceivedUnitCostAsync(cable.Id, null)).Should().Be(100);
    }

    [PostgresFact]
    public async Task PlannedReceipt_FinalSerialDelivery_CompletesAndClosedReceiptCanOnlyReplay()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seed = database.CreateContext();
        var (actor, supplier, category) = await SeedCommonAsync(seed);
        var phone = new Product { CategoryId = category.Id, Name = "Phone", Sku = "P", IsSerialTracked = true };
        seed.Products.Add(phone);
        await seed.SaveChangesAsync();
        var (service, context) = CreateService(database);
        await using var scope = context;
        var request = new CreatePurchaseReceiptRequest { ReceiveLater = true, ClientRequestId = Guid.NewGuid(), SupplierId = supplier.Id,
            Lines = [new() { ProductId = phone.Id, Quantity = 2, UnitCost = 95.25m, WarrantyMonths = 6 }] };
        var plan = (await service.CreateAsync(request, actor.Id)).Data!;
        var line = plan.Lines.Single();
        var first = new ReceivePurchaseDeliveryRequest { ClientRequestId = Guid.NewGuid(),
            Lines = [new() { LineId = line.Id, Quantity = 1, Devices = [new() { SerialNumber = "PHONE-A" }] }] };
        var part = (await service.ReceiveAsync(plan.Id, first, actor.Id)).Data!;
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, part.Status);
        var last = new ReceivePurchaseDeliveryRequest { ClientRequestId = Guid.NewGuid(),
            Lines = [new() { LineId = line.Id, ExpectedReceivedQuantity = 1, Quantity = 1, Devices = [new() { SerialNumber = "PHONE-A" }] }] };
        Assert.True((await service.ReceiveAsync(plan.Id, last, actor.Id)).IsConflict);
        last.Lines[0].Devices[0].SerialNumber = "PHONE-B";
        var full = (await service.ReceiveAsync(plan.Id, last, actor.Id)).Data!;
        Assert.Equal(PurchaseOrderStatus.Received, full.Status);
        Assert.Equal(190.50m, full.TotalCost);
        Assert.Equal(2, full.Lines.Single().Devices.Count);
        Assert.True((await service.ReceiveAsync(plan.Id, first, actor.Id)).IsReplay);
        Assert.True((await service.CancelRemainderAsync(plan.Id, actor.Id)).IsConflict);

        request.ClientRequestId = Guid.NewGuid();
        var neverReceived = (await service.CreateAsync(request, actor.Id)).Data!;
        var closed = (await service.CancelRemainderAsync(neverReceived.Id, actor.Id)).Data!;
        Assert.Equal(PurchaseOrderStatus.Cancelled, closed.Status);
        Assert.Null(closed.ReceivedAt);
        Assert.Equal(0, closed.TotalCost);
        Assert.Equal(2, await seed.InventoryMovements.CountAsync());
    }

    [PostgresFact]
    public async Task PlannedReceipt_ConcurrentDeliveryReplayAndCancellation_AreSerialized()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seed = database.CreateContext();
        var (actor, supplier, category) = await SeedCommonAsync(seed);
        var cable = new Product { CategoryId = category.Id, Name = "Cable", Sku = "C", IsSerialTracked = false };
        seed.Products.Add(cable);
        await seed.SaveChangesAsync();
        foreach (var competingAction in new[] { "replay", "receive", "cancel" })
        {
            var (planner, plannerDb) = CreateService(database);
            await using var planScope = plannerDb;
            var plan = (await planner.CreateAsync(new() { ReceiveLater = true, ClientRequestId = Guid.NewGuid(), SupplierId = supplier.Id,
                Lines = [new() { ProductId = cable.Id, Quantity = 2, UnitCost = 30 }] }, actor.Id)).Data!;
            var line = plan.Lines.Single();
            var barrier = new LockBarrier();
            var key = Guid.NewGuid();
            async Task<PurchaseReceiptResult> Run(string action)
            {
                var (svc, db) = CreateService(database, new FirstLockInterceptor(barrier));
                await using var requestScope = db;
                return action == "cancel" ? await svc.CancelRemainderAsync(plan.Id, actor.Id) :
                    await svc.ReceiveAsync(plan.Id, new() { ClientRequestId = action == "receive" ? Guid.NewGuid() : key,
                        Lines = [new() { LineId = line.Id, Quantity = 2 }] }, actor.Id);
            }
            var outcomes = await Task.WhenAll(Run("first"), Run(competingAction));
            if (competingAction == "replay")
            {
                outcomes.Should().OnlyContain(o => o.IsSuccess);
                outcomes.Count(o => o.IsReplay).Should().Be(1);
            }
            else outcomes.Count(o => o.IsConflict).Should().Be(1);
            var detail = (await planner.GetDetailAsync(plan.Id)).Data!;
            var received = detail.Status == PurchaseOrderStatus.Received;
            detail.Lines.Single().Quantity.Should().Be(received ? 2 : 0);
            detail.TotalCost.Should().Be(received ? 60 : 0);
            (await seed.InventoryMovements.CountAsync(m => m.PurchaseOrderItemId == line.Id)).Should().Be(received ? 1 : 0);
        }
    }

    [PostgresFact]
    public async Task CreateAsync_SerialAndNonSerialLines_UpdatesStockLedgerDevicesAndAverageCost()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, supplier, category) = await SeedCommonAsync(seedContext);

        var nonSerialProduct = new Product
        {
            CategoryId = category.Id,
            Name = "USB Cable",
            Sku = "CBL-TEST",
            Brand = "Generic",
            BasePrice = 150_000m,
            CostPrice = 100_000m,
            IsSerialTracked = false
        };
        var phoneProduct = new Product
        {
            CategoryId = category.Id,
            Name = "iPhone 16",
            Sku = "IP16-TEST",
            Brand = "Apple",
            BasePrice = 20_000_000m,
            CostPrice = 16_000_000m,
            IsSerialTracked = true
        };
        seedContext.Products.AddRange(nonSerialProduct, phoneProduct);
        await seedContext.SaveChangesAsync();

        var (service, context) = CreateService(database);
        await using (context)
        {
            // Act 1: Non-serial lot 1: 10 x 100,000
            var res1 = await service.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines = [new PurchaseReceiptLineRequest { ProductId = nonSerialProduct.Id, Quantity = 10, UnitCost = 100_000m }]
            }, user.Id);
            res1.IsSuccess.Should().BeTrue();

            // Act 2: Non-serial lot 2: 10 x 120,000
            var res2 = await service.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines = [new PurchaseReceiptLineRequest { ProductId = nonSerialProduct.Id, Quantity = 10, UnitCost = 120_000m }]
            }, user.Id);
            res2.IsSuccess.Should().BeTrue();

            // Assert: Stock balance = 20, average cost = (10*100k + 10*120k)/20 = 110,000
            var stockRepo = new InventoryStockRepository(context);
            var stock = await stockRepo.GetByProductAndVariantAsync(nonSerialProduct.Id, null);
            stock.Should().NotBeNull();
            stock!.Quantity.Should().Be(20);

            var avgCost = await stockRepo.GetAverageReceivedUnitCostAsync(nonSerialProduct.Id, null);
            avgCost.Should().Be(110_000m);

            var movements = await context.InventoryMovements
                .Where(m => m.InventoryStockId == stock.Id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
            movements.Should().HaveCount(2);
            movements[0].QuantityAfter.Should().Be(10);
            movements[1].QuantityAfter.Should().Be(20);

            // Act 3: Serial product intake: 5 devices
            var res3 = await service.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines =
                [
                    new PurchaseReceiptLineRequest
                    {
                        ProductId = phoneProduct.Id,
                        Quantity = 5,
                        UnitCost = 16_000_000m,
                        WarrantyMonths = 12,
                        Devices =
                        [
                            new PurchaseReceiptDeviceRequest { Imei = "356789012345601" },
                            new PurchaseReceiptDeviceRequest { Imei = "356789012345602" },
                            new PurchaseReceiptDeviceRequest { Imei = "356789012345603" },
                            new PurchaseReceiptDeviceRequest { Imei = "356789012345604" },
                            new PurchaseReceiptDeviceRequest { Imei = "356789012345605" }
                        ]
                    }
                ]
            }, user.Id);
            res3.IsSuccess.Should().BeTrue();

            var phoneStock = await stockRepo.GetByProductAndVariantAsync(phoneProduct.Id, null);
            phoneStock.Should().NotBeNull();
            phoneStock!.Quantity.Should().Be(5);

            var devices = await context.SerialImeis
                .Where(d => d.ProductId == phoneProduct.Id)
                .ToListAsync();
            devices.Should().HaveCount(5);
            devices.Should().AllSatisfy(d =>
            {
                d.Status.Should().Be(SerialImeiStatus.InStock);
                d.UnitCost.Should().Be(16_000_000m);
                d.WarrantyMonths.Should().Be(12);
                d.PurchaseOrderId.Should().Be(res3.Data!.Id);
            });
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task CreateAsync_SaveFails_RollsBackEverything()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, supplier, category) = await SeedCommonAsync(seedContext);

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Mouse",
            Sku = "MS-001",
            Brand = "Generic",
            BasePrice = 200_000m,
            CostPrice = 100_000m,
            IsSerialTracked = false
        };
        seedContext.Products.Add(product);
        await seedContext.SaveChangesAsync();

        // Act
        var (failingService, context) = CreateService(database, failOnSave: true);
        await using (context)
        {
            var act = () => failingService.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines = [new PurchaseReceiptLineRequest { ProductId = product.Id, Quantity = 5, UnitCost = 100_000m }]
            }, user.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();

            // Verify nothing remained in database
            (await context.PurchaseOrders.CountAsync()).Should().Be(0);
            (await context.PurchaseOrderItems.CountAsync()).Should().Be(0);
            (await context.InventoryStocks.CountAsync(s => s.ProductId == product.Id)).Should().Be(0);
            (await context.InventoryMovements.CountAsync()).Should().Be(0);
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task CreateAsync_SameClientRequestIdSequentialAndConcurrent_RecordsOnce()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, supplier, category) = await SeedCommonAsync(seedContext);

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Tablet",
            Sku = "TAB-001",
            Brand = "Generic",
            BasePrice = 5_000_000m,
            CostPrice = 3_000_000m,
            IsSerialTracked = true
        };
        seedContext.Products.Add(product);
        await seedContext.SaveChangesAsync();

        // 1. Sequential test
        var clientRequestId1 = Guid.NewGuid();
        var (seqService, seqContext) = CreateService(database);
        await using (seqContext)
        {
            var req = new CreatePurchaseReceiptRequest
            {
                ClientRequestId = clientRequestId1,
                SupplierId = supplier.Id,
                Lines =
                [
                    new PurchaseReceiptLineRequest
                    {
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitCost = 3_000_000m,
                        Devices = [new PurchaseReceiptDeviceRequest { Imei = "356789012345001" }]
                    }
                ]
            };

            // Act
            var first = await seqService.CreateAsync(req, user.Id);

            // Assert
            first.IsSuccess.Should().BeTrue();
            first.IsReplay.Should().BeFalse();

            var second = await seqService.CreateAsync(req, user.Id);
            second.IsSuccess.Should().BeTrue();
            second.IsReplay.Should().BeTrue();
            second.Data!.Id.Should().Be(first.Data!.Id);
        }

        // 2. Concurrent test with NO existing stock row for a brand new product
        var newProduct = new Product
        {
            CategoryId = category.Id,
            Name = "SmartWatch",
            Sku = "SW-001",
            Brand = "Generic",
            BasePrice = 4_000_000m,
            CostPrice = 2_000_000m,
            IsSerialTracked = true
        };
        seedContext.Products.Add(newProduct);
        await seedContext.SaveChangesAsync();

        var clientRequestId2 = Guid.NewGuid();
        var concurrentReq = new CreatePurchaseReceiptRequest
        {
            ClientRequestId = clientRequestId2,
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest
                {
                    ProductId = newProduct.Id,
                    Quantity = 1,
                    UnitCost = 2_000_000m,
                    Devices = [new PurchaseReceiptDeviceRequest { Imei = "356789012345999" }]
                }
            ]
        };

        // Both transactions pass the replay and duplicate checks before either writes,
        // so the unique-index path (DuplicateKeyException) is really exercised.
        var barrier = new LockBarrier();
        var (svc1, ctx1) = CreateService(database, new FirstLockInterceptor(barrier));
        var (svc2, ctx2) = CreateService(database, new FirstLockInterceptor(barrier));
        await using (ctx1)
        await using (ctx2)
        {
            // Act: the same request twice in parallel
            var task1 = svc1.CreateAsync(concurrentReq, user.Id);
            var task2 = svc2.CreateAsync(concurrentReq, user.Id);

            var results = await Task.WhenAll(task1, task2);
            // Assert
            results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
            results.Count(r => !r.IsReplay).Should().Be(1);
            results.Count(r => r.IsReplay).Should().Be(1);

            // Exactly one receipt recorded in DB
            await using var verifyContext = database.CreateContext();
            var orders = await verifyContext.PurchaseOrders.Where(p => p.ClientRequestId == clientRequestId2).ToListAsync();
            orders.Should().ContainSingle();

            var stock = await verifyContext.InventoryStocks.FirstOrDefaultAsync(s => s.ProductId == newProduct.Id);
            stock.Should().NotBeNull();
            stock!.Quantity.Should().Be(1);

            var devices = await verifyContext.SerialImeis.Where(d => d.ProductId == newProduct.Id).ToListAsync();
            devices.Should().ContainSingle();
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task CreateAsync_TwoConcurrentReceiptsForOneProduct_SumsQuantity()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, supplier, category) = await SeedCommonAsync(seedContext);

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Keyboard",
            Sku = "KB-PARALLEL",
            Brand = "Generic",
            BasePrice = 500_000m,
            CostPrice = 300_000m,
            IsSerialTracked = false
        };
        seedContext.Products.Add(product);
        var initialStock = new InventoryStock
        {
            ProductId = product.Id,
            Quantity = 0,
            ReservedQuantity = 0,
            MinStockAlert = 5
        };
        seedContext.InventoryStocks.Add(initialStock);
        await seedContext.SaveChangesAsync();

        var barrier = new LockBarrier();
        var (svc1, ctx1) = CreateService(database, new FirstLockInterceptor(barrier));
        var (svc2, ctx2) = CreateService(database, new FirstLockInterceptor(barrier));
        await using (ctx1)
        await using (ctx2)
        {
            // Act: both receipts start before either commits
            var task1 = svc1.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines = [new PurchaseReceiptLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 300_000m }]
            }, user.Id);

            var task2 = svc2.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines = [new PurchaseReceiptLineRequest { ProductId = product.Id, Quantity = 15, UnitCost = 300_000m }]
            }, user.Id);

            var results = await Task.WhenAll(task1, task2);

            // Assert
            results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());

            await using var verifyContext = database.CreateContext();
            var stock = await verifyContext.InventoryStocks.FirstOrDefaultAsync(s => s.ProductId == product.Id);
            stock.Should().NotBeNull();
            stock!.Quantity.Should().Be(25); // 10 + 15
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task CreateAsync_TwoConcurrentReceiptsWithSameImei_OneSucceedsOneConflicts()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, supplier, category) = await SeedCommonAsync(seedContext);

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Smartphone",
            Sku = "SP-RACE",
            Brand = "Generic",
            BasePrice = 10_000_000m,
            CostPrice = 7_000_000m,
            IsSerialTracked = true
        };
        seedContext.Products.Add(product);
        await seedContext.SaveChangesAsync();

        var sharedImei = "356789012345888";
        // Both transactions pass the replay and duplicate checks before either writes,
        // so the unique-index path (DuplicateKeyException) is really exercised.
        var barrier = new LockBarrier();
        var (svc1, ctx1) = CreateService(database, new FirstLockInterceptor(barrier));
        var (svc2, ctx2) = CreateService(database, new FirstLockInterceptor(barrier));
        await using (ctx1)
        await using (ctx2)
        {
            // Act: both receipts start before either commits
            var task1 = svc1.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines =
                [
                    new PurchaseReceiptLineRequest
                    {
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitCost = 7_000_000m,
                        Devices = [new PurchaseReceiptDeviceRequest { Imei = sharedImei }]
                    }
                ]
            }, user.Id);

            var task2 = svc2.CreateAsync(new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(),
                SupplierId = supplier.Id,
                Lines =
                [
                    new PurchaseReceiptLineRequest
                    {
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitCost = 7_000_000m,
                        Devices = [new PurchaseReceiptDeviceRequest { Imei = sharedImei }]
                    }
                ]
            }, user.Id);

            var results = await Task.WhenAll(task1, task2);

            // Assert
            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Count(r => r.IsConflict).Should().Be(1);

            await using var verifyContext = database.CreateContext();
            var devices = await verifyContext.SerialImeis.Where(d => d.Imei == sharedImei).ToListAsync();
            devices.Should().ContainSingle();
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }
}
