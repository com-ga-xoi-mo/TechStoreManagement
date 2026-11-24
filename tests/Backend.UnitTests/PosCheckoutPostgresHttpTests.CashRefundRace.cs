using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task UndeliveredRequest_FulfilledBeforeApproval_RequiresPhysicalReturnAndFreshRequest()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var draft = await PostDto<OrderDto>(world.SalesA.Client, "/api/orders", new CreateOrderRequest
        {
            PaymentMethod = "Cash", Items = [new() { ProductId = world.ProductId, SerialImeiId = world.DeviceIds[0], Quantity = 1 }]
        });
        foreach (var target in new[] { "Processing", "Completed" })
        {
            using var response = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{draft.Id}/status",
                new UpdateOrderStatusRequest { TargetStatus = target });
            response.EnsureSuccessStatusCode();
            if (target == "Processing")
                await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(draft, 1));
        }
        var history = await world.Manager.GetFromJsonAsync<OrderReturnsDto>($"/api/returns/order/{draft.Id}");
        var original = Assert.Single(history!.History);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, $"/api/returns/{original.Id}/settle",
            new SettleReturnRequest("VietQr")));
        var fresh = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(draft, 1));
        await world.OpenShiftAsync(new PosWorld.Seller(world.Manager, world.ManagerUserId), Total - 1);
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, $"/api/returns/{fresh.Id}/settle",
            new SettleReturnRequest("Cash")));
        await using var db = world.Database.CreateContext();
        Assert.False(await db.OrderReturns.AnyAsync(r => r.OrderId == draft.Id));
        Assert.False(await db.Set<CashRefundPayout>().AnyAsync(p => p.RefundId == fresh.Id));
    }

    [PostgresFact]
    public async Task RefundWhileClosing_CashDrawerEitherIncludesOnePayoutOrRejectsIt()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[0], Total));
        var request = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(sale, 1));
        var drawer = await world.OpenShiftAsync(new PosWorld.Seller(world.Manager, world.ManagerUserId), Total);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpStatusCode> PayAsync()
        {
            await gate.Task;
            return await PostStatus(world.Manager, $"/api/returns/{request.Id}/settle", new SettleReturnRequest("Cash"));
        }
        async Task<PosShiftDto> CloseAsync()
        {
            await gate.Task;
            return await PostDto<PosShiftDto>(world.Manager, $"/api/pos/shifts/{drawer.Id}/close",
                new CloseShiftRequest { ClosingBalance = Total, Note = "Đối chiếu hai thao tác đồng thời." });
        }
        var pay = PayAsync();
        var close = CloseAsync();
        gate.SetResult();
        await Task.WhenAll(pay, close);
        Assert.Contains(pay.Result, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using var db = world.Database.CreateContext();
        var expectedCount = pay.Result == HttpStatusCode.OK ? 1 : 0;
        Assert.Equal(expectedCount, await db.Set<CashRefundPayout>().CountAsync(p => p.RefundId == request.Id));
        Assert.Equal(expectedCount, await db.OrderReturns.CountAsync(p => p.OrderId == sale.Id));
        Assert.Equal(Total * expectedCount, close.Result.CashRefundTotal);
        Assert.Equal(Total - Total * expectedCount, close.Result.ExpectedClosingBalance);
    }
}
