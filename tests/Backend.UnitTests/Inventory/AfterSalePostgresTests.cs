using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests.Inventory;

public class AfterSalePostgresTests
{
    private sealed record Fixture(InventoryStock Stock, SerialImei Device, Order Order, FulfillmentLine Line);

    private static async Task<Fixture> SeedAsync(AppDbContext db, OrderStatus status)
    {
        var category = new Category { Name = "Phones", Slug = "phones" };
        var product = new Product { CategoryId = category.Id, Sku = "PH", Name = "Phone", IsSerialTracked = true };
        var cashier = new User { Username = "staff", Email = "staff@example.test", FullName = "Staff", PasswordHash = "test" };
        var held = status == OrderStatus.Processing;
        var device = new SerialImei { ProductId = product.Id, SerialNumber = "A",
            Status = held ? SerialImeiStatus.Reserved : SerialImeiStatus.Sold };
        var stock = new InventoryStock { ProductId = product.Id, Quantity = held ? 1 : 0, ReservedQuantity = held ? 1 : 0 };
        var order = new Order { OrderCode = "M4-1", CashierUserId = cashier.Id, Status = status, PaidAt = DateTime.UtcNow,
            CompletedAt = held ? null : DateTime.UtcNow };
        var item = new OrderItem { OrderId = order.Id, ProductId = product.Id, SerialImeiId = device.Id };
        db.AddRange(category, product, cashier, device, stock, order, item);
        await db.SaveChangesAsync();
        return new(stock, device, order, new(item.Id, product.Id, null, device.Id, 1));
    }

    private static InventoryAfterSaleService Inventory(AppDbContext db) =>
        new(new InventoryAfterSaleRepository(db), new SerialImeiRepository(db), new InventoryStockRepository(db), TimeProvider.System);

    [PostgresFact]
    public async Task Release_RollbackAfterSave_RestoresEveryRowAndAllowsRetry()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var seed = database.CreateContext();
        var f = await SeedAsync(seed, OrderStatus.Processing);
        var key = Guid.NewGuid();
        await using (var ctx = database.CreateContext())
        {
            var work = new UnitOfWork(ctx);
            await work.BeginTransactionAsync();
            Assert.True((await Inventory(ctx).ReleaseAsync(key, f.Order.Id, [f.Line])).IsSuccess);
            await work.SaveChangesAsync();
            await work.RollbackAsync();
        }
        await using (var ctx = database.CreateContext())
        {
            Assert.Empty(await ctx.Set<InventoryOperation>().ToListAsync());
            Assert.Empty(await ctx.Set<InventoryReservation>().ToListAsync());
            Assert.Equal(1, (await ctx.InventoryStocks.SingleAsync()).ReservedQuantity);
            Assert.Equal(SerialImeiStatus.Reserved, (await ctx.SerialImeis.SingleAsync()).Status);
            var work = new UnitOfWork(ctx);
            await work.BeginTransactionAsync();
            Assert.True((await Inventory(ctx).ReleaseAsync(key, f.Order.Id, [f.Line])).IsSuccess);
            await work.SaveChangesAsync();
            await work.CommitAsync();
        }
        await using var verify = database.CreateContext();
        Assert.Equal(ReservationState.Released, (await verify.Set<InventoryReservation>().SingleAsync()).State);
        Assert.Equal(0, (await verify.InventoryStocks.SingleAsync()).ReservedQuantity);
        Assert.Equal(SerialImeiStatus.InStock, (await verify.SerialImeis.SingleAsync()).Status);
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task ReturnAndWarranty_CompeteAfterBothReadSold_OnlyOneOperationCommits()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var seed = database.CreateContext();
        var f = await SeedAsync(seed, OrderStatus.Completed);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = limit.Token;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ready = 0;
        async Task<bool> TryAsync(bool warranty)
        {
            await using var ctx = database.CreateContext();
            var work = new UnitOfWork(ctx);
            await work.BeginTransactionAsync(ct);
            // Force the losing context to track the previous device state before either transaction obtains its lock.
            await ctx.SerialImeis.SingleAsync(ct);
            if (Interlocked.Increment(ref ready) == 2) gate.SetResult();
            await gate.Task.WaitAsync(ct);
            var inventory = Inventory(ctx);
            var outcome = warranty
                ? await inventory.ApplyWarrantyAsync(Guid.NewGuid(), Guid.NewGuid(), f.Order.Id, f.Line.OrderItemId!.Value,
                    f.Device.Id, WarrantyAction.ReceiveForRepair, null, f.Order.CashierUserId, ct)
                : await inventory.ReceiveReturnAsync(Guid.NewGuid(), f.Order.Id, [f.Line], ct);
            if (outcome.IsSuccess)
            {
                await work.SaveChangesAsync(ct);
                await work.CommitAsync(ct);
            }
            else await work.RollbackAsync(ct);
            return outcome.IsSuccess;
        }
        var outcomes = await Task.WhenAll(TryAsync(false), TryAsync(true));
        Assert.Single(outcomes, x => x);
        await using var verify = database.CreateContext();
        Assert.Equal(outcomes[0] ? SerialImeiStatus.Returned : SerialImeiStatus.UnderRepair,
            (await verify.SerialImeis.SingleAsync(ct)).Status);
        Assert.Single(await verify.Set<InventoryOperation>().ToListAsync(ct));
        Assert.Equal(0, (await verify.InventoryStocks.SingleAsync(ct)).Quantity);
        Assert.Empty(await verify.InventoryMovements.ToListAsync(ct));
    }

    [PostgresFact]
    public async Task Replace_TwoCasesChooseOneSpare_OnlyOneReducesStock()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var seed = database.CreateContext();
        var first = await SeedAsync(seed, OrderStatus.Completed);
        var old = new SerialImei { ProductId = first.Stock.ProductId, SerialNumber = "B", Status = SerialImeiStatus.Sold };
        var spare = new SerialImei { ProductId = first.Stock.ProductId, SerialNumber = "C", UnitCost = 75m };
        var order = new Order { OrderCode = "M4-2", CashierUserId = first.Order.CashierUserId,
            Status = OrderStatus.Completed, PaidAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow };
        var item = new OrderItem { ProductId = first.Stock.ProductId, OrderId = order.Id, SerialImeiId = old.Id };
        first.Stock.Quantity = 1;
        seed.AddRange(old, spare, order, item);
        await seed.SaveChangesAsync();

        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = limit.Token;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ready = 0;
        async Task<InventoryOperationResult> ReplaceAsync(Guid orderId, Guid itemId, Guid deviceId)
        {
            await using var ctx = database.CreateContext();
            await using var tx = await ctx.Database.BeginTransactionAsync(ct);
            await ctx.SerialImeis.SingleAsync(x => x.Id == spare.Id, ct);
            if (Interlocked.Increment(ref ready) == 2) gate.SetResult();
            await gate.Task.WaitAsync(ct);
            var result = await Inventory(ctx).ApplyWarrantyAsync(Guid.NewGuid(), Guid.NewGuid(), orderId, itemId,
                deviceId, WarrantyAction.Replace, spare.Id, order.CashierUserId, ct);
            if (result.IsSuccess)
            {
                await ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            else await tx.RollbackAsync(ct);
            return result;
        }
        var outcomes = await Task.WhenAll(ReplaceAsync(first.Order.Id, first.Line.OrderItemId!.Value, first.Device.Id),
            ReplaceAsync(order.Id, item.Id, old.Id));
        var success = Assert.Single(outcomes, x => x.IsSuccess);
        Assert.Equal(75m, success.UnitCost);
        await using var verify = database.CreateContext();
        var movement = await verify.InventoryMovements.SingleAsync(ct);
        Assert.Equal(success.MovementId, movement.Id);
        Assert.Equal((-1, 0, InventoryMovementType.WarrantyReplacement),
            (movement.QuantityChange, movement.QuantityAfter, movement.MovementType));
        Assert.Equal(0, (await verify.InventoryStocks.SingleAsync(ct)).Quantity);
        Assert.Equal(1, await verify.SerialImeis.CountAsync(x => x.Status == SerialImeiStatus.Defective, ct));
        Assert.Equal(2, await verify.SerialImeis.CountAsync(x => x.Status == SerialImeiStatus.Sold, ct));
    }
}
