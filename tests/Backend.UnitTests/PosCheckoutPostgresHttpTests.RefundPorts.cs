using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task ReturnCash_TestPortsRequireActorShiftShareTransactionAndReceiveRetainedNetExactlyOnce()
    {
        // Arrange: only the POS payout and loyalty contracts use test adapters.
        await using var world = await PosWorld.CreateAsync(2);
        var saleShift = await world.OpenShiftAsync(world.SalesA, 0m);
        await using var db = world.Database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE refund_test_journal(
              kind text NOT NULL, refund_id uuid NOT NULL, source_id uuid NOT NULL, owner_id uuid NOT NULL,
              amount numeric(18,2) NOT NULL, PRIMARY KEY(kind,refund_id),
              CONSTRAINT fail_first_loyalty CHECK(kind <> 'Loyalty'));
            """);
        await using var host = world.Api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICashRefundService, TestRefundPorts>();
            services.AddScoped<IRefundLoyaltyService, TestRefundPorts>();
        }));
        using var manager = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        manager.DefaultRequestHeaders.Authorization = world.Manager.DefaultRequestHeaders.Authorization;
        var customer = await db.Customers.Select(c => c.Id).FirstAsync();
        var checkout = Checkout(world, saleShift.Id, world.DeviceIds[0], Total * 2);
        checkout.CustomerId = customer;
        checkout.Items!.Add(new() { ProductId = world.ProductId, SerialImeiId = world.DeviceIds[1], Quantity = 1 });
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        var claims = sale.Items.Select(i => new CreateReturnRequest(Guid.NewGuid(), sale.Id, "Cash refund",
            [new(i.Id, 1, i.SerialImeiId)])).ToArray();
        var first = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", claims[0]);
        var firstPath = $"/api/returns/{first.Id}/settle";
        var cash = new SettleReturnRequest("Cash");

        // Act and assert: the original cashier's open shift cannot fund the manager's payout.
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(manager, firstPath, cash));
        var refundShift = new PosSession { CashierUserId = world.ManagerUserId, OpeningBalance = 2 * Total };
        db.PosSessions.Add(refundShift);
        await db.SaveChangesAsync();

        // A failure in the last port rolls back POS, inventory, case and order.
        Assert.Equal(HttpStatusCode.InternalServerError, await PostStatus(manager, firstPath, cash));
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM refund_test_journal").SingleAsync());
        Assert.False(await db.OrderReturns.AnyAsync(r => r.OrderId == sale.Id));
        Assert.Equal(OrderStatus.Completed, await db.Orders.Where(o => o.Id == sale.Id).Select(o => o.Status).SingleAsync());
        Assert.Equal(2, await db.SerialImeis.CountAsync(s => s.ProductId == world.ProductId && s.Status == SerialImeiStatus.Sold));
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE refund_test_journal DROP CONSTRAINT fail_first_loyalty");

        // Act and assert: the repaired dependency allows the same ID to settle and replay.
        var paid = await PostDto<ReturnDto>(manager, firstPath, cash);
        Assert.Equal("Refunded", paid.Status);
        Assert.Equal(paid.RefundedAt, (await PostDto<ReturnDto>(manager, firstPath, cash)).RefundedAt);
        Assert.Equal(OrderStatus.PartiallyRefunded, await db.Orders.Where(o => o.Id == sale.Id).Select(o => o.Status).SingleAsync());
        Assert.Equal(Price, await db.Database.SqlQuery<decimal>($"""
            SELECT amount AS "Value" FROM refund_test_journal
            WHERE kind='Loyalty' AND refund_id={first.Id} AND source_id={sale.Id} AND owner_id={customer}
            """).SingleAsync());
        Assert.Equal(Total, await db.Database.SqlQuery<decimal>($"""
            SELECT amount AS "Value" FROM refund_test_journal
            WHERE kind='Cash' AND refund_id={first.Id} AND source_id={refundShift.Id} AND owner_id={world.ManagerUserId}
            """).SingleAsync());
        var second = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", claims[1]);
        await PostDto<ReturnDto>(manager, $"/api/returns/{second.Id}/settle", cash);
        Assert.Equal(0m, await db.Database.SqlQuery<decimal>($"""
            SELECT amount AS "Value" FROM refund_test_journal WHERE kind='Loyalty' AND refund_id={second.Id}
            """).SingleAsync());
        Assert.Equal(4, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM refund_test_journal").SingleAsync());
        var payouts = await db.OrderReturns.Where(r => r.OrderId == sale.Id).ToListAsync();
        Assert.Equal(2, payouts.Count);
        Assert.All(payouts, r => Assert.Equal(refundShift.Id, r.RefundPosSessionId));
        Assert.All(payouts, r => Assert.Equal(world.ManagerUserId, r.ProcessedByUserId));
        Assert.Equal(2 * Total, payouts.Sum(r => r.RefundAmount));
        Assert.Equal(OrderStatus.Refunded, await db.Orders.Where(o => o.Id == sale.Id).Select(o => o.Status).SingleAsync());
    }

    private sealed class TestRefundPorts(AppDbContext db) : ICashRefundService, IRefundLoyaltyService
    {
        public async Task<Guid?> LockOpenShiftAsync(Guid actorId, CancellationToken ct) =>
            (await db.PosSessions.FromSqlInterpolated($"""
                SELECT * FROM pos_sessions WHERE cashier_user_id={actorId} AND status='Open' FOR UPDATE
                """).SingleOrDefaultAsync(ct))?.Id;

        public Task StagePayoutAsync(Guid refundId, Guid shiftId, Guid actorId, decimal amount, CancellationToken ct) =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO refund_test_journal(kind,refund_id,source_id,owner_id,amount)
                VALUES ('Cash',{refundId},{shiftId},{actorId},{amount})
                """, ct);

        public Task StageSaleAsync(Guid orderId, Guid customerId, decimal netAmount, CancellationToken ct) => Task.CompletedTask;

        public Task StageReversalAsync(Guid refundId, Guid orderId, Guid customerId, decimal remainingNetAmount, CancellationToken ct) =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO refund_test_journal(kind,refund_id,source_id,owner_id,amount)
                VALUES ('Loyalty',{refundId},{orderId},{customerId},{remainingNetAmount})
                """, ct);
    }
}
