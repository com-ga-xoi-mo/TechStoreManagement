using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class FinancialReportLiveReconciliationPostgresHttpTests
{
    [PostgresFact]
    public async Task LastUnit_ConcurrentCheckouts_CommitExactlyOneSaleAndReceipt()
    {
        await using var scenario = await Scenario.StartAsync();
        var product = await CreateProductAsync(scenario);
        await PostAsync<PurchaseReceiptDto>(scenario.Warehouse, ReceiptPath, await ReceiptAsync(scenario, product.Id, 1));
        var stock = await StockAsync(scenario, product.Id);
        var customer = await CreateCustomerAsync(scenario);
        var otherUser = await IdentityPostgresApi.CreateUserAsync(scenario.Admin, "SalesStaff");
        using var other = NewClient(scenario.Host);
        await scenario.LoginAsync(other, otherUser.Username, "HttpEmployee@123", RoleType.SalesStaff);
        var firstShift = await OpenShiftAsync(scenario.Seller, 0);
        var secondShift = await OpenShiftAsync(other, 0);
        var first = Checkout(firstShift.Id, product.Id, customer: customer.Id);
        var second = Checkout(secondShift.Id, product.Id, customer: customer.Id);
        Assert.Equal(EmptyTotals, (await ReadReportAsync(scenario.Manager, Day1, Day1)).Summary);

        var race = await ObserveRaceAsync(scenario, $"SELECT id FROM inventory_stocks WHERE id={stock.Id} FOR UPDATE",
            "%inventory_stocks%", token => StatusAsync(scenario.Seller, CheckoutPath, first, token),
            token => StatusAsync(other, CheckoutPath, second, token));
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, race.Outcomes.Order());
        var winner = race.Outcomes[0] == HttpStatusCode.Created ? first : second;
        var loser = race.Outcomes[0] == HttpStatusCode.Created ? second : first;
        await using var db = scenario.Api.Database.CreateContext();
        var sale = await db.Orders.AsNoTracking().SingleAsync(row => row.CheckoutKey == winner.CheckoutKey);
        Assert.False(await db.Orders.AnyAsync(row => row.CheckoutKey == loser.CheckoutKey));
        var movement = await db.InventoryMovements.AsNoTracking().SingleAsync(row => row.InventoryStockId == stock.Id && row.MovementType == InventoryMovementType.Sale);
        Assert.Equal((-1, 0), (movement.QuantityChange, movement.QuantityAfter));
        Assert.Equal(0, (await StockAsync(scenario, product.Id)).Quantity);
        Assert.Equal(10, (await db.CustomerLoyaltyPoints.AsNoTracking().SingleAsync(row => row.CustomerId == customer.Id)).PointsChange);
        Assert.Equal(0, await DemoVoucherUsesAsync(scenario));
        var after = await ReadReportAsync(scenario.Manager, Day1, Day1);
        Assert.Equal((1L, 1L, 1_000_000m, 1_100_000m, 600_000m, 400_000m),
            (after.Summary.CompletedOrderCount, after.Summary.Quantity, after.Summary.SalesExcludingTax,
                after.Summary.PaymentReceivedAmount, after.Summary.CostOfGoodsSold, after.Summary.NetGrossProfit));
        Assert.Equal(winner.PosSessionId, Assert.Single(after.CashByShift).PosSessionId);
        Assert.Equal(winner.PosSessionId, sale.PosSessionId);
        Assert.Equal(1_100_000m, (await ReadAsync<PosShiftDto>(scenario.Manager, $"{ShiftPath}/{winner.PosSessionId}")).CashSalesTotal);
        Assert.Equal(0m, (await ReadAsync<PosShiftDto>(scenario.Manager, $"{ShiftPath}/{loser.PosSessionId}")).CashSalesTotal);
        Write("race-final-stock", new { Stock = stock.Id, Sale = sale.Id, Movement = movement.Id, race, after.Summary });
    }

    [PostgresFact]
    public Task SameImei_RefundQueuedFirst_CommitsTheRefundAndRejectsWarranty() => SameImeiRaceAsync(refundFirst: true);

    [PostgresFact]
    public Task SameImei_WarrantyQueuedFirst_CommitsTheIntakeAndRejectsRefund() => SameImeiRaceAsync(refundFirst: false);

    private async Task SameImeiRaceAsync(bool refundFirst)
    {
        await using var scenario = await Scenario.StartAsync();
        var product = await CreateProductAsync(scenario, tracked: true);
        var device = await ImportDeviceAsync(scenario, product.Id, 1);
        var sellerShift = await OpenShiftAsync(scenario.Seller, 0);
        var payingShift = await OpenShiftAsync(scenario.Manager, 2_000_000);
        var sale = await PostAsync<OrderDto>(scenario.Seller, CheckoutPath, Checkout(sellerShift.Id, product.Id, serial: device));
        var claim = await PostAsync<ReturnDto>(scenario.Seller, ReturnPath, ReturnFor(sale));
        var warrantyId = Guid.NewGuid();
        var before = await ReadReportAsync(scenario.Manager, Day1, Day1);

        Func<CancellationToken, Task<HttpStatusCode>> refund = token =>
            StatusAsync(scenario.Manager, $"{ReturnPath}/{claim.Id}/settle", new SettleReturnRequest("Cash"), token);
        Func<CancellationToken, Task<HttpStatusCode>> warranty = token =>
            StatusAsync(scenario.Admin, $"/api/warranties/{warrantyId}/receive", ReceiveRequest(sale, device), token);
        // The row lock queues contenders in arrival order, so the first waiter is the allowed winner.
        var race = await ObserveRaceAsync(scenario, $"SELECT id FROM orders WHERE id={sale.Id} FOR UPDATE", "%orders%",
            refundFirst ? refund : warranty, refundFirst ? warranty : refund);
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, race.Outcomes);
        await using var db = scenario.Api.Database.CreateContext();
        Assert.Equal(refundFirst ? 1 : 0, await db.OrderReturns.CountAsync(row => row.OrderId == sale.Id));
        Assert.Equal(refundFirst ? 1 : 0, await db.Set<CashRefundPayout>().CountAsync(row => row.RefundId == claim.Id));
        Assert.Equal(!refundFirst, await db.Set<WarrantyCase>().AnyAsync(row => row.Id == warrantyId));
        Assert.Equal(refundFirst ? SerialImeiStatus.Returned : SerialImeiStatus.UnderRepair,
            await db.SerialImeis.Where(row => row.Id == device).Select(row => row.Status).SingleAsync());
        Assert.Equal(0, (await StockAsync(scenario, product.Id)).Quantity);
        var report = await ReadReportAsync(scenario.Manager, Day1, Day1);
        var expected = refundFirst ? before.Summary with { RefundedSalesExcludingTax = 1_000_000,
            RefundPaidAmount = 1_100_000, CashRefundPaidAmount = 1_100_000, RefundTaxAmount = 100_000 } : before.Summary;
        Assert.Equal(expected, report.Summary);
        Assert.Equal(refundFirst ? 1_100_000m : 0m,
            (await ReadAsync<PosShiftDto>(scenario.Manager, $"{ShiftPath}/{payingShift.Id}")).CashRefundTotal);
        Write("race-same-imei", new { Order = sale.Id, Device = device, RefundCase = claim.Id, WarrantyCase = warrantyId,
            QueuedFirst = refundFirst ? "refund" : "warranty", race, report.Summary });
    }

    /// <summary>
    /// Holds the target row, queues the first contender behind it and then the second. The blocker is released
    /// only after PostgreSQL reports both contenders in open transactions waiting on locks at the same time.
    /// </summary>
    private static async Task<RaceEvidence> ObserveRaceAsync(Scenario scenario, FormattableString lockQuery, string relationPattern,
        Func<CancellationToken, Task<HttpStatusCode>> startFirst, Func<CancellationToken, Task<HttpStatusCode>> startSecond)
    {
        await using var blocker = scenario.Api.Database.CreateContext();
        await using var observer = scenario.Api.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync(lockQuery);
        var blockerPid = await blocker.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var first = startFirst(deadline.Token);
        Task<HttpStatusCode>? second = null;
        RaceParticipant[] participants = [];
        try
        {
            participants = await WaitForWaitersAsync(1, first);
            if (participants.Length == 1)
            {
                second = startSecond(deadline.Token);
                participants = await WaitForWaitersAsync(2, first, second);
            }
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        var firstOutcome = await first;
        Assert.True(second is not null, "PostgreSQL did not observe the first contender waiting on the target relation.");
        HttpStatusCode[] outcomes = [firstOutcome, await second!];
        Assert.True(participants.Length == 2, "PostgreSQL did not observe two concurrent transactions waiting on the target relation.");
        Assert.NotEqual(participants[0].Pid, participants[1].Pid);
        Assert.DoesNotContain(blockerPid, participants.Select(row => row.Pid));
        // Every wait edge stays inside the race, and the chain reaches the transaction holding the target row.
        var racePids = participants.Select(row => row.Pid).Append(blockerPid).ToHashSet();
        Assert.All(participants, row => Assert.Subset(racePids, row.Blockers.ToHashSet()));
        Assert.Contains(participants, row => row.Blockers.Contains(blockerPid));
        var overlapStart = participants.Max(row => row.StartedAt);
        var overlapEnd = participants.Min(row => row.ObservedAt);
        Assert.True(overlapEnd > overlapStart);
        return new(blockerPid, participants, overlapStart, overlapEnd, outcomes);

        async Task<RaceParticipant[]> WaitForWaitersAsync(int count, params Task[] requests)
        {
            RaceParticipant[] waiting = [];
            while (!requests.Any(request => request.IsCompleted) && !deadline.IsCancellationRequested)
            {
                // clock_timestamp() is read per row after the activity snapshot, so it follows each xact_start.
                waiting = await observer.Database.SqlQuery<RaceParticipant>($"""
                    SELECT pid AS "Pid", xact_start AS "StartedAt", clock_timestamp() AS "ObservedAt",
                           wait_event AS "WaitEvent", pg_blocking_pids(pid) AS "Blockers"
                    FROM pg_stat_activity
                    WHERE datname = current_database() AND pid <> pg_backend_pid()
                      AND wait_event_type = 'Lock' AND xact_start IS NOT NULL AND query ILIKE {relationPattern}
                      AND cardinality(pg_blocking_pids(pid)) > 0
                    ORDER BY pid
                    """).ToArrayAsync();
                if (waiting.Length >= count) return waiting;
                await Task.Delay(30);
            }

            return waiting;
        }
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path, object request, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(path, request, cancellationToken);
        return response.StatusCode;
    }

    private sealed record RaceParticipant(int Pid, DateTime StartedAt, DateTime ObservedAt, string WaitEvent, int[] Blockers);
    private sealed record RaceEvidence(int BlockerPid, RaceParticipant[] Participants, DateTime OverlapStart, DateTime OverlapEnd,
        HttpStatusCode[] Outcomes);
}
