using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task VietQrClose_OwnershipDeadlineCancelAndReplay_ReleasesOnlyThisQr()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 5m);
        var qr = await HeldQrAsync(world, shift.Id, world.DeviceIds[0], expired: false);
        var other = await HeldQrAsync(world, shift.Id, world.DeviceIds[1], expired: false);
        var url = $"/api/pos/vietqr/{qr.Id}";
        using var anonymous = world.Api.Factory.NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, await CloseQrAsync(anonymous, url + "/cancel"));
        Assert.Equal(HttpStatusCode.Forbidden, await CloseQrAsync(world.Warehouse, url + "/cancel"));
        Assert.Equal(HttpStatusCode.Forbidden, await CloseQrAsync(world.Manager, url + "/cancel"));
        Assert.Equal(HttpStatusCode.NotFound, await CloseQrAsync(world.SalesB.Client, url + "/cancel"));
        Assert.Equal(HttpStatusCode.Conflict, await CloseQrAsync(world.SalesA.Client, url + "/expire"));
        using var skipRelease = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{qr.OrderId}/status",
            new UpdateOrderStatusRequest { TargetStatus = "Cancelled" });
        Assert.Equal(HttpStatusCode.Conflict, skipRelease.StatusCode);

        using var success = await world.SalesA.Client.PostAsync(url + "/cancel", null);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var closed = await success.Content.ReadFromJsonAsync<ClosedVietQrHold>();
        Assert.Equal(new ClosedVietQrHold(qr.Id, qr.OrderId, "Failed", "Cancelled"), closed);
        using var replay = await world.SalesA.Client.PostAsync(url + "/cancel", null);
        Assert.Equal(closed, await replay.Content.ReadFromJsonAsync<ClosedVietQrHold>());
        Assert.Equal(HttpStatusCode.Conflict, await CloseQrAsync(world.SalesA.Client, url + "/expire"));

        await using var verify = world.Database.CreateContext();
        Assert.Equal(ReservationState.Released, (await verify.Set<InventoryReservation>().SingleAsync(r => r.OrderItemId == qr.ItemId)).State);
        Assert.Equal(ReservationState.Held, (await verify.Set<InventoryReservation>().SingleAsync(r => r.OrderItemId == other.ItemId)).State);
        var stock = await verify.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId);
        Assert.Equal((2, 1), (stock.Quantity, stock.ReservedQuantity));
        Assert.Equal(SerialImeiStatus.InStock, (await verify.SerialImeis.FindAsync(world.DeviceIds[0]))!.Status);
        Assert.Equal(SerialImeiStatus.Reserved, (await verify.SerialImeis.FindAsync(world.DeviceIds[1]))!.Status);
        Assert.Single(await verify.Set<InventoryOperation>().Where(o => o.Id == qr.Id && o.Kind == "Release").ToListAsync());
        Assert.False(await verify.InventoryMovements.AnyAsync(m => m.OrderItemId == qr.ItemId));
        Assert.Equal(0m, (await verify.PosSessions.FindAsync(shift.Id))!.VietQrSalesTotal);
    }

    [PostgresFact]
    public async Task VietQrClose_ExpireRacesCancel_TwoExpiryRetriesHaveOneReleaseAndNoCash()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await HeldQrAsync(world, shift.Id, world.DeviceIds[0], expired: true);
        var url = $"/api/pos/vietqr/{qr.Id}";
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpStatusCode> Run(string reason)
        {
            await gate.Task;
            return await CloseQrAsync(world.SalesA.Client, url + "/" + reason);
        }
        var calls = new[] { Run("cancel"), Run("expire"), Run("expire") };
        gate.SetResult();
        Assert.Equal(new[] { HttpStatusCode.Conflict, HttpStatusCode.OK, HttpStatusCode.OK }, await Task.WhenAll(calls));
        await using var verify = world.Database.CreateContext();
        Assert.Equal(VietQrStatus.Expired, (await verify.VietQrTransactions.FindAsync(qr.Id))!.Status);
        var order = (await verify.Orders.FindAsync(qr.OrderId))!;
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.PaidAt);
        Assert.Null(order.CompletedAt);
        var stock = await verify.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId);
        Assert.Equal((1, 0), (stock.Quantity, stock.ReservedQuantity));
        Assert.Equal(1, await verify.Set<InventoryOperation>().CountAsync(x => x.Id == qr.Id));
        Assert.False(await verify.InventoryMovements.AnyAsync(m => m.OrderItemId == qr.ItemId));
    }

    [PostgresFact]
    public async Task VietQrClose_VoucherOrPaymentEvidence_LeavesHoldAndOriginalQrUntouched()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await HeldQrAsync(world, shift.Id, world.DeviceIds[0], expired: true);
        var url = $"/api/pos/vietqr/{qr.Id}/expire";
        await using var db = world.Database.CreateContext();
        var order = (await db.Orders.FindAsync(qr.OrderId))!;
        var voucher = await db.Vouchers.SingleAsync(v => v.Code == "DEMO-OCT26");
        order.VoucherId = voucher.Id;
        await db.SaveChangesAsync();
        var uses = voucher.UsedCount;
        Assert.Equal(HttpStatusCode.Conflict, await CloseQrAsync(world.SalesA.Client, url));
        Assert.Equal(uses, await db.Vouchers.AsNoTracking().Where(v => v.Id == voucher.Id).Select(v => v.UsedCount).SingleAsync());

        order.VoucherId = null;
        var tx = (await db.VietQrTransactions.FindAsync(qr.Id))!;
        tx.Provider = "DemoBank";
        tx.ProviderTransactionId = "external-payment-evidence";
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, await CloseQrAsync(world.SalesA.Client, url));
        db.ChangeTracker.Clear();
        Assert.Equal(VietQrStatus.Pending, (await db.VietQrTransactions.FindAsync(qr.Id))!.Status);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.FindAsync(qr.OrderId))!.Status);
        Assert.Equal(ReservationState.Held, (await db.Set<InventoryReservation>().FindAsync(qr.ItemId))!.State);
        Assert.Equal(1, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).ReservedQuantity);

        using var admin = world.Api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        var confirmed = await db.VietQrTransactions.AsNoTracking().FirstAsync(q => q.Status == VietQrStatus.Confirmed);
        Assert.Equal(HttpStatusCode.Conflict, await CloseQrAsync(admin, $"/api/pos/vietqr/{confirmed.Id}/expire"));
        Assert.Equal(VietQrStatus.Confirmed, (await db.VietQrTransactions.FindAsync(confirmed.Id))!.Status);
    }

    [PostgresFact]
    public async Task VietQrClose_PersistenceFails_AfterRollbackSameQrCanBeRetried()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await HeldQrAsync(world, shift.Id, world.DeviceIds[0], expired: true);
        var url = $"/api/pos/vietqr/{qr.Id}/expire";
        await using var db = world.Database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_qr_close_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'QR fixture: release transaction interrupted'; END; $$;
            CREATE TRIGGER reject_qr_close BEFORE UPDATE ON vietqr_transactions FOR EACH ROW EXECUTE FUNCTION reject_qr_close_fixture();
            """);

        Assert.Equal(HttpStatusCode.InternalServerError, await CloseQrAsync(world.SalesA.Client, url));
        Assert.False(await db.Set<InventoryOperation>().AnyAsync(o => o.Id == qr.Id));
        Assert.Equal(ReservationState.Held, (await db.Set<InventoryReservation>().FindAsync(qr.ItemId))!.State);
        Assert.Equal(1, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).ReservedQuantity);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.FindAsync(qr.OrderId))!.Status);
        Assert.Equal(VietQrStatus.Pending, (await db.VietQrTransactions.FindAsync(qr.Id))!.Status);

        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_qr_close ON vietqr_transactions; DROP FUNCTION reject_qr_close_fixture();");
        Assert.Equal(HttpStatusCode.OK, await CloseQrAsync(world.SalesA.Client, url));
        db.ChangeTracker.Clear();
        Assert.Equal(ReservationState.Released, (await db.Set<InventoryReservation>().FindAsync(qr.ItemId))!.State);
        Assert.Equal(0, (await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId)).ReservedQuantity);
    }

    private sealed record QrHold(Guid Id, Guid OrderId, Guid ItemId);

    private static async Task<QrHold> HeldQrAsync(PosWorld world, Guid shiftId, Guid deviceId, bool expired)
    {
        // The payment creator belongs to the POS checkout workflow. Supply the pending document here
        // and actually reserve the IMEI through Inventory in the transaction that creates it.
        await using var db = world.Database.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var createdAt = DateTime.UtcNow.AddMinutes(expired ? -30 : -1);
        var id = Guid.NewGuid();
        var order = new Order { OrderCode = "TQR-" + id.ToString("N"), CashierUserId = world.SalesA.UserId,
            PaymentMethod = PaymentMethod.VietQr, PosSessionId = shiftId, Subtotal = Price,
            TaxAmount = Total - Price, TotalAmount = Total, CreatedAt = createdAt };
        var line = new OrderItem { OrderId = order.Id, ProductId = world.ProductId, SerialImeiId = deviceId, Quantity = 1,
            UnitPrice = Price, TotalPrice = Price, TaxAmount = Total - Price };
        var qr = new VietQrTransaction { Id = id, OrderId = order.Id, PosSessionId = shiftId, TransactionCode = "TQR-" + id.ToString("N"),
            Amount = Total, BankBin = "970422", BankAccountNumber = "test", QrContent = "fixture",
            CreatedAt = createdAt, ExpiresAt = createdAt.AddMinutes(15) };
        db.AddRange(order, line, qr);
        var inventory = new InventoryFulfillmentService(new SerialImeiRepository(db),
            new InventoryStockRepository(db), new InventoryAfterSaleRepository(db));
        var held = await inventory.ReserveAsync([new(line.Id, line.ProductId, null, deviceId, 1)]);
        Assert.True(held.IsSuccess);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(qr.Id, order.Id, line.Id);
    }

    private static async Task<HttpStatusCode> CloseQrAsync(HttpClient client, string url)
    {
        using var response = await client.PostAsync(url, null);
        return response.StatusCode;
    }
}
