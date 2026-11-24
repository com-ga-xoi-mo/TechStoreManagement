using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task WarrantyCase_MemberRepair_RequiresCustomerAndActiveCoverageThenReturnsSameDevice()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        await using var db = world.Database.CreateContext();
        var customer = await db.Customers.Select(c => c.Id).FirstAsync();
        var checkout = Checkout(world, shift.Id, world.DeviceIds[0], Total);
        checkout.CustomerId = customer;
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        var item = sale.Items.Single();
        var request = new ReceiveWarrantyRequest(sale.Id, item.Id, world.DeviceIds[0], customer);
        var id = Guid.NewGuid();
        var path = $"/api/warranties/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await world.Manager.GetAsync(path)).StatusCode);
        using var anonymous = world.Api.Factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.SalesA.Client, path + "/receive", request));
        Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.Warehouse, path + "/receive", request));
        Assert.Equal(HttpStatusCode.NotFound, await PostStatus(world.Manager, path + "/receive", request with { CustomerId = null }));
        Assert.Equal(HttpStatusCode.NotFound, await PostStatus(world.Manager, path + "/receive", request with { CustomerId = Guid.NewGuid() }));

        var device = (await db.SerialImeis.FindAsync(world.DeviceIds[0]))!;
        var start = device.WarrantyStartAt;
        var end = device.WarrantyEndAt;
        device.WarrantyStartAt = start!.Value.AddDays(1);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/receive", request));
        device.WarrantyStartAt = start.Value.AddYears(-1);
        device.WarrantyEndAt = start.Value;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/receive", request));
        Assert.False(await db.Set<WarrantyCase>().AnyAsync(c => c.Id == id));
        Assert.Equal(SerialImeiStatus.Sold, await db.SerialImeis.AsNoTracking().Where(i => i.Id == device.Id).Select(i => i.Status).SingleAsync());
        device.WarrantyStartAt = start;
        device.WarrantyEndAt = end;
        await db.SaveChangesAsync();

        var received = await PostDto<WarrantyCaseDto>(world.Manager, path + "/receive", request);
        Assert.Equal("UnderRepair", received.Status);
        Assert.Equal(start, received.WarrantyStartAt);
        Assert.Equal(end, received.WarrantyEndAt);
        Assert.Equal(received, await PostDto<WarrantyCaseDto>(world.Manager, path + "/receive", request));
        using var handback = await world.Manager.PostAsync(path + "/return", null);
        handback.EnsureSuccessStatusCode();
        var closed = await handback.Content.ReadFromJsonAsync<WarrantyCaseDto>();
        Assert.Equal("ReturnedToCustomer", closed!.Status);
        using var replay = await world.Manager.PostAsync(path + "/return", null);
        Assert.Equal(closed, await replay.Content.ReadFromJsonAsync<WarrantyCaseDto>());
        Assert.Equal(closed, await world.Manager.GetFromJsonAsync<WarrantyCaseDto>(path));
        Assert.Null(closed.MovementId);
        Assert.Equal(SerialImeiStatus.Sold, await db.SerialImeis.AsNoTracking().Where(i => i.Id == device.Id).Select(i => i.Status).SingleAsync());
        Assert.Equal(0, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).Quantity);
    }

    [PostgresFact]
    public async Task WarrantyCase_WalkInReplacement_ReplayKeepsOneMovementAndInternalCost()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var old = world.DeviceIds[0];
        var spare = world.DeviceIds[1];
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", Checkout(world, shift.Id, old, Total));
        var request = new ReceiveWarrantyRequest(sale.Id, sale.Items.Single().Id, old, null);
        var id = Guid.NewGuid();
        var path = $"/api/warranties/{id}";
        await PostDto<WarrantyCaseDto>(world.Manager, path + "/receive", request);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/receive", request with { DeviceId = spare }));
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/replace", new ReplaceWarrantyDeviceRequest(old)));

        var replace = new ReplaceWarrantyDeviceRequest(spare);
        var result = await PostDto<WarrantyCaseDto>(world.Manager, path + "/replace", replace);
        Assert.Equal("Replaced", result.Status);
        Assert.Equal(spare, result.ReplacementDeviceId);
        Assert.NotNull(result.MovementId);
        Assert.Equal(result, await PostDto<WarrantyCaseDto>(world.Manager, path + "/replace", replace));
        Assert.Equal(HttpStatusCode.Conflict, (await world.Manager.PostAsync(path + "/return", null)).StatusCode);
        using var lookup = await world.Manager.GetAsync(path);
        Assert.DoesNotContain("unitCost", await lookup.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        await using var db = world.Database.CreateContext();
        var claim = (await db.Set<WarrantyCase>().FindAsync(id))!;
        Assert.Equal((old, spare, world.ManagerUserId, 11_000_000m),
            (claim.DeviceId, claim.ReplacementDeviceId!.Value, claim.ClosedById!.Value, claim.ReplacementUnitCost!.Value));
        var movement = await db.InventoryMovements.SingleAsync(m => m.OrderItemId == request.OrderItemId &&
            m.MovementType == InventoryMovementType.WarrantyReplacement);
        Assert.Equal(result.MovementId, movement.Id);
        var replacement = (await db.SerialImeis.FindAsync(spare))!;
        Assert.Equal(SerialImeiStatus.Defective, (await db.SerialImeis.FindAsync(old))!.Status);
        Assert.Equal(SerialImeiStatus.Sold, replacement.Status);
        Assert.Equal((result.WarrantyStartAt, result.WarrantyEndAt), (replacement.WarrantyStartAt!.Value, replacement.WarrantyEndAt!.Value));
        Assert.Equal(0, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).Quantity);
        Assert.False(await db.OrderReturns.AnyAsync(r => r.OrderId == sale.Id));
        Assert.False(await db.CustomerLoyaltyPoints.AnyAsync(r => r.OrderId == sale.Id));
    }

    [PostgresFact]
    public async Task WarrantyCase_LegacyPartiallyRefundedOrder_OnlyRemainingLineIsEligible()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var checkout = Checkout(world, shift.Id, world.DeviceIds[0], 2 * Total);
        checkout.Items!.Add(new() { ProductId = world.ProductId, Quantity = 1, SerialImeiId = world.DeviceIds[1] });
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        var refunded = sale.Items.Single(i => i.SerialImeiId == world.DeviceIds[0]);
        var retained = sale.Items.Single(i => i.SerialImeiId == world.DeviceIds[1]);

        // Legacy refunds may have a settlement without the M4 inventory receipt and its Returned state.
        await using var db = world.Database.CreateContext();
        (await db.Orders.FindAsync(sale.Id))!.Status = OrderStatus.PartiallyRefunded;
        db.OrderReturns.Add(new OrderReturn { OrderId = sale.Id, OrderItemId = refunded.Id,
            ProcessedByUserId = world.ManagerUserId, Quantity = 1, Reason = "M3 settlement",
            RefundMethod = PaymentMethod.VietQr, RefundAmount = Total });
        await db.SaveChangesAsync();

        var path = $"/api/warranties/{Guid.NewGuid()}/receive";
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path,
            new ReceiveWarrantyRequest(sale.Id, refunded.Id, refunded.SerialImeiId!.Value, null)));
        var claim = await PostDto<WarrantyCaseDto>(world.Manager, path,
            new ReceiveWarrantyRequest(sale.Id, retained.Id, retained.SerialImeiId!.Value, null));
        Assert.Equal(retained.Id, claim.OrderItemId);
        Assert.Equal("UnderRepair", claim.Status);
    }

    [PostgresFact]
    public async Task WarrantyCase_ConcurrentRefundOfSameSoldImei_OnlyOneReceivesDevice()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var old = world.DeviceIds[0];
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", Checkout(world, shift.Id, old, Total));
        var requested = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(sale, 1));
        await PostDto<PosShiftDto>(world.Manager, "/api/pos/shifts", new OpenShiftRequest { OpeningBalance = 2 * Total });
        var caseId = Guid.NewGuid();
        await using var locked = world.Database.CreateContext();
        await using var observing = world.Database.CreateContext();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM orders WHERE id = {sale.Id} FOR UPDATE");
        var receive = PostStatus(world.Manager, $"/api/warranties/{caseId}/receive",
            new ReceiveWarrantyRequest(sale.Id, sale.Items.Single().Id, old, null));
        var refund = PostStatus(world.Manager, $"/api/returns/{requested.Id}/settle", new SettleReturnRequest("Cash"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (await observing.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_stat_activity
            WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%orders%'
            """).SingleAsync(timeout.Token) < 2)
            await Task.Delay(20, timeout.Token);
        await transaction.CommitAsync();

        var results = await Task.WhenAll(receive, refund);
        Assert.Single(results, x => x == HttpStatusCode.OK);
        Assert.Single(results, x => x == HttpStatusCode.Conflict);
        var warrantyWon = results[0] == HttpStatusCode.OK;
        await using var db = world.Database.CreateContext();
        Assert.Equal(warrantyWon, await db.Set<WarrantyCase>().AnyAsync(c => c.Id == caseId));
        Assert.Equal(!warrantyWon, await db.OrderReturns.AnyAsync(r => r.OrderId == sale.Id));
        Assert.Equal(warrantyWon ? SerialImeiStatus.UnderRepair : SerialImeiStatus.Returned, (await db.SerialImeis.FindAsync(old))!.Status);
        Assert.Equal(0, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).Quantity);
    }
}
