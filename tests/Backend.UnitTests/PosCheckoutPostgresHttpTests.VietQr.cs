using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Sales;
using TechStore.Api.Sales.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// Real JWT pipeline, services, repositories and PostgreSQL for the VietQR flow: create, confirm, webhook, expire,
// cancel, close and the races between them.
public sealed partial class PosCheckoutPostgresHttpTests
{
    private const string VietQrSecret = "test-webhook-secret-0123456789abcdef";
    private const string BankAccount = "0123456789";

    // ---------------------------------------------------------------- create

    [PostgresFact]
    public async Task VietQr_CreateThenConfirmByManager_CompletesTheSaleOnceAndCreditsOnlyTheQrTotal()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 1_000_000m);
        var request = QrRequest(world, shift.Id, world.DeviceIds[0]);

        using var created = await world.SalesA.Client.PostAsJsonAsync("/api/pos/vietqr", request);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var qr = (await created.Content.ReadFromJsonAsync<VietQrPaymentDto>())!;
        Assert.Equal("Pending", qr.Status);
        Assert.Equal(Total, qr.Amount);
        Assert.Equal("TECHSTORE TEST", qr.BankAccountName);
        Assert.True(VietQrPayload.HasValidCrc(qr.QrContent));
        Assert.Contains(qr.TransactionCode, qr.QrContent);
        Assert.StartsWith("iVBOR", qr.QrImagePng);
        Assert.Equal(qr.CreatedAt.AddMinutes(15), qr.ExpiresAt);
        await using (var db = world.Database.CreateContext())
        {
            Assert.Equal(SerialImeiStatus.Reserved, (await db.SerialImeis.FindAsync(world.DeviceIds[0]))!.Status);
            var held = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == qr.OrderId);
            Assert.Equal((OrderStatus.Pending, PaymentMethod.VietQr, shift.Id), (held.Status, held.PaymentMethod, held.PosSessionId));
            Assert.Null(held.PaidAt);
        }

        using var again = await world.SalesA.Client.PostAsJsonAsync("/api/pos/vietqr", request);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(qr.Id, (await again.Content.ReadFromJsonAsync<VietQrPaymentDto>())!.Id);

        using var status = await world.SalesA.Client.GetAsync($"/api/pos/vietqr/{qr.Id}");
        Assert.Equal("Pending", (await status.Content.ReadFromJsonAsync<VietQrPaymentDto>())!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await world.SalesB.Client.GetAsync($"/api/pos/vietqr/{qr.Id}")).StatusCode);

        using var confirm = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm",
            new ConfirmVietQrRequest { ReferenceCode = "FT26112500123", ReceivedAmount = Total });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        await using var verify = world.Database.CreateContext();
        var saved = await verify.VietQrTransactions.AsNoTracking().SingleAsync(q => q.Id == qr.Id);
        Assert.Equal((VietQrStatus.Confirmed, "manual", "FT26112500123"), (saved.Status, saved.Provider, saved.ProviderTransactionId));
        Assert.Equal(world.ManagerUserId, saved.ConfirmedByUserId);
        var order = await verify.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == qr.OrderId);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.PaidAt);
        Assert.Equal(SerialImeiStatus.Sold, (await verify.SerialImeis.FindAsync(world.DeviceIds[0]))!.Status);
        Assert.Equal(SerialImeiStatus.InStock, (await verify.SerialImeis.FindAsync(world.DeviceIds[1]))!.Status);
        Assert.Equal(1, await verify.InventoryMovements.CountAsync(m => m.OrderItemId == order.Items.Single().Id && m.MovementType == InventoryMovementType.Sale));
        var shiftRow = await verify.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        Assert.Equal((Total, 0m), (shiftRow.VietQrSalesTotal, shiftRow.CashSalesTotal));

        using var replay = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm",
            new ConfirmVietQrRequest { ReferenceCode = "FT26112500123", ReceivedAmount = Total });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await using var after = world.Database.CreateContext();
        Assert.Equal(Total, (await after.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        Assert.Equal(1, await after.InventoryMovements.CountAsync(m => m.OrderItemId == order.Items.Single().Id && m.MovementType == InventoryMovementType.Sale));
    }

    [PostgresFact]
    public async Task VietQr_RolesAndOwnership_FollowThePermissionTable()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var request = QrRequest(world, shift.Id, world.DeviceIds[0]);

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Manager.PostAsJsonAsync("/api/pos/vietqr", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Warehouse.PostAsJsonAsync("/api/pos/vietqr", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.SalesB.Client.PostAsJsonAsync("/api/pos/vietqr", request)).StatusCode);

        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        var confirm = new ConfirmVietQrRequest { ReferenceCode = "FT1", ReceivedAmount = Total };
        Assert.Equal(HttpStatusCode.Forbidden, (await world.SalesA.Client.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm", confirm)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Warehouse.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm", confirm)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Warehouse.GetAsync($"/api/pos/vietqr/{qr.Id}")).StatusCode);

        var listA = await world.SalesA.Client.GetFromJsonAsync<PagedResultDto<VietQrPaymentDto>>($"/api/pos/vietqr?status=Pending&posSessionId={shift.Id}");
        var listB = await world.SalesB.Client.GetFromJsonAsync<PagedResultDto<VietQrPaymentDto>>("/api/pos/vietqr?status=Pending");
        var listM = await world.Manager.GetFromJsonAsync<PagedResultDto<VietQrPaymentDto>>("/api/pos/vietqr?status=Pending");
        Assert.Equal(qr.Id, Assert.Single(listA!.Items).Id);
        Assert.Null(listA.Items[0].QrImagePng);
        Assert.Empty(listB!.Items);
        Assert.Contains(listM!.Items, p => p.Id == qr.Id);
    }

    [PostgresFact]
    public async Task VietQr_WrongAmountOrReference_ChangesNothingAndTheSameReferenceCannotPayTwoOrders()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var first = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        var second = await CreateQrAsync(world, shift.Id, world.DeviceIds[1]);

        using var wrong = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{first.Id}/confirm", new ConfirmVietQrRequest { ReferenceCode = "FT1", ReceivedAmount = Total - 1 });
        using var blank = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{first.Id}/confirm", new ConfirmVietQrRequest { ReferenceCode = " ", ReceivedAmount = Total });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{first.Id}/confirm", new ConfirmVietQrRequest { ReferenceCode = "FT1", ReceivedAmount = Total })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{second.Id}/confirm", new ConfirmVietQrRequest { ReferenceCode = "FT1", ReceivedAmount = Total })).StatusCode);

        await using var db = world.Database.CreateContext();
        Assert.Equal(VietQrStatus.Pending, (await db.VietQrTransactions.FindAsync(second.Id))!.Status);
        Assert.Equal(Total, (await db.PosSessions.SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        Assert.Empty(await db.VietQrBankEvents.ToListAsync());
    }

    // --------------------------------------------------------------- webhook

    [PostgresFact]
    public async Task VietQrWebhook_SignedNotice_CompletesTheSaleAndReplayAppliesNothingTwice()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        using var bank = world.Api.Factory.NewClient();
        var body = Notice("FT777", Total, $"CK {qr.TransactionCode} mua may");

        using var first = await SendWebhookAsync(bank, body);
        using var replay = await SendWebhookAsync(bank, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal("sandbox", firstJson.RootElement.GetProperty("mode").GetString());
        Assert.Equal("Applied", firstJson.RootElement.GetProperty("result").GetString());
        Assert.Equal("Duplicate", replayJson.RootElement.GetProperty("result").GetString());
        await using var db = world.Database.CreateContext();
        var saved = await db.VietQrTransactions.AsNoTracking().SingleAsync(q => q.Id == qr.Id);
        Assert.Equal((VietQrStatus.Confirmed, "sandbox", (Guid?)null), (saved.Status, saved.Provider, saved.ConfirmedByUserId));
        Assert.Equal(OrderStatus.Completed, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == qr.OrderId)).Status);
        Assert.Equal(Total, (await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        var applied = await db.VietQrBankEvents.AsNoTracking().SingleAsync();
        Assert.Equal((VietQrEventStatus.Applied, "FT777"), (applied.Status, applied.ProviderTransactionId));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(m => m.MovementType == InventoryMovementType.Sale && m.InventoryStock.ProductId == world.ProductId));
    }

    [PostgresFact]
    public async Task VietQrWebhook_BadSignatureStaleTimestampOrBody_AreRefusedAndStoreNothing()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        using var bank = world.Api.Factory.NewClient();
        var body = Notice("FT1", Total, qr.TransactionCode);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWebhookAsync(bank, body, signature: "0".PadLeft(64, '0'))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWebhookAsync(bank, body, stamp: (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 900).ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWebhookAsync(bank, body, tamper: true)).StatusCode);
        using var noHeaders = await bank.PostAsync("/api/payments/vietqr/webhook", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, noHeaders.StatusCode);
        using var tooBig = await SendWebhookAsync(bank, new string(' ', VietQrPaymentsController.MaxWebhookBytes + 10));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        using var notJson = await SendWebhookAsync(bank, "not json");
        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);

        await using var db = world.Database.CreateContext();
        Assert.Empty(await db.VietQrBankEvents.ToListAsync());
        Assert.Equal(VietQrStatus.Pending, (await db.VietQrTransactions.FindAsync(qr.Id))!.Status);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.FindAsync(qr.OrderId))!.Status);
    }

    [PostgresFact]
    public async Task VietQrWebhook_AmountAccountOrCodeWrong_KeepsTheMoneyForReconciliation()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        using var bank = world.Api.Factory.NewClient();

        var replies = new List<string>();
        foreach (var notice in new[]
        {
            Notice("FT1", Total - 1, qr.TransactionCode),
            Notice("FT2", Total, qr.TransactionCode, account: "9999999999"),
            Notice("FT3", Total, "TSNOSUCHCODE")
        })
        {
            using var reply = await SendWebhookAsync(bank, notice);
            replies.Add(await reply.Content.ReadAsStringAsync());
        }
        // The anonymous endpoint never echoes an account number back to the caller.
        Assert.All(replies, r => Assert.DoesNotContain("9999999999", r));
        Assert.All(replies, r => Assert.DoesNotContain(BankAccount, r));

        var list = await world.Manager.GetFromJsonAsync<PagedResultDto<VietQrBankEventDto>>("/api/payments/vietqr/events?status=NeedsReconciliation");
        Assert.Equal(new[] { "AccountMismatch", "AmountMismatch", "Unmatched" }, list!.Items.Select(e => e.Reason).Order().ToArray());
        await using var db = world.Database.CreateContext();
        Assert.Equal(VietQrStatus.Pending, (await db.VietQrTransactions.FindAsync(qr.Id))!.Status);
        Assert.Equal(0m, (await db.PosSessions.SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);

        var open = list.Items.First(e => e.Reason == "AmountMismatch");
        Assert.Equal(HttpStatusCode.Forbidden, (await world.SalesA.Client.GetAsync("/api/payments/vietqr/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.SalesA.Client.PostAsJsonAsync($"/api/payments/vietqr/events/{open.Id}/resolve", new ResolveVietQrEventRequest { Note = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Manager.PostAsJsonAsync($"/api/payments/vietqr/events/{open.Id}/resolve", new ResolveVietQrEventRequest { Note = " " })).StatusCode);
        using var resolved = await world.Manager.PostAsJsonAsync($"/api/payments/vietqr/events/{open.Id}/resolve", new ResolveVietQrEventRequest { Note = "Hoan lai cho khach" });
        var dto = (await resolved.Content.ReadFromJsonAsync<VietQrBankEventDto>())!;
        Assert.Equal(("Resolved", world.ManagerUserId), (dto.Status, dto.ResolvedByUserId));
        using var second = await world.Manager.PostAsJsonAsync($"/api/payments/vietqr/events/{open.Id}/resolve", new ResolveVietQrEventRequest { Note = "other" });
        Assert.Equal("Hoan lai cho khach", (await second.Content.ReadFromJsonAsync<VietQrBankEventDto>())!.ResolutionNote);
    }

    // ----------------------------------------------------- expiry and cancel

    [PostgresFact]
    public async Task VietQr_MoneyAfterTheDeadline_ExpiresTheRequestReleasesGoodsAndStoresTheMoney()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        await MakeOverdueAsync(world, qr.Id);
        using var bank = world.Api.Factory.NewClient();

        using var late = await SendWebhookAsync(bank, Notice("FT9", Total, qr.TransactionCode));

        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        using var json = JsonDocument.Parse(await late.Content.ReadAsStringAsync());
        Assert.Equal(("Recorded", "LateAfterExpiry"), (json.RootElement.GetProperty("result").GetString(), json.RootElement.GetProperty("reason").GetString()));
        await using var db = world.Database.CreateContext();
        Assert.Equal(VietQrStatus.Expired, (await db.VietQrTransactions.FindAsync(qr.Id))!.Status);
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.FindAsync(qr.OrderId))!.Status);
        Assert.Equal(SerialImeiStatus.InStock, (await db.SerialImeis.FindAsync(world.DeviceIds[0]))!.Status);
        Assert.Equal(0m, (await db.PosSessions.SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        var kept = await db.VietQrBankEvents.AsNoTracking().SingleAsync();
        Assert.Equal((VietQrEventReason.LateAfterExpiry, VietQrEventStatus.NeedsReconciliation, qr.Id), (kept.Reason, kept.Status, kept.VietQrTransactionId));
    }

    [PostgresFact]
    public async Task VietQr_ReadingAnOverdueRequest_ExpiresItAndTheWorkerReleasesEveryOtherOneOnce()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var read = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        var swept = await CreateQrAsync(world, shift.Id, world.DeviceIds[1]);
        await MakeOverdueAsync(world, read.Id);
        await MakeOverdueAsync(world, swept.Id);

        using var status = await world.SalesA.Client.GetAsync($"/api/pos/vietqr/{read.Id}");
        Assert.Equal("Expired", (await status.Content.ReadFromJsonAsync<VietQrPaymentDto>())!.Status);

        var worker = new VietQrExpiryWorker(world.Api.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new VietQrOptions()), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VietQrExpiryWorker>.Instance);
        Assert.Equal(1, await worker.SweepAsync(CancellationToken.None));
        Assert.Equal(0, await worker.SweepAsync(CancellationToken.None));

        await using var db = world.Database.CreateContext();
        Assert.All(await db.VietQrTransactions.AsNoTracking().Where(q => q.PosSessionId == shift.Id).ToListAsync(), q => Assert.Equal(VietQrStatus.Expired, q.Status));
        Assert.Equal(2, await db.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.InStock));
        var stock = await db.InventoryStocks.AsNoTracking().SingleAsync(s => s.ProductId == world.ProductId);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(2, await db.Set<InventoryOperation>().CountAsync(o => o.Kind == "Release"));
    }

    [PostgresFact]
    public async Task VietQr_CancelWithAVoucher_ReturnsTheUseExactlyOnce()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var voucher = await CreateVoucherAsync(world, "CANCEL1", usageLimit: 1);
        var request = QrRequest(world, shift.Id, world.DeviceIds[0]);
        request.VoucherCode = voucher.Code;
        using var created = await world.SalesA.Client.PostAsJsonAsync("/api/pos/vietqr", request);
        var qr = (await created.Content.ReadFromJsonAsync<VietQrPaymentDto>())!;
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await using (var held = world.Database.CreateContext())
            Assert.Equal((0, 1), await UsesAsync(held, voucher.Id));

        Assert.Equal(HttpStatusCode.OK, (await world.SalesA.Client.PostAsync($"/api/pos/vietqr/{qr.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.SalesA.Client.PostAsync($"/api/pos/vietqr/{qr.Id}/cancel", null)).StatusCode);

        await using var db = world.Database.CreateContext();
        Assert.Equal((0, 0), await UsesAsync(db, voucher.Id));
        Assert.Equal(VietQrStatus.Failed, (await db.VietQrTransactions.FindAsync(qr.Id))!.Status);
        Assert.Equal(SerialImeiStatus.InStock, (await db.SerialImeis.FindAsync(world.DeviceIds[0]))!.Status);
    }

    // ----------------------------------------------------------------- vouchers

    [PostgresFact]
    public async Task Voucher_LastUseTakenByTwoQrRequestsAtOnce_OnlyOneWins()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shiftA = await world.OpenShiftAsync(world.SalesA, 0m);
        var shiftB = await world.OpenShiftAsync(world.SalesB, 0m);
        var voucher = await CreateVoucherAsync(world, "LAST1", usageLimit: 1);
        var gate = new TaskCompletionSource();

        async Task<HttpStatusCode> TryAsync(PosWorld.Seller seller, Guid shiftId, Guid deviceId)
        {
            var request = QrRequest(world, shiftId, deviceId);
            request.VoucherCode = voucher.Code;
            await gate.Task;
            using var response = await seller.Client.PostAsJsonAsync("/api/pos/vietqr", request);
            return response.StatusCode;
        }

        var tasks = new[] { TryAsync(world.SalesA, shiftA.Id, world.DeviceIds[0]), TryAsync(world.SalesB, shiftB.Id, world.DeviceIds[1]) };
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses.Order().ToArray());
        await using var db = world.Database.CreateContext();
        Assert.Equal((0, 1), await UsesAsync(db, voucher.Id));
        Assert.Equal(1, await db.VietQrTransactions.CountAsync(q => q.Status == VietQrStatus.Pending));
        Assert.Equal(1, await db.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.Reserved));
    }

    [PostgresFact]
    public async Task Voucher_PaidByQrOrByCash_IsConsumedAndNeverReturned()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qrVoucher = await CreateVoucherAsync(world, "QRPAID", usageLimit: 1);
        var cashVoucher = await CreateVoucherAsync(world, "CASHPAID", usageLimit: 1);

        var request = QrRequest(world, shift.Id, world.DeviceIds[0]);
        request.VoucherCode = qrVoucher.Code;
        using var created = await world.SalesA.Client.PostAsJsonAsync("/api/pos/vietqr", request);
        var qr = (await created.Content.ReadFromJsonAsync<VietQrPaymentDto>())!;
        using var confirm = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm",
            new ConfirmVietQrRequest { ReferenceCode = "FTV1", ReceivedAmount = qr.Amount });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var cash = Checkout(world, shift.Id, world.DeviceIds[1], 20_000_000m);
        cash.VoucherCode = cashVoucher.Code;
        using var sale = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", cash);
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
        using var cashAgain = await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", cash);
        Assert.Equal(HttpStatusCode.OK, cashAgain.StatusCode);

        await using var db = world.Database.CreateContext();
        Assert.Equal((1, 0), await UsesAsync(db, qrVoucher.Id));
        Assert.Equal((1, 0), await UsesAsync(db, cashVoucher.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await world.SalesA.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], 20_000_000m, voucher: qrVoucher.Code))).StatusCode);
    }

    [PostgresFact]
    public async Task Voucher_TwoCashSalesTakeTheLastUseAtOnce_OnlyOneCompletes()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shiftA = await world.OpenShiftAsync(world.SalesA, 0m);
        var shiftB = await world.OpenShiftAsync(world.SalesB, 0m);
        var voucher = await CreateVoucherAsync(world, "CASHRACE", usageLimit: 1);
        var gate = new TaskCompletionSource();

        async Task<HttpStatusCode> SellAsync(PosWorld.Seller seller, Guid shiftId, Guid deviceId)
        {
            await gate.Task;
            using var response = await seller.Client.PostAsJsonAsync("/api/pos/checkout", Checkout(world, shiftId, deviceId, 20_000_000m, voucher: voucher.Code));
            return response.StatusCode;
        }

        var tasks = new[] { SellAsync(world.SalesA, shiftA.Id, world.DeviceIds[0]), SellAsync(world.SalesB, shiftB.Id, world.DeviceIds[1]) };
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses.Order().ToArray());
        await using var db = world.Database.CreateContext();
        Assert.Equal((1, 0), await UsesAsync(db, voucher.Id));
        Assert.Equal(1, await world.TestOrders(db).CountAsync());
        Assert.Equal(1, await db.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.Sold));
        Assert.Equal(1, await db.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.InStock));
    }

    // ----------------------------------------------------------------- races

    [PostgresFact]
    public async Task VietQr_ConfirmAgainstCancelAtOnce_ExactlyOneBranchWins()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        var gate = new TaskCompletionSource();

        var confirm = Task.Run(async () =>
        {
            await gate.Task;
            using var response = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm", new ConfirmVietQrRequest { ReferenceCode = "FTR1", ReceivedAmount = Total });
            return response.StatusCode;
        });
        var cancel = Task.Run(async () =>
        {
            await gate.Task;
            using var response = await world.SalesA.Client.PostAsync($"/api/pos/vietqr/{qr.Id}/cancel", null);
            return response.StatusCode;
        });
        gate.SetResult();
        var (confirmed, cancelled) = (await confirm, await cancel);

        await using var db = world.Database.CreateContext();
        var saved = await db.VietQrTransactions.AsNoTracking().SingleAsync(q => q.Id == qr.Id);
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == qr.OrderId);
        var shiftRow = await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        var device = (await db.SerialImeis.FindAsync(world.DeviceIds[0]))!;
        if (saved.Status == VietQrStatus.Confirmed)
        {
            Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict), (confirmed, cancelled));
            Assert.Equal((OrderStatus.Completed, SerialImeiStatus.Sold, Total), (order.Status, device.Status, shiftRow.VietQrSalesTotal));
            Assert.Empty(await db.VietQrBankEvents.ToListAsync());
        }
        else
        {
            Assert.Equal((VietQrStatus.Failed, HttpStatusCode.OK, HttpStatusCode.Conflict), (saved.Status, cancelled, confirmed));
            Assert.Equal((OrderStatus.Cancelled, SerialImeiStatus.InStock, 0m), (order.Status, device.Status, shiftRow.VietQrSalesTotal));
            Assert.Equal(VietQrEventReason.LateAfterCancel, (await db.VietQrBankEvents.SingleAsync()).Reason);
        }
    }

    [PostgresFact]
    public async Task VietQr_PendingRequestBlocksClosingTheShiftUntilItEnds()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 1_000_000m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        var close = new CloseShiftRequest { ClosingBalance = 1_000_000m };

        using var blocked = await world.SalesA.Client.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", close);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains(qr.OrderCode, await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await world.Manager.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 1_000_000m, Note = "x" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm", new ConfirmVietQrRequest { ReferenceCode = "FTC1", ReceivedAmount = Total })).StatusCode);
        using var closed = await world.SalesA.Client.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", close);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var dto = (await closed.Content.ReadFromJsonAsync<PosShiftDto>())!;
        Assert.Equal((1_000_000m, 0m, Total), (dto.ExpectedClosingBalance, dto.Discrepancy, dto.VietQrSalesTotal));

        using var late = await SendWebhookAsync(world.Api.Factory.NewClient(), Notice("FTLATE", Total, qr.TransactionCode));
        using var json = JsonDocument.Parse(await late.Content.ReadAsStringAsync());
        Assert.Equal("DuplicateReference", json.RootElement.GetProperty("reason").GetString());
        await using var db = world.Database.CreateContext();
        Assert.Equal(Total, (await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
    }

    [PostgresFact]
    public async Task VietQr_CreateAgainstCloseAtOnce_NeverLeavesAPendingRequestOnAClosedShift()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var gate = new TaskCompletionSource();

        var create = Task.Run(async () =>
        {
            await gate.Task;
            using var response = await world.SalesA.Client.PostAsJsonAsync("/api/pos/vietqr", QrRequest(world, shift.Id, world.DeviceIds[0]));
            return response.StatusCode;
        });
        var close = Task.Run(async () =>
        {
            await gate.Task;
            using var response = await world.SalesA.Client.PostAsJsonAsync($"/api/pos/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingBalance = 0m });
            return response.StatusCode;
        });
        gate.SetResult();
        var (created, closed) = (await create, await close);

        await using var db = world.Database.CreateContext();
        var shiftRow = await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        var pending = await db.VietQrTransactions.CountAsync(q => q.PosSessionId == shift.Id && q.Status == VietQrStatus.Pending);
        if (shiftRow.Status == PosSessionStatus.Closed)
        {
            Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict, 0), (closed, created, pending));
        }
        else
        {
            Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Conflict, 1), (created, closed, pending));
        }
    }

    [PostgresFact]
    public async Task VietQr_TwoRequestsForTheLastDevice_OnlyOneIsCreated()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shiftA = await world.OpenShiftAsync(world.SalesA, 0m);
        var shiftB = await world.OpenShiftAsync(world.SalesB, 0m);
        var gate = new TaskCompletionSource();

        async Task<HttpStatusCode> TryAsync(PosWorld.Seller seller, Guid shiftId)
        {
            await gate.Task;
            using var response = await seller.Client.PostAsJsonAsync("/api/pos/vietqr", QrRequest(world, shiftId, world.DeviceIds[0]));
            return response.StatusCode;
        }

        var tasks = new[] { TryAsync(world.SalesA, shiftA.Id), TryAsync(world.SalesB, shiftB.Id) };
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses.Order().ToArray());
        await using var db = world.Database.CreateContext();
        Assert.Equal(1, await db.VietQrTransactions.CountAsync(q => q.PosSessionId == shiftA.Id || q.PosSessionId == shiftB.Id));
        Assert.Equal(1, await world.TestOrders(db).CountAsync(o => o.PaymentMethod == PaymentMethod.VietQr && o.Status == OrderStatus.Pending));
    }

    [PostgresFact]
    public async Task VietQr_SameWebhookSentTwiceAtOnce_SellsOnce()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        var body = Notice("FTPAR", Total, qr.TransactionCode);
        var gate = new TaskCompletionSource();

        async Task<string> SendAsync()
        {
            using var bank = world.Api.Factory.NewClient();
            await gate.Task;
            using var response = await SendWebhookAsync(bank, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("result").GetString()!;
        }

        var tasks = new[] { SendAsync(), SendAsync() };
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(["Applied", "Duplicate"], results.Order().ToArray());
        await using var db = world.Database.CreateContext();
        Assert.Equal(Total, (await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        Assert.Single(await db.VietQrBankEvents.ToListAsync());
        Assert.Equal(1, await db.InventoryMovements.CountAsync(m => m.MovementType == InventoryMovementType.Sale && m.InventoryStock.ProductId == world.ProductId));
    }

    [PostgresFact]
    public async Task VietQr_ConfirmationFailsInTheDatabase_RollsBackEverythingAndALaterRetrySucceeds()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var qr = await CreateQrAsync(world, shift.Id, world.DeviceIds[0]);
        await using var db = world.Database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_qr_confirm_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.status = 'Confirmed' THEN RAISE EXCEPTION 'QR fixture: confirmation interrupted'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER reject_qr_confirm BEFORE UPDATE ON vietqr_transactions FOR EACH ROW EXECUTE FUNCTION reject_qr_confirm_fixture();
            """);
        var confirm = new ConfirmVietQrRequest { ReferenceCode = "FTRB", ReceivedAmount = Total };

        using var failed = await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm", confirm);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == qr.OrderId)).Status);
        Assert.Equal(SerialImeiStatus.Reserved, (await db.SerialImeis.AsNoTracking().SingleAsync(s => s.Id == world.DeviceIds[0])).Status);
        Assert.Equal(0m, (await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        Assert.Equal(0, await db.InventoryMovements.CountAsync(m => m.MovementType == InventoryMovementType.Sale && m.InventoryStock.ProductId == world.ProductId));

        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_qr_confirm ON vietqr_transactions; DROP FUNCTION reject_qr_confirm_fixture();");
        Assert.Equal(HttpStatusCode.OK, (await world.Manager.PostAsJsonAsync($"/api/pos/vietqr/{qr.Id}/confirm", confirm)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal(Total, (await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).VietQrSalesTotal);
        Assert.Equal(1, await db.InventoryMovements.CountAsync(m => m.MovementType == InventoryMovementType.Sale && m.InventoryStock.ProductId == world.ProductId));
    }

    // --------------------------------------------------------------- helpers

    private static CreateVietQrRequest QrRequest(PosWorld world, Guid shiftId, Guid deviceId) => new()
    {
        PosSessionId = shiftId,
        CheckoutKey = Guid.NewGuid(),
        Items = [new CreateOrderItemRequest { ProductId = world.ProductId, SerialImeiId = deviceId }]
    };

    private static async Task<VietQrPaymentDto> CreateQrAsync(PosWorld world, Guid shiftId, Guid deviceId)
    {
        using var response = await world.SalesA.Client.PostAsJsonAsync("/api/pos/vietqr", QrRequest(world, shiftId, deviceId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VietQrPaymentDto>())!;
    }

    private static string Notice(string id, decimal amount, string content, string account = BankAccount) =>
        JsonSerializer.Serialize(new { transactionId = id, amount, transferContent = content, accountNumber = account, transferredAt = DateTime.UtcNow });

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client, string body, string? signature = null, string? stamp = null, bool tamper = false)
    {
        stamp ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/vietqr/webhook")
        {
            Content = new StringContent(tamper ? body + " " : body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Timestamp", stamp);
        request.Headers.Add("X-Signature", signature ?? VietQrWebhookSignature.Sign(VietQrSecret, stamp, body));
        return await client.SendAsync(request);
    }

    // The request is created for 15 minutes, so tests move its clock into the past instead of waiting.
    private static async Task MakeOverdueAsync(PosWorld world, Guid qrId)
    {
        await using var db = world.Database.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vietqr_transactions SET created_at = now() - interval '30 minutes', expires_at = now() - interval '1 minute' WHERE id = {qrId}");
    }

    private static async Task<VoucherDto> CreateVoucherAsync(PosWorld world, string code, int usageLimit)
    {
        using var response = await world.Manager.PostAsJsonAsync("/api/vouchers", new CreateVoucherRequest
        {
            Code = code, DiscountType = "FixedAmount", DiscountValue = 1_000_000m, MinOrderAmount = 0m,
            UsageLimit = usageLimit, ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VoucherDto>())!;
    }

    private static async Task<(int Used, int Reserved)> UsesAsync(TechStore.Api.Common.Data.AppDbContext db, Guid voucherId)
    {
        var voucher = await db.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucherId);
        return (voucher.UsedCount, voucher.ReservedCount);
    }
}
