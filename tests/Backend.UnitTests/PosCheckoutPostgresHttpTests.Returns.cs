using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task ReturnRequest_EnforcesRolesOwnershipLineValidationAndPayloadReplay()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var request = ReturnFor(sale, 1);
        using var anonymous = world.Api.Factory.NewClient();

        // Act and assert
        Assert.Equal(HttpStatusCode.Unauthorized, await PostStatus(anonymous, "/api/returns", request));
        Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.Warehouse, "/api/returns", request));
        Assert.Equal(HttpStatusCode.NotFound, await PostStatus(world.SalesB.Client, "/api/returns", request));
        foreach (var quantity in new[] { -1, 0, 2 })
        {
            var invalid = request with { Lines = [request.Lines[0] with { Quantity = quantity }] };
            await AssertInvalidField(world.SalesA.Client, invalid, "lines[0].quantity");
        }
        await AssertInvalidField(world.SalesA.Client, request with { Lines = [request.Lines[0] with { SerialImeiId = Guid.NewGuid() }] }, "lines[0].serialImeiId");
        await AssertInvalidField(world.SalesA.Client, request with { Reason = "  " }, "reason");
        var created = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", request);
        Assert.Equal("Pending", created.Status);
        Assert.Equal(request.RequestId, (await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", request)).Id);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.SalesA.Client, "/api/returns", request with { Reason = "Other reason" }));
        var path = $"/api/returns/{request.RequestId}";
        using var hidden = await world.SalesB.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var hiddenOrder = await world.SalesB.Client.GetAsync($"/api/returns/order/{sale.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenOrder.StatusCode);
        using var forbidden = await world.Warehouse.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var history = await world.Manager.GetFromJsonAsync<OrderReturnsDto>($"/api/returns/order/{sale.Id}");
        Assert.Equal(created.Id, Assert.Single(history!.History).Id);
        Assert.Equal(Total, Assert.Single(history.Balances).RemainingAmount);
        foreach (var (suffix, body) in new (string, object)[]
        {
            ("/settle", new SettleReturnRequest("Cash")),
            ("/reconcile", new ReconcileReturnRequest("denied", 0, false)),
            ("/exchange", new LinkExchangeRequest(sale.Id))
        })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await PostStatus(anonymous, path + suffix, body));
            Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.SalesA.Client, path + suffix, body));
            Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.Warehouse, path + suffix, body));
        }
        using var unauthorizedQueue = await anonymous.GetAsync("/api/inventory/returns");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedQueue.StatusCode);
        foreach (var actor in new[] { world.Manager, world.SalesA.Client })
        {
            using var deniedQueue = await actor.GetAsync("/api/inventory/returns");
            Assert.Equal(HttpStatusCode.Forbidden, deniedQueue.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(actor, $"/api/inventory/returns/{Guid.NewGuid()}/inspections",
                new InspectReturnRequest(Guid.NewGuid(), 1, "InStock")));
        }
        await using var db = world.Database.CreateContext();
        Assert.Empty(await db.OrderReturns.Where(r => r.OrderId == sale.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task ReturnBank_OverlappingClaimsFailureReleaseDbRollbackAndReplay_RefundsOnceAndLinksReplacement()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(2);
        using var admin = world.Api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var claims = new[] { ReturnFor(sale, 1), ReturnFor(sale, 1) };
        foreach (var claim in claims) await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", claim);
        var actors = new[] { world.Manager, admin };
        string PathFor(int index) => $"/api/returns/{claims[index].RequestId}";
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act: independent requests acquire their payout hold under the order lock.
        var races = Enumerable.Range(0, 2).Select(async i =>
        {
            await gate.Task;
            return await PostStatus(actors[i], PathFor(i) + "/settle", new SettleReturnRequest("VietQr"));
        }).ToArray();
        gate.SetResult();
        var result = await Task.WhenAll(races);

        // Assert: a definitive bank failure releases the winning hold.
        Assert.Single(result, s => (int)s is >= 200 and < 300);
        var loser = Array.IndexOf(result, HttpStatusCode.Conflict);
        Assert.InRange(loser, 0, 1);
        var winner = 1 - loser;
        var failure = new ReconcileReturnRequest("r1-declined", 0m, false);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(actors[winner], PathFor(winner) + "/reconcile", failure with { Amount = Total }));
        Assert.Equal("Failed", (await PostDto<ReturnDto>(actors[winner], PathFor(winner) + "/reconcile", failure)).Status);
        var basePath = PathFor(loser);
        Assert.Equal("AwaitingBank", (await PostDto<ReturnDto>(actors[loser], basePath + "/settle", new SettleReturnRequest("VietQr"))).Status);
        var unavailable = await world.Manager.GetFromJsonAsync<OrderReturnsDto>($"/api/returns/order/{sale.Id}");
        Assert.False(string.IsNullOrWhiteSpace(unavailable!.ReviewReason));
        var heldUnit = Assert.Single(unavailable.Balances);
        Assert.Equal((0, 0m), (heldUnit.RemainingQuantity, heldUnit.RemainingAmount));
        var paid = new ReconcileReturnRequest("r1-paid", Total, true, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.BadRequest, await PostStatus(actors[loser], basePath + "/reconcile", paid with { PaidAt = null }));
        Assert.Equal(HttpStatusCode.BadRequest, await PostStatus(actors[loser], basePath + "/reconcile", paid with { PaidAt = DateTimeOffset.UtcNow.AddDays(1) }));
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(actors[loser], basePath + "/reconcile", paid with { Amount = Total - .01m }));

        // Arrange a local commit failure after the operator has independently verified the transfer.
        await using var db = world.Database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_refund() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test-only simulated failure'; END; $$;
            CREATE TRIGGER fail_test_refund BEFORE INSERT ON order_returns FOR EACH ROW EXECUTE FUNCTION reject_test_refund();
            """);
        Assert.Equal(HttpStatusCode.InternalServerError, await PostStatus(world.Manager, basePath + "/reconcile", paid));
        Assert.Equal("AwaitingBank", (await world.Manager.GetFromJsonAsync<ReturnDto>(basePath))!.Status);
        Assert.Equal(SerialImeiStatus.Sold, await db.SerialImeis.Where(s => s.Id == world.DeviceIds[0]).Select(s => s.Status).SingleAsync());
        Assert.False(await db.OrderReturns.AnyAsync(r => r.OrderId == sale.Id));
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_test_refund ON order_returns; DROP FUNCTION reject_test_refund();");

        // Act and assert: replaying the original reference is sufficient after rollback or a lost response.
        var confirmed = await Task.WhenAll(
            PostDto<ReturnDto>(admin, basePath + "/reconcile", paid),
            PostDto<ReturnDto>(world.Manager, basePath + "/reconcile", paid));
        Assert.All(confirmed, x => Assert.Equal("Refunded", x.Status));
        Assert.Equal(confirmed[0].RefundedAt, confirmed[1].RefundedAt);
        var bankTime = paid.PaidAt!.Value.UtcDateTime;
        Assert.Equal(bankTime.AddTicks(-(bankTime.Ticks % 10)), confirmed[0].RefundedAt);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(admin, basePath + "/reconcile", paid with { BankReference = "different" }));
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(admin, basePath + "/reconcile", paid with { PaidAt = DateTimeOffset.UtcNow }));
        var settled = await db.OrderReturns.SingleAsync(r => r.OrderId == sale.Id);
        Assert.Equal(Total, settled.RefundAmount);
        Assert.Null(settled.RefundPosSessionId);
        var savedSale = await db.Orders.SingleAsync(o => o.Id == sale.Id);
        Assert.Equal(OrderStatus.Refunded, savedSale.Status);
        Assert.Equal(sale.PaidAt, savedSale.PaidAt);
        Assert.Equal(sale.CompletedAt, savedSale.CompletedAt);
        Assert.Equal(SerialImeiStatus.Returned, await db.SerialImeis.Where(s => s.Id == world.DeviceIds[0]).Select(s => s.Status).SingleAsync());

        // Act and assert: checkout fails, then succeeds with the original refund key.
        var exchange = Checkout(world, shift.Id, world.DeviceIds[0], Total);
        exchange.CheckoutKey = claims[loser].RequestId;
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.SalesA.Client, "/api/pos/checkout", exchange));
        Assert.Null((await world.Manager.GetFromJsonAsync<ReturnDto>(basePath))!.ReplacementOrderId);
        exchange.Items![0].SerialImeiId = world.DeviceIds[1];
        var replacement = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", exchange);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, basePath + "/exchange", new LinkExchangeRequest(sale.Id)));
        Assert.Equal(replacement.Id, (await world.Manager.GetFromJsonAsync<ReturnDto>(basePath))!.ReplacementOrderId);
        var link = new LinkExchangeRequest(replacement.Id);
        Assert.Equal(replacement.Id, (await PostDto<ReturnDto>(world.Manager, basePath + "/exchange", link)).ReplacementOrderId);
        Assert.Equal(replacement.Id, (await PostDto<ReturnDto>(world.Manager, basePath + "/exchange", link)).ReplacementOrderId);
        Assert.Equal(1, await db.OrderReturns.CountAsync(r => r.OrderId == sale.Id));
    }

    [PostgresFact]
    public async Task Reconcile_LegacyPendingCaseRequiresVerifiedApprovalAndKeepsOriginalReceiptOnRetry()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var claim = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(sale, 1));
        var path = $"/api/returns/{claim.Id}";
        var held = await PostDto<ReturnDto>(world.Manager, path + "/settle", new SettleReturnRequest("VietQr"));
        var approvedAt = Assert.IsType<DateTime>(held.BankApprovedAt);
        await using var db = world.Database.CreateContext();
        await db.GetService<IMigrator>().MigrateAsync("20261124140008_TrackWarrantyCases");
        await db.Database.MigrateAsync();

        var original = await world.Manager.GetFromJsonAsync<ReturnDto>(path);
        Assert.Equal("AwaitingBank", original!.Status);
        Assert.Null(original.BankApprovedAt);
        var proof = new ReconcileReturnRequest("legacy-paid", Total, true, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.BadRequest, await PostStatus(world.Manager, path + "/reconcile", proof));
        Assert.Equal(HttpStatusCode.BadRequest, await PostStatus(world.Manager, path + "/reconcile",
            proof with { VerifiedLegacyApprovalAt = claim.CreatedAt.AddDays(-1) }));
        var confirmed = await PostDto<ReturnDto>(world.Manager, path + "/reconcile",
            proof with { VerifiedLegacyApprovalAt = approvedAt });
        Assert.Equal(approvedAt, confirmed.BankApprovedAt);
        var replay = await PostDto<ReturnDto>(world.Manager, path + "/reconcile", proof);
        Assert.Equal(confirmed.RefundedAt, replay.RefundedAt);
        Assert.Single(await db.OrderReturns.Where(row => row.OrderId == sale.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task ReturnInspection_PartialDispositionsAndReplay_ReconcileAmountsAndIncrementOnlyResalableUnits()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        await using var db = world.Database.CreateContext();
        var stock = await AddQuantityStock(world, db, 4);
        var checkout = new PosCheckoutRequest
        {
            PosSessionId = shift.Id, CheckoutKey = Guid.NewGuid(), CashTendered = 4_000_000m, OrderDiscountAmount = 400_000m,
            Items = [new() { ProductId = stock.ProductId, Quantity = 4 }]
        };
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        var savedLine = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == sale.Id);
        Assert.Null(savedLine.UnitCost);
        var request = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(sale, 3));
        var path = $"/api/returns/{request.Id}";
        // The request was filed two days before approval. Neither its creation time nor
        // a client-supplied legacy timestamp may authorize an earlier transfer.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE return_cases SET created_at = created_at - interval '2 days' WHERE id = {request.Id}");
        var approval = await PostDto<ReturnDto>(world.Manager, path + "/settle", new SettleReturnRequest("VietQr"));
        var approvedAt = Assert.IsType<DateTime>(approval.BankApprovedAt);
        var replay = await PostDto<ReturnDto>(world.Manager, path + "/settle", new SettleReturnRequest("VietQr"));
        Assert.Equal(approvedAt, replay.BankApprovedAt);
        var proof = new ReconcileReturnRequest("r2-paid", 2_970_000m, true, approvedAt);
        foreach (var earlier in new[] { approvedAt.AddDays(-1), approvedAt.AddTicks(-10) })
            Assert.Equal(HttpStatusCode.BadRequest, await PostStatus(world.Manager, path + "/reconcile",
                proof with { PaidAt = earlier, VerifiedLegacyApprovalAt = earlier }));
        Assert.Empty(await db.OrderReturns.Where(r => r.OrderId == sale.Id).ToListAsync());
        var paid = await PostDto<ReturnDto>(world.Manager, path + "/reconcile", proof);
        Assert.Equal(approvedAt, paid.RefundedAt);

        // Act and assert
        var remaining = await world.Manager.GetFromJsonAsync<OrderReturnsDto>($"/api/returns/order/{sale.Id}");
        Assert.Equal("PartiallyRefunded", remaining!.OrderStatus);
        Assert.Equal(990_000m, remaining.Balances.Single().RemainingAmount);
        Assert.Equal(1, remaining.Balances.Single().RemainingQuantity);
        using var queue = await world.Warehouse.GetAsync("/api/inventory/returns");
        var raw = await queue.Content.ReadAsStringAsync();
        foreach (var field in new[] { "netAmount", "discountAmount", "taxAmount", "refundAmount", "unitCost", "refundPosSessionId" })
            Assert.DoesNotContain(field, raw, StringComparison.OrdinalIgnoreCase);
        var row = Assert.Single((await queue.Content.ReadFromJsonAsync<List<ReturnInspectionDto>>())!, x => x.RequestId == request.Id);
        Assert.Equal(3, row.Pending);
        Assert.Equal(savedLine.Sku, row.Sku);
        var inspect = $"/api/inventory/returns/{row.ReturnLineId}/inspections";
        var operation = new InspectReturnRequest(Guid.NewGuid(), 1, "InStock");
        Assert.Equal(HttpStatusCode.Forbidden, await PostStatus(world.SalesA.Client, inspect, operation));
        await PostDto<ReturnInspectionDto>(world.Warehouse, inspect, operation);
        await PostDto<ReturnInspectionDto>(world.Warehouse, inspect, operation);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Warehouse, inspect, operation with { Outcome = "Defective" }));
        await PostDto<ReturnInspectionDto>(world.Warehouse, inspect, new InspectReturnRequest(Guid.NewGuid(), 1, "Defective"));
        var last = await PostDto<ReturnInspectionDto>(world.Warehouse, inspect, new InspectReturnRequest(Guid.NewGuid(), 1, "UnderRepair"));
        Assert.Equal((0, 1, 1, 1), (last.Pending, last.Restocked, last.Defective, last.UnderRepair));
        var rows = await db.OrderReturns.AsNoTracking().Where(r => r.OrderId == sale.Id).ToListAsync();
        Assert.All(rows, r => Assert.Equal(paid.RefundedAt, r.RefundedAt));
        Assert.Equal(3, rows.Sum(r => r.Quantity));
        Assert.Equal(2_700_000m, rows.Sum(r => r.NetAmount));
        Assert.Equal(300_000m, rows.Sum(r => r.DiscountAmount));
        Assert.Equal(270_000m, rows.Sum(r => r.TaxAmount));
        var restocked = Assert.Single(rows, r => r.IsRestocked);
        var movement = await db.InventoryMovements.SingleAsync(m => m.InventoryStockId == stock.Id && m.MovementType == InventoryMovementType.CustomerReturn);
        Assert.Equal(restocked.Id, movement.OrderReturnId);
        Assert.Equal(1, movement.QuantityChange);
        Assert.Null(savedLine.UnitCost * movement.QuantityChange);
        Assert.Equal(1, await db.InventoryStocks.Where(s => s.Id == stock.Id).Select(s => s.Quantity).SingleAsync());
    }

    [PostgresFact]
    public async Task ReturnProcessing_BankHoldPreventsCompletionAndPayoutReleasesOnlyReservedUnits()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(2);
        var draft = await PostDto<OrderDto>(world.SalesA.Client, "/api/orders", new CreateOrderRequest
        {
            PaymentMethod = "Cash", Items = world.DeviceIds.Select(id => new CreateOrderItemRequest { ProductId = world.ProductId, SerialImeiId = id, Quantity = 1 }).ToList()
        });
        using var processing = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{draft.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Processing" });
        processing.EnsureSuccessStatusCode();
        var whole = new CreateReturnRequest(Guid.NewGuid(), draft.Id, "Cancel before delivery", draft.Items.Select(i => new ReturnLineRequest(i.Id, 1, i.SerialImeiId)).ToList());
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.SalesA.Client, "/api/returns", whole with { Lines = [whole.Lines[0]] }));
        await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", whole);
        var path = $"/api/returns/{whole.RequestId}";

        // Act
        await PostDto<ReturnDto>(world.Manager, path + "/settle", new SettleReturnRequest("VietQr"));
        using var blocked = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{draft.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Completed" });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var payout = Task.Run(async () =>
        {
            await gate.Task;
            return await PostDto<ReturnDto>(world.Manager, path + "/reconcile", new ReconcileReturnRequest("r3-paid", 2 * Total, true, DateTimeOffset.UtcNow));
        });
        var delivery = Task.Run(async () =>
        {
            await gate.Task;
            using var result = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{draft.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Completed" });
            return result.StatusCode;
        });
        gate.SetResult();
        Assert.Equal("Refunded", (await payout).Status);
        Assert.Equal(HttpStatusCode.Conflict, await delivery);

        // Assert
        await using var db = world.Database.CreateContext();
        var stock = await db.InventoryStocks.SingleAsync(s => s.ProductId == world.ProductId);
        Assert.Equal((2, 0), (stock.Quantity, stock.ReservedQuantity));
        Assert.Equal(2, await db.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.InStock));
        Assert.False(await db.InventoryMovements.AnyAsync(m => m.InventoryStockId == stock.Id && m.MovementType == InventoryMovementType.CustomerReturn));
        var saved = await db.Orders.SingleAsync(o => o.Id == draft.Id);
        Assert.Equal(OrderStatus.Refunded, saved.Status);
        Assert.Null(saved.CompletedAt);
        Assert.NotNull(saved.PaidAt);
        var queue = await world.Warehouse.GetFromJsonAsync<List<ReturnInspectionDto>>("/api/inventory/returns");
        Assert.DoesNotContain(queue!, x => x.RequestId == whole.RequestId);
    }

    private static async Task<InventoryStock> AddQuantityStock(PosWorld world, TechStore.Api.Common.Data.AppDbContext db, int quantity)
    {
        var product = new Product
        {
            CategoryId = await db.Products.Where(p => p.Id == world.ProductId).Select(p => p.CategoryId).SingleAsync(),
            Name = "Quantity fixture", Sku = "RETN-" + Guid.NewGuid().ToString("N"), Brand = "Fixture",
            BasePrice = 1_000_000m, CostPrice = 600_000m, IsSerialTracked = false
        };
        var stock = new InventoryStock { ProductId = product.Id, Quantity = quantity };
        db.Add(product);
        db.Add(stock);
        await db.SaveChangesAsync();
        return stock;
    }

    private static CreateReturnRequest ReturnFor(OrderDto sale, int quantity)
    {
        var item = sale.Items.Single();
        return new(Guid.NewGuid(), sale.Id, "Return for refund", [new(item.Id, quantity, item.SerialImeiId)]);
    }

    private static async Task AssertInvalidField(HttpClient actor, CreateReturnRequest request, string field)
    {
        using var response = await actor.PostAsJsonAsync("/api/returns", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"Missing field {field}");
    }

    private static async Task<T> PostDto<T>(HttpClient actor, string url, object request)
    {
        using var response = await actor.PostAsJsonAsync(url, request);
        Assert.True(response.IsSuccessStatusCode, $"{url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<HttpStatusCode> PostStatus(HttpClient actor, string url, object request)
    {
        using var response = await actor.PostAsJsonAsync(url, request);
        return response.StatusCode;
    }
}
