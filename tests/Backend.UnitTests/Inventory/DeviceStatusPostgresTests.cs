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

public class DeviceStatusPostgresTests
{
    private sealed class LockBarrier
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;
        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private sealed class SerialLockInterceptor(LockBarrier barrier) : DbCommandInterceptor
    {
        private bool _arrived;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_arrived && command.CommandText.Contains("serial_imeis", StringComparison.OrdinalIgnoreCase)
                          && command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                _arrived = true;
                await barrier.ArriveAsync(cancellationToken);
            }
            return result;
        }
    }

    private static async Task<(User User, Product Product, InventoryStock Stock, SerialImei Device)> SeedSingleDeviceAsync(
        AppDbContext context,
        SerialImeiStatus initialStatus = SerialImeiStatus.InStock)
    {
        var user = new User
        {
            Username = "staff_" + Guid.NewGuid().ToString("N")[..8],
            Email = "staff_" + Guid.NewGuid().ToString("N")[..8] + "@example.com",
            PasswordHash = "hash",
            FullName = "Staff Member",
            IsActive = true
        };
        var category = new Category
        {
            Name = "Cat_" + Guid.NewGuid().ToString("N")[..8],
            Slug = "cat-" + Guid.NewGuid().ToString("N")[..8],
            IsActive = true
        };
        context.Users.Add(user);
        context.Categories.Add(category);

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Smartphone",
            Sku = "PHONE-" + Guid.NewGuid().ToString("N")[..8],
            Brand = "Apple",
            BasePrice = 20_000_000m,
            CostPrice = 16_000_000m,
            IsSerialTracked = true
        };
        context.Products.Add(product);

        var stock = new InventoryStock
        {
            ProductId = product.Id,
            Quantity = initialStatus == SerialImeiStatus.InStock ? 1 : 0,
            ReservedQuantity = initialStatus == SerialImeiStatus.Reserved ? 1 : 0,
            MinStockAlert = 5
        };
        context.InventoryStocks.Add(stock);

        var device = new SerialImei
        {
            ProductId = product.Id,
            Imei = "35" + Random.Shared.NextInt64(10_000_000_000_00L, 99_999_999_999_99L).ToString("D13"),
            Status = initialStatus,
            UnitCost = 16_000_000m,
            WarrantyMonths = 12
        };
        context.SerialImeis.Add(device);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (user, product, stock, device);
    }

    [PostgresFact]
    public async Task ReserveAsync_DefectiveDevice_ReturnsConflict()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (_, product, _, device) = await SeedSingleDeviceAsync(seedContext, SerialImeiStatus.Defective);

        await using var actContext = database.CreateContext();
        var uow = new UnitOfWork(actContext);
        var fulfillmentService = new InventoryFulfillmentService(
            new SerialImeiRepository(actContext),
            new InventoryStockRepository(actContext), new InventoryAfterSaleRepository(actContext));

        // Act
        await uow.BeginTransactionAsync();
        var result = await fulfillmentService.ReserveAsync([
            new FulfillmentLine(null, product.Id, null, device.Id, 1)
        ]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        await uow.RollbackAsync();

        // Reconciliation check
        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task ReportDefectiveAndRestoreAsync_KeepQuantityEqualToInStockPlusReserved()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, product, stock, device) = await SeedSingleDeviceAsync(seedContext, SerialImeiStatus.InStock);

        // Act 1: Report defective
        await using (var actContext1 = database.CreateContext())
        {
            var uow1 = new UnitOfWork(actContext1);
            var service1 = new SerialImeiService(
                new SerialImeiRepository(actContext1),
                new InventoryStockRepository(actContext1),
                new ProductRepository(actContext1),
                uow1);

            var reportRes = await service1.ReportDefectiveAsync(
                device.Id,
                new ReportDefectiveDeviceRequest { Reason = "Hư màn hình", Note = "Rơi vỡ" },
                user.Id);

            reportRes.IsSuccess.Should().BeTrue();
            reportRes.Data!.Status.Should().Be(SerialImeiStatus.Defective);
        }

        // Verify after report defective
        await using (var verifyContext = database.CreateContext())
        {
            var s = await verifyContext.InventoryStocks.SingleAsync(st => st.Id == stock.Id);
            s.Quantity.Should().Be(0);
            s.ReservedQuantity.Should().Be(0);

            var d = await verifyContext.SerialImeis.SingleAsync(di => di.Id == device.Id);
            d.Status.Should().Be(SerialImeiStatus.Defective);

            var movement = await verifyContext.InventoryMovements.SingleAsync(m => m.InventoryStockId == s.Id);
            movement.MovementType.Should().Be(InventoryMovementType.Defective);
            movement.QuantityChange.Should().Be(-1);
            movement.QuantityAfter.Should().Be(0);
            movement.Reason.Should().Contain(device.Imei!);
            movement.Reason.Should().Contain("Hư màn hình");
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);

        // Act 2: Restore device to stock
        await using (var actContext2 = database.CreateContext())
        {
            var uow2 = new UnitOfWork(actContext2);
            var service2 = new SerialImeiService(
                new SerialImeiRepository(actContext2),
                new InventoryStockRepository(actContext2),
                new ProductRepository(actContext2),
                uow2);

            var restoreRes = await service2.RestoreAsync(
                device.Id,
                new RestoreDeviceRequest { Note = "Đã thay màn hình mới" },
                user.Id);

            restoreRes.IsSuccess.Should().BeTrue();
            restoreRes.Data!.Status.Should().Be(SerialImeiStatus.InStock);
        }

        // Verify after restore
        await using (var verifyContext = database.CreateContext())
        {
            var s = await verifyContext.InventoryStocks.SingleAsync(st => st.Id == stock.Id);
            s.Quantity.Should().Be(1);
            s.ReservedQuantity.Should().Be(0);

            var d = await verifyContext.SerialImeis.SingleAsync(di => di.Id == device.Id);
            d.Status.Should().Be(SerialImeiStatus.InStock);
            d.UnitCost.Should().Be(device.UnitCost);
            d.WarrantyMonths.Should().Be(device.WarrantyMonths);

            var movements = await verifyContext.InventoryMovements
                .Where(m => m.InventoryStockId == s.Id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
            movements.Should().HaveCount(2);
            movements[1].MovementType.Should().Be(InventoryMovementType.Adjustment);
            movements[1].QuantityChange.Should().Be(1);
            movements[1].QuantityAfter.Should().Be(1);
            movements[1].Reason.Should().Contain("Sửa xong");
            movements[1].Reason.Should().Contain("Đã thay màn hình mới");
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task ReportDefectiveAsync_ConcurrentWithOrderReserve_ExactlyOneSucceeds()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (user, product, stock, device) = await SeedSingleDeviceAsync(seedContext, SerialImeiStatus.InStock);

        var barrier = new LockBarrier();

        // Act: report the defect and reserve the same device in parallel
        var task1 = Task.Run(async () =>
        {
            await using var ctx1 = database.CreateContext(new SerialLockInterceptor(barrier));
            var uow1 = new UnitOfWork(ctx1);
            var service = new SerialImeiService(
                new SerialImeiRepository(ctx1),
                new InventoryStockRepository(ctx1),
                new ProductRepository(ctx1),
                uow1);

            return await service.ReportDefectiveAsync(
                device.Id,
                new ReportDefectiveDeviceRequest { Reason = "Lỗi phần cứng" },
                user.Id);
        });

        var task2 = Task.Run(async () =>
        {
            await using var ctx2 = database.CreateContext(new SerialLockInterceptor(barrier));
            var uow2 = new UnitOfWork(ctx2);
            var fulfillment = new InventoryFulfillmentService(
                new SerialImeiRepository(ctx2),
                new InventoryStockRepository(ctx2), new InventoryAfterSaleRepository(ctx2));

            await uow2.BeginTransactionAsync();
            var res = await fulfillment.ReserveAsync([
                new FulfillmentLine(null, product.Id, null, device.Id, 1)
            ]);

            if (res.IsSuccess)
            {
                await uow2.SaveChangesAsync();
                await uow2.CommitAsync();
            }
            else
            {
                await uow2.RollbackAsync();
            }

            return res;
        });

        var (reportRes, reserveRes) = (await task1, await task2);

        // Assert: exactly one must succeed
        (reportRes.IsSuccess ^ reserveRes.IsSuccess).Should().BeTrue();

        await using var verifyContext = database.CreateContext();
        var currentStock = await verifyContext.InventoryStocks.SingleAsync(s => s.Id == stock.Id);
        var currentDevice = await verifyContext.SerialImeis.SingleAsync(d => d.Id == device.Id);

        if (reportRes.IsSuccess)
        {
            currentDevice.Status.Should().Be(SerialImeiStatus.Defective);
            currentStock.Quantity.Should().Be(0);
            currentStock.ReservedQuantity.Should().Be(0);
        }
        else
        {
            currentDevice.Status.Should().Be(SerialImeiStatus.Reserved);
            currentStock.Quantity.Should().Be(1);
            currentStock.ReservedQuantity.Should().Be(1);
        }

        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
    }

    [PostgresFact]
    public async Task AssertSerialStockReconciledAsync_BalanceTamperedBySql_ThrowsWithExactDiscrepancy()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var seedContext = database.CreateContext();
        var (_, _, stock, _) = await SeedSingleDeviceAsync(seedContext, SerialImeiStatus.InStock);

        // Reconciled initially
        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);

        // Tamper directly via raw SQL
        await using (var tamperContext = database.CreateContext())
        {
            await tamperContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE inventory_stocks SET quantity = 99 WHERE id = {stock.Id}");
        }

        // Assert helper catches discrepancy
        var act = async () => await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(database);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(stock.Id.ToString());
        ex.Which.Message.Should().Contain("Stored(Quantity=99");
        ex.Which.Message.Should().Contain("Expected(Quantity=1");
    }
}
