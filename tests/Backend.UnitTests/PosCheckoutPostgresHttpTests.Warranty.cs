using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    private static async Task<ReceiveWarrantyRequest> WarrantyIntakeAsync(PosWorld world, Guid deviceId,
        string? orderCode = null, Guid? customerId = null)
    {
        await using var db = world.Database.CreateContext();
        var code = await db.SerialImeis.Where(d => d.Id == deviceId).Select(d => d.Imei ?? d.SerialNumber).SingleAsync();
        return new ReceiveWarrantyRequest(code, orderCode, customerId, "Máy không lên nguồn", "Khách bảo hành", "0901234567");
    }

    private static void AssertWarrantyReplay(WarrantyCaseDto expected, WarrantyCaseDto? replay)
    {
        Assert.NotNull(replay);
        var emptyEvents = Array.Empty<WarrantyCaseEventDto>();
        Assert.Equal(expected with { Events = emptyEvents }, replay with { Events = emptyEvents });
        Assert.Equal(expected.Events.ToArray(), replay.Events.ToArray());
    }

    [PostgresFact]
    public async Task WarrantyLookup_PhoneFollowsRoleAndUnknownIntakeLeavesNoWrites()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        await using var db = world.Database.CreateContext();
        var customer = await db.Customers.FirstAsync();
        var checkout = Checkout(world, shift.Id, world.DeviceIds[0], Total);
        checkout.CustomerId = customer.Id;
        await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        var request = await WarrantyIntakeAsync(world, world.DeviceIds[0], customerId: customer.Id);
        var path = $"/api/warranties/lookup?code={request.Code}&customerId={customer.Id}";
        var warehouse = (await world.Warehouse.GetFromJsonAsync<WarrantyLookupDto>(path))!;
        var sales = (await world.SalesA.Client.GetFromJsonAsync<WarrantyLookupDto>(path))!;
        Assert.True(warehouse.Eligible);
        Assert.True(sales.Eligible);
        Assert.Null(warehouse.CustomerPhone);
        Assert.Equal(customer.Phone, sales.CustomerPhone);

        var caseId = Guid.NewGuid();
        var eventCount = await db.Set<WarrantyCaseEvent>().CountAsync();
        using var response = await world.SalesA.Client.PostAsJsonAsync($"/api/warranties/{caseId}/receive",
            request with { Code = "UNKNOWN-WARRANTY-DEVICE" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await db.Set<WarrantyCase>().AnyAsync(c => c.Id == caseId));
        Assert.Equal(eventCount, await db.Set<WarrantyCaseEvent>().CountAsync());
        Assert.Equal(SerialImeiStatus.Sold, await db.SerialImeis.Where(d => d.Id == world.DeviceIds[0]).Select(d => d.Status).SingleAsync());
    }

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
        var request = await WarrantyIntakeAsync(world, world.DeviceIds[0], customerId: customer);
        var id = Guid.NewGuid();
        var path = $"/api/warranties/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await world.Manager.GetAsync(path)).StatusCode);
        using var anonymous = world.Api.Factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.Warehouse, path + "/receive", request));
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/receive", request with { CustomerId = null }));
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/receive", request with { CustomerId = Guid.NewGuid() }));

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

        using var intake = await world.SalesA.Client.PostAsJsonAsync(path + "/receive", request);
        Assert.Equal(HttpStatusCode.Created, intake.StatusCode);
        Assert.EndsWith(path, intake.Headers.Location!.ToString());
        var received = (await intake.Content.ReadFromJsonAsync<WarrantyCaseDto>())!;
        Assert.Equal(sale.Id, received.OrderId);
        Assert.Equal("Received", received.Status);
        Assert.Equal(start, received.WarrantyStartAt);
        Assert.Equal(end, received.WarrantyEndAt);
        using var intakeReplay = await world.SalesA.Client.PostAsJsonAsync(path + "/receive", request);
        Assert.Equal(HttpStatusCode.OK, intakeReplay.StatusCode);
        AssertWarrantyReplay(received, await intakeReplay.Content.ReadFromJsonAsync<WarrantyCaseDto>());
        await PostDto<WarrantyCaseDto>(world.Warehouse, path + "/inspect", new InspectWarrantyRequest("Repair", "Cần sửa"));
        await PostDto<WarrantyCaseDto>(world.Warehouse, path + "/repaired", new MarkWarrantyRepairedRequest());
        using var handback = await world.SalesA.Client.PostAsync(path + "/return", null);
        handback.EnsureSuccessStatusCode();
        var closed = await handback.Content.ReadFromJsonAsync<WarrantyCaseDto>();
        Assert.Equal("ReturnedToCustomer", closed!.Status);
        using var replay = await world.Manager.PostAsync(path + "/return", null);
        AssertWarrantyReplay(closed, await replay.Content.ReadFromJsonAsync<WarrantyCaseDto>());
        AssertWarrantyReplay(closed, await world.Manager.GetFromJsonAsync<WarrantyCaseDto>(path));
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
        var request = await WarrantyIntakeAsync(world, old, sale.OrderCode);
        var wrongDevice = await WarrantyIntakeAsync(world, spare, sale.OrderCode);
        var id = Guid.NewGuid();
        var path = $"/api/warranties/{id}";
        await PostDto<WarrantyCaseDto>(world.Manager, path + "/receive", request);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/receive", wrongDevice));
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path + "/replace", new ReplaceWarrantyDeviceRequest(old)));

        await PostDto<WarrantyCaseDto>(world.Warehouse, path + "/inspect", new InspectWarrantyRequest("Replace", "Không thể sửa"));
        var replace = new ReplaceWarrantyDeviceRequest(spare);
        var result = await PostDto<WarrantyCaseDto>(world.Manager, path + "/replace", replace);
        Assert.Equal("Replaced", result.Status);
        Assert.Equal(spare, result.ReplacementDeviceId);
        Assert.NotNull(result.MovementId);
        AssertWarrantyReplay(result, await PostDto<WarrantyCaseDto>(world.Manager, path + "/replace", replace));
        Assert.Equal(HttpStatusCode.Conflict, (await world.Manager.PostAsync(path + "/return", null)).StatusCode);
        Assert.Equal(11_000_000m, (await world.Manager.GetFromJsonAsync<WarrantyCaseDto>(path))!.ReplacementUnitCost);
        Assert.Null((await world.SalesA.Client.GetFromJsonAsync<WarrantyCaseDto>(path))!.ReplacementUnitCost);

        await using var db = world.Database.CreateContext();
        var claim = (await db.Set<WarrantyCase>().FindAsync(id))!;
        Assert.Equal((old, spare, world.ManagerUserId, 11_000_000m),
            (claim.DeviceId, claim.ReplacementDeviceId!.Value, claim.ClosedById!.Value, claim.ReplacementUnitCost!.Value));
        var movement = await db.InventoryMovements.SingleAsync(m => m.OrderItemId == sale.Items.Single().Id &&
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
        await db.SerialImeis.Where(d => world.DeviceIds.Contains(d.Id))
            .ExecuteUpdateAsync(row => row.SetProperty(device => device.SaleOrderItemId, (Guid?)null));

        var path = $"/api/warranties/{Guid.NewGuid()}/receive";
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, path,
            await WarrantyIntakeAsync(world, refunded.SerialImeiId!.Value, sale.OrderCode)));
        var claim = await PostDto<WarrantyCaseDto>(world.Manager, path,
            await WarrantyIntakeAsync(world, retained.SerialImeiId!.Value, sale.OrderCode));
        Assert.Equal(retained.Id, claim.OrderItemId);
        Assert.Equal("Received", claim.Status);
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
        var request = await WarrantyIntakeAsync(world, old, sale.OrderCode);
        await using var locked = world.Database.CreateContext();
        await using var observing = world.Database.CreateContext();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM orders WHERE id = {sale.Id} FOR UPDATE");
        var receive = PostStatus(world.Manager, $"/api/warranties/{caseId}/receive", request);
        var refund = PostStatus(world.Manager, $"/api/returns/{requested.Id}/settle", new SettleReturnRequest("Cash"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (await observing.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_stat_activity
            WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%orders%'
            """).SingleAsync(timeout.Token) < 2)
            await Task.Delay(20, timeout.Token);
        await transaction.CommitAsync();

        var results = await Task.WhenAll(receive, refund);
        Assert.Single(results, x => x is HttpStatusCode.Created or HttpStatusCode.OK);
        Assert.Single(results, x => x == HttpStatusCode.Conflict);
        var warrantyWon = results[0] == HttpStatusCode.Created;
        await using var db = world.Database.CreateContext();
        Assert.Equal(warrantyWon, await db.Set<WarrantyCase>().AnyAsync(c => c.Id == caseId));
        Assert.Equal(!warrantyWon, await db.OrderReturns.AnyAsync(r => r.OrderId == sale.Id));
        Assert.Equal(warrantyWon ? SerialImeiStatus.UnderRepair : SerialImeiStatus.Returned, (await db.SerialImeis.FindAsync(old))!.Status);
        Assert.Equal(0, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).Quantity);
    }
}
