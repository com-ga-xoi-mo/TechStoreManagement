using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task ReturnCash_RacesWarrantyCallerAndRepairsOrReplacesOnlyTheAuthorizedSoldDevice()
    {
        // Arrange: two sold devices plus one in-stock spare. POS and Orders use real JWT endpoints.
        await using var world = await PosWorld.CreateAsync(3);
        var saleShift = await world.OpenShiftAsync(world.SalesA, 0m);
        var cashShift = await world.OpenShiftAsync(new(world.Manager, world.ManagerUserId), 2 * Total);
        var checkout = Checkout(world, saleShift.Id, world.DeviceIds[0], 2 * Total);
        checkout.Items!.Add(new() { ProductId = world.ProductId, Quantity = 1, SerialImeiId = world.DeviceIds[1] });
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        var contested = sale.Items.Single(i => i.SerialImeiId == world.DeviceIds[0]);
        var other = sale.Items.Single(i => i.SerialImeiId == world.DeviceIds[1]);
        var claim = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", new CreateReturnRequest(Guid.NewGuid(),
            sale.Id, "Device returned", [new(contested.Id, 1, contested.SerialImeiId)]));
        var caseId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();

        // Model the external warranty caller: one request scope and UnitOfWork per decision.
        async Task<InventoryOperationResult> Warranty(Guid op, Guid warrantyCase, OrderItemDto item,
            WarrantyAction action, Guid? replacementId = null)
        {
            using var scope = world.Api.Factory.Services.CreateScope();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var inventory = scope.ServiceProvider.GetRequiredService<IInventoryAfterSaleService>();
            await unit.BeginTransactionAsync();
            var result = await inventory.ApplyWarrantyAsync(op, warrantyCase, sale.Id, item.Id, item.SerialImeiId!.Value,
                action, replacementId, world.ManagerUserId);
            if (result.IsSuccess)
            {
                await unit.SaveChangesAsync();
                await unit.CommitAsync();
            }
            else await unit.RollbackAsync();
            return result;
        }

        // Hold the sale until both independent connections are waiting for its lock.
        await using var db = world.Database.CreateContext();
        await using var blocker = world.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM orders WHERE id={sale.Id} FOR UPDATE");
        var payout = PostStatus(world.Manager, $"/api/returns/{claim.Id}/settle", new SettleReturnRequest("Cash"));
        var intake = Warranty(intakeId, caseId, contested, WarrantyAction.ReceiveForRepair);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_stat_activity
            WHERE datname=current_database() AND wait_event_type='Lock' AND query ILIKE '%orders%'
            """).SingleAsync(timeout.Token) < 2)
            await Task.Delay(20, timeout.Token);
        await transaction.CommitAsync();

        // Assert: whichever claimant wins, the losing transaction leaves its side effects absent.
        var paidStatus = await payout;
        var repair = await intake;
        Assert.Contains(paidStatus, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        var paid = paidStatus == HttpStatusCode.OK;
        Assert.NotEqual(paid, repair.IsSuccess);
        Assert.Equal(paid, await db.OrderReturns.AnyAsync(r => r.OrderItemId == contested.Id));
        Assert.Equal(paid, await db.Set<CashRefundPayout>().AnyAsync(p => p.RefundId == claim.Id));
        Assert.Equal(repair.IsSuccess, await db.Set<InventoryOperation>().AnyAsync(o => o.Id == intakeId));
        Assert.Equal(paid ? Total : 0m, await db.PosSessions.Where(s => s.Id == cashShift.Id).Select(s => s.CashRefundTotal).SingleAsync());
        Assert.Equal(paid ? SerialImeiStatus.Returned : SerialImeiStatus.UnderRepair,
            await db.SerialImeis.Where(s => s.Id == contested.SerialImeiId).Select(s => s.Status).SingleAsync());

        // Act and assert: repair, hand back, and then replace the second device in an independent case.
        var old = await db.SerialImeis.AsNoTracking().SingleAsync(s => s.Id == other.SerialImeiId);
        var warrantyCase = Guid.NewGuid();
        Assert.True((await Warranty(Guid.NewGuid(), warrantyCase, other, WarrantyAction.ReceiveForRepair)).IsSuccess);
        Assert.Equal(SerialImeiStatus.UnderRepair, await db.SerialImeis.Where(s => s.Id == old.Id).Select(s => s.Status).SingleAsync());
        Assert.False((await Warranty(Guid.NewGuid(), Guid.NewGuid(), other, WarrantyAction.ReturnToCustomer)).IsSuccess);
        Assert.True((await Warranty(Guid.NewGuid(), warrantyCase, other, WarrantyAction.ReturnToCustomer)).IsSuccess);
        var returned = await db.SerialImeis.AsNoTracking().SingleAsync(s => s.Id == old.Id);
        Assert.Equal(SerialImeiStatus.Sold, returned.Status);
        Assert.Equal(old.WarrantyEndAt, returned.WarrantyEndAt);
        var replacementOp = Guid.NewGuid();
        var replacementCase = Guid.NewGuid();
        var spareId = world.DeviceIds[2];
        var replacement = await Warranty(replacementOp, replacementCase, other, WarrantyAction.Replace, spareId);
        Assert.True(replacement.IsSuccess);
        Assert.Equal(11_000_000m, replacement.UnitCost);
        Assert.Equal(replacement, await Warranty(replacementOp, replacementCase, other, WarrantyAction.Replace, spareId));
        var spare = await db.SerialImeis.AsNoTracking().SingleAsync(s => s.Id == spareId);
        Assert.Equal((SerialImeiStatus.Sold, old.WarrantyStartAt, old.WarrantyEndAt), (spare.Status, spare.WarrantyStartAt, spare.WarrantyEndAt));
        Assert.Equal(SerialImeiStatus.Defective, await db.SerialImeis.Where(s => s.Id == old.Id).Select(s => s.Status).SingleAsync());
        var movement = await db.InventoryMovements.SingleAsync(m => m.OrderItemId == other.Id && m.MovementType == InventoryMovementType.WarrantyReplacement);
        Assert.Equal(replacement.MovementId, movement.Id);
        Assert.Equal(-1, movement.QuantityChange);
        Assert.Equal(0, await db.InventoryStocks.Where(s => s.ProductId == world.ProductId).Select(s => s.Quantity).SingleAsync());
        Assert.False(await db.OrderReturns.AnyAsync(r => r.OrderItemId == other.Id));
    }
}
