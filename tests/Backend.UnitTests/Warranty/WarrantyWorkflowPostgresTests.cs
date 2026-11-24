using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using TechStore.UnitTests.Inventory;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task WarrantyWorkflow_RepairRejectedReplacementAndReplacement_ReconcileStockAndPreserveSales()
    {
        await using var world = await PosWorld.CreateAsync(4);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var checkout = Checkout(world, shift.Id, world.DeviceIds[0], 3 * Total);
        checkout.Items!.AddRange(world.DeviceIds.Skip(1).Take(2)
            .Select(id => new CreateOrderItemRequest { ProductId = world.ProductId, SerialImeiId = id, Quantity = 1 }));
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        await using var db = world.Database.CreateContext();
        var before = (await db.Orders.CountAsync(), await db.OrderReturns.CountAsync(), await db.CustomerLoyaltyPoints.CountAsync());
        for (var index = 0; index < 3; index++)
        {
            var intake = await WarrantyIntakeAsync(world, world.DeviceIds[index], sale.OrderCode);
            var lookupPath = $"/api/warranties/lookup?code={intake.Code}";
            var unproven = (await world.SalesA.Client.GetFromJsonAsync<WarrantyLookupDto>(lookupPath))!;
            Assert.False(unproven.Eligible);
            Assert.Contains(WarrantyReasonCodes.OrderCodeRequired, unproven.Reasons);
            Assert.Null(unproven.OrderCode);
            Assert.Null(unproven.PurchasedAt);
            Assert.Empty(unproven.History);

            var proven = (await world.SalesA.Client.GetFromJsonAsync<WarrantyLookupDto>($"{lookupPath}&orderCode={sale.OrderCode}"))!;
            Assert.True(proven.Eligible);
            Assert.Equal(sale.OrderCode, proven.OrderCode);
            var path = $"/api/warranties/{Guid.NewGuid()}";
            var received = await PostDto<WarrantyCaseDto>(world.SalesA.Client, path + "/receive", intake);
            var inspection = new InspectWarrantyRequest(index == 0 ? "Repair" : "Replace", "Kiểm định thiết bị");
            var inspected = await PostDto<WarrantyCaseDto>(world.Warehouse, path + "/inspect", inspection);
            Assert.Equal(index == 0 ? "Repairing" : "PendingReplacement", inspected.Status);
            if (index == 1)
            {
                var decision = await PostDto<WarrantyCaseDto>(world.Manager, path + "/reject-replacement",
                    new RejectWarrantyReplacementRequest("Đã xác nhận có thể sửa"));
                Assert.Equal("Repairing", decision.Status);
            }

            WarrantyCaseDto closed;
            if (index < 2)
            {
                await PostDto<WarrantyCaseDto>(world.Warehouse, path + "/repaired", new MarkWarrantyRepairedRequest());
                using var response = await world.SalesA.Client.PostAsync(path + "/return", null);
                response.EnsureSuccessStatusCode();
                closed = (await response.Content.ReadFromJsonAsync<WarrantyCaseDto>())!;
                Assert.Equal("ReturnedToCustomer", closed.Status);
                Assert.Equal(index == 0 ? 4 : 5, closed.Events.Count);
                Assert.Equal(SerialImeiStatus.Sold, await db.SerialImeis.Where(d => d.Id == received.DeviceId).Select(d => d.Status).SingleAsync());
            }
            else
            {
                closed = await PostDto<WarrantyCaseDto>(world.Manager, path + "/replace", new ReplaceWarrantyDeviceRequest(world.DeviceIds[3]));
                Assert.Equal("Replaced", closed.Status);
                var salesRead = (await world.SalesA.Client.GetFromJsonAsync<WarrantyCaseDto>(path))!;
                var warehouseRead = (await world.Warehouse.GetFromJsonAsync<WarrantyCaseDto>(path))!;
                Assert.Equal(closed.ReplacementDeviceCode, salesRead.ReplacementDeviceCode);
                Assert.Null(salesRead.ReplacementUnitCost);
                Assert.Equal(11_000_000m, warehouseRead.ReplacementUnitCost);
            }

            Assert.Equal(before, (await db.Orders.CountAsync(), await db.OrderReturns.CountAsync(), await db.CustomerLoyaltyPoints.CountAsync()));
            await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(world.Database);
        }
    }

    [PostgresFact]
    public async Task WarrantyRace_TwoIntakesForSameDevice_OneCreatesOneConflicts()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout",
            Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var intake = await WarrantyIntakeAsync(world, world.DeviceIds[0], sale.OrderCode);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var results = await RaceUnderOrderLockAsync(world, sale.Id,
            () => PostStatus(world.SalesA.Client, $"/api/warranties/{ids[0]}/receive", intake),
            () => PostStatus(world.Manager, $"/api/warranties/{ids[1]}/receive", intake));
        Assert.Single(results, status => status == HttpStatusCode.Created);
        Assert.Single(results, status => status == HttpStatusCode.Conflict);
        await using var db = world.Database.CreateContext();
        var claim = await db.Set<WarrantyCase>().Include(c => c.Events).SingleAsync(c => ids.Contains(c.Id));
        Assert.Single(claim.Events);
        Assert.Equal(SerialImeiStatus.UnderRepair, await db.SerialImeis.Where(d => d.Id == claim.DeviceId).Select(d => d.Status).SingleAsync());
        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(world.Database);
    }

    [PostgresFact]
    public async Task WarrantyRace_TwoDifferentReplacements_OneCommitsAndRefundIsBlocked()
    {
        await using var world = await PosWorld.CreateAsync(3);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout",
            Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var intake = await WarrantyIntakeAsync(world, world.DeviceIds[0], sale.OrderCode);
        var path = $"/api/warranties/{Guid.NewGuid()}";
        await PostDto<WarrantyCaseDto>(world.SalesA.Client, path + "/receive", intake);
        await PostDto<WarrantyCaseDto>(world.Warehouse, path + "/inspect", new InspectWarrantyRequest("Replace", "Cần đổi"));

        var results = await RaceUnderOrderLockAsync(world, sale.Id,
            () => PostStatus(world.Manager, path + "/replace", new ReplaceWarrantyDeviceRequest(world.DeviceIds[1])),
            () => PostStatus(world.Manager, path + "/replace", new ReplaceWarrantyDeviceRequest(world.DeviceIds[2])));
        Assert.Single(results, status => status == HttpStatusCode.OK);
        Assert.Single(results, status => status == HttpStatusCode.Conflict);

        await using var db = world.Database.CreateContext();
        var replacement = (await world.Manager.GetFromJsonAsync<WarrantyCaseDto>(path))!;
        Assert.Equal("Replaced", replacement.Status);
        Assert.Equal(1, await db.InventoryStocks.Where(s => s.ProductId == world.ProductId).Select(s => s.Quantity).SingleAsync());
        Assert.Single(await db.InventoryMovements.Where(m => m.OrderItemId == replacement.OrderItemId
            && m.MovementType == InventoryMovementType.WarrantyReplacement).ToListAsync());
        await SerialStockReconciliationHelper.AssertSerialStockReconciledAsync(world.Database);

        var lookup = (await world.Manager.GetFromJsonAsync<WarrantyLookupDto>(
            $"/api/warranties/lookup?code={replacement.ReplacementDeviceCode}&orderCode={sale.OrderCode}"))!;
        Assert.True(lookup.Eligible);
        Assert.Equal(sale.OrderCode, lookup.OrderCode);

        var refund = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(sale, 1));
        await world.OpenShiftAsync(new(world.Manager, world.ManagerUserId), 2 * Total);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, $"/api/returns/{refund.Id}/settle", new SettleReturnRequest("Cash")));
        Assert.False(await db.OrderReturns.AnyAsync(r => r.OrderId == sale.Id));
        var afterDeniedRefund = (await world.Manager.GetFromJsonAsync<WarrantyLookupDto>(
            $"/api/warranties/lookup?code={replacement.ReplacementDeviceCode}&orderCode={sale.OrderCode}"))!;
        Assert.True(afterDeniedRefund.Eligible, string.Join(", ", afterDeniedRefund.Reasons));
        var nextIntake = await WarrantyIntakeAsync(world, replacement.ReplacementDeviceId!.Value, sale.OrderCode);
        Assert.Equal(HttpStatusCode.Created,
            await PostStatus(world.SalesA.Client, $"/api/warranties/{Guid.NewGuid()}/receive", nextIntake));
    }

    private static async Task<HttpStatusCode[]> RaceUnderOrderLockAsync(PosWorld world, Guid orderId,
        Func<Task<HttpStatusCode>> first, Func<Task<HttpStatusCode>> second)
    {
        await using var locked = world.Database.CreateContext();
        await using var observing = world.Database.CreateContext();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM orders WHERE id = {orderId} FOR UPDATE");
        var left = first();
        var right = second();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (await observing.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_stat_activity
            WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%orders%'
            """).SingleAsync(timeout.Token) < 2)
            await Task.Delay(20, timeout.Token);
        await transaction.CommitAsync();
        return await Task.WhenAll(left, right);
    }
}
