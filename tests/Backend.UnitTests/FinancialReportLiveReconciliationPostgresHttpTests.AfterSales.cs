using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class FinancialReportLiveReconciliationPostgresHttpTests
{
    [PostgresFact]
    public async Task PaidUndeliveredOrder_ManualQrRefund_ReleasesStockAndRecordsCashFlowWithoutSales()
    {
        await using var scenario = await Scenario.StartAsync();
        var product = await CreateProductAsync(scenario);
        var customer = await CreateCustomerAsync(scenario);
        var stockReceipt = await ReceiptAsync(scenario, product.Id, 1);
        await PostAsync<PurchaseReceiptDto>(scenario.Warehouse, ReceiptPath, stockReceipt);
        var managerShift = await OpenShiftAsync(scenario.Manager, 2_000_000);
        var draft = await PostAsync<OrderDto>(scenario.Seller, "/api/orders", new CreateOrderRequest
        {
            CustomerId = customer.Id, PaymentMethod = "Cash",
            Items = [new() { ProductId = product.Id, Quantity = 1 }]
        });
        using (var settleOrder = await scenario.Seller.PatchAsJsonAsync($"/api/orders/{draft.Id}/status",
                   new UpdateOrderStatusRequest { TargetStatus = "Processing" }))
            await AssertStatusAsync(settleOrder, "/api/orders/{id}/status");
        var before = await ReadReportAsync(scenario.Manager, Day1, Day1);
        Assert.Equal(EmptyTotals with { PaymentReceivedAmount = 1_100_000 }, before.Summary);
        Assert.Empty(before.CashByShift);
        Assert.Equal(1, (await StockAsync(scenario, product.Id)).ReservedQuantity);

        scenario.Clock.Now = At(29, 3);
        var claim = await PostAsync<ReturnDto>(scenario.Seller, ReturnPath, ReturnFor(draft));
        var path = $"{ReturnPath}/{claim.Id}";
        var pending = await PostAsync<ReturnDto>(scenario.Manager, path + "/settle", new SettleReturnRequest("VietQr"));
        Assert.Equal("AwaitingBank", pending.Status);
        Assert.Equal(EmptyTotals, (await ReadReportAsync(scenario.Manager, Day1.AddDays(1), Day1.AddDays(1))).Summary);

        const string proofReference = "recon-test-bank-001";
        var proof = new ReconcileReturnRequest(proofReference, 1_100_000, true, scenario.Clock.Now);
        var paid = await PostAsync<ReturnDto>(scenario.Manager, path + "/reconcile", proof);
        var report = await ReadReportAsync(scenario.Manager, Day1, Day1.AddDays(1));
        Assert.Equal(paid.RefundedAt, (await PostAsync<ReturnDto>(scenario.Manager, path + "/reconcile", proof)).RefundedAt);
        AssertDataEqual(report, await ReadReportAsync(scenario.Manager, Day1, Day1.AddDays(1)));
        var paidDay = EmptyTotals with { RefundPaidAmount = 1_100_000, RefundTaxAmount = 100_000, VietQrRefundPaidAmount = 1_100_000 };
        Assert.Equal(before.Summary, report.Daily[0].Totals);
        Assert.Equal(paidDay, report.Daily[1].Totals);
        Assert.Equal(paidDay with { PaymentReceivedAmount = 1_100_000 }, report.Summary);
        Assert.Empty(report.CashByShift);
        await AssertClosingAsync(scenario.Manager, managerShift.Id, 2_000_000);

        await using var db = scenario.Api.Database.CreateContext();
        var saved = await db.Orders.AsNoTracking().SingleAsync(order => order.Id == draft.Id);
        var payout = await db.OrderReturns.AsNoTracking().SingleAsync(row => row.OrderId == draft.Id);
        Assert.Equal(OrderStatus.Refunded, saved.Status);
        Assert.Equal(At(28, 1).UtcDateTime, saved.PaidAt);
        Assert.Null(saved.CompletedAt);
        Assert.Null(saved.VoucherId);
        Assert.Equal((PaymentMethod.VietQr, 1_100_000m, 100_000m, scenario.Clock.Now.UtcDateTime),
            (payout.RefundMethod, payout.RefundAmount, payout.TaxAmount, payout.RefundedAt));
        Assert.Null(payout.RefundPosSessionId);
        Assert.False(await db.Set<CashRefundPayout>().AnyAsync(row => row.RefundId == claim.Id));
        Assert.False(await db.InventoryMovements.AnyAsync(row => row.OrderReturnId == payout.Id));
        Assert.False(await db.CustomerLoyaltyPoints.AnyAsync(row => row.CustomerId == customer.Id));
        var stock = await StockAsync(scenario, product.Id);
        Assert.Equal((1, 0), (stock.Quantity, stock.ReservedQuantity));
        Assert.Equal(0, await DemoVoucherUsesAsync(scenario));
        Write("manual-qr-undelivered", new { Order = draft.Id, Case = claim.Id, Payout = payout.Id, proofReference,
            payout.RefundedAt, report.Daily, report.Summary, Movement = "reservation released", DrawerClosing = 2_000_000 });
    }

    [PostgresFact]
    public async Task QrReceiptReport_UsesTheExistingConfirmedM3Fixture()
    {
        await using var scenario = await Scenario.StartAsync();
        var from = new DateOnly(2026, 10, 15);
        var to = new DateOnly(2026, 10, 21);
        var report = await ReadReportAsync(scenario.Manager, from, to);
        await using var db = scenario.Api.Database.CreateContext();
        var confirmed = await db.VietQrTransactions.AsNoTracking()
            .Where(row => row.Order.OrderCode.StartsWith("DEMO-202610-") && row.Status == VietQrStatus.Confirmed)
            .Select(row => new { row.Id, row.OrderId, row.Amount, row.ConfirmedAt }).ToArrayAsync();
        Assert.NotEmpty(confirmed);
        Assert.All(confirmed, row => Assert.NotNull(row.ConfirmedAt));
        foreach (var day in report.Daily)
            Assert.Equal(confirmed.Where(row => DateOnly.FromDateTime(row.ConfirmedAt!.Value.AddHours(7)) == day.Date)
                .Sum(row => row.Amount), day.Totals.VietQrReceivedAmount);
        Assert.Equal(33_970_000m, report.Summary.VietQrReceivedAmount);
        Assert.Equal(88_630_000m, report.Summary.CashReceivedAmount);
        Assert.Equal(report.Summary.CashReceivedAmount, report.CashByShift.Sum(row => row.CashReceivedAmount));
        Write("seeded-qr-receipts", new { Mode = "Persisted M3 confirmations", report.StartDate, report.EndDate,
            report.Summary.VietQrReceivedAmount, report.Summary.CashReceivedAmount, confirmed });
    }

    [PostgresFact]
    public async Task WarrantyReplacementAndHandback_ReportOnlyIssueCostAndKeepOriginalGrossProfit()
    {
        await using var scenario = await Scenario.StartAsync();
        var product = await CreateProductAsync(scenario, tracked: true);
        var original = await ImportDeviceAsync(scenario, product.Id, 1);
        var repair = await ImportDeviceAsync(scenario, product.Id, 2);
        var spare = await ImportDeviceAsync(scenario, product.Id, 3, 500_000);
        var shift = await OpenShiftAsync(scenario.Seller, 0);
        var originalSale = await PostAsync<OrderDto>(scenario.Seller, CheckoutPath, Checkout(shift.Id, product.Id, serial: original));
        var repairSale = await PostAsync<OrderDto>(scenario.Seller, CheckoutPath, Checkout(shift.Id, product.Id, serial: repair));
        var salesBaseline = await ReadReportAsync(scenario.Manager, Day1, Day1);
        Assert.Equal(800_000m, salesBaseline.Summary.NetGrossProfit);

        scenario.Clock.Now = At(29, 17, 5);
        var handbackCase = Guid.NewGuid();
        await ReceiveAsync(scenario.Manager, handbackCase, repairSale, repair);
        using (var handback = await scenario.Manager.PostAsync($"/api/warranties/{handbackCase}/return", null))
            await AssertStatusAsync(handback, "/api/warranties/{id}/return");
        var issueDate = Day1.AddDays(2);
        Assert.Equal(0m, (await ReadReportAsync(scenario.Manager, issueDate, issueDate)).Summary.WarrantyCost);
        var replacingCase = Guid.NewGuid();
        await ReceiveAsync(scenario.Manager, replacingCase, originalSale, original);
        var replaceRequest = new ReplaceWarrantyDeviceRequest(spare);
        var path = $"/api/warranties/{replacingCase}/replace";
        var replaced = await PostAsync<WarrantyCaseDto>(scenario.Manager, path, replaceRequest);
        var snapshot = await ReadReportAsync(scenario.Manager, Day1, issueDate);
        Assert.Equal(replaced, await PostAsync<WarrantyCaseDto>(scenario.Manager, path, replaceRequest));
        AssertDataEqual(snapshot, await ReadReportAsync(scenario.Manager, Day1, issueDate));
        Assert.Equal(salesBaseline.Summary with { WarrantyCost = 500_000m }, snapshot.Summary);
        Assert.Equal(EmptyTotals with { WarrantyCost = 500_000m }, snapshot.Daily[2].Totals);

        await using var db = scenario.Api.Database.CreateContext();
        var claim = await db.Set<WarrantyCase>().AsNoTracking().SingleAsync(row => row.Id == replacingCase);
        Assert.Equal(WarrantyCaseStatus.Replaced, claim.Status);
        Assert.Equal((spare, 500_000m), (claim.ReplacementDeviceId, claim.ReplacementUnitCost));
        var issue = await db.InventoryMovements.AsNoTracking().SingleAsync(row => row.Id == claim.MovementId);
        Assert.Equal((InventoryMovementType.WarrantyReplacement, -1, scenario.Clock.Now.UtcDateTime),
            (issue.MovementType, issue.QuantityChange, issue.CreatedAt));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(row => row.InventoryStock.ProductId == product.Id && row.MovementType == InventoryMovementType.WarrantyReplacement));
        var devices = await db.SerialImeis.AsNoTracking().Where(row => row.ProductId == product.Id).ToDictionaryAsync(row => row.Id);
        Assert.Equal(SerialImeiStatus.Defective, devices[original].Status);
        Assert.Equal(SerialImeiStatus.Sold, devices[repair].Status);
        Assert.Equal(SerialImeiStatus.Sold, devices[spare].Status);
        Assert.Equal(0, (await StockAsync(scenario, product.Id)).Quantity);
        Write("warranty", new { Case = replacingCase, Handback = handbackCase, Original = original, Spare = spare,
            Movement = issue.Id, issue.CreatedAt, claim.ReplacementUnitCost, snapshot.Summary });
    }

    [PostgresFact]
    public async Task FailedRefund_RollsBackLedgerAndDevice_ThenDefectiveImeiIsUnavailableForCheckout()
    {
        await using var scenario = await Scenario.StartAsync();
        var product = await CreateProductAsync(scenario, tracked: true);
        var imei = await ImportDeviceAsync(scenario, product.Id, 1);
        var customer = await CreateCustomerAsync(scenario);
        var sellingShift = await OpenShiftAsync(scenario.Seller, 0);
        var payingShift = await OpenShiftAsync(scenario.Manager, 2_000_000);
        var sale = await PostAsync<OrderDto>(scenario.Seller, CheckoutPath, Checkout(sellingShift.Id, product.Id, customer: customer.Id, serial: imei));
        var request = ReturnFor(sale);
        var claim = await PostAsync<ReturnDto>(scenario.Seller, ReturnPath, request);
        var before = await ReadReportAsync(scenario.Manager, Day1, Day1);
        await using var db = scenario.Api.Database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_reconciliation_return() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Reconciliation fixture rejected order_returns insert'; END; $$;
            CREATE TRIGGER fail_reconciliation_return BEFORE INSERT ON order_returns
              FOR EACH ROW EXECUTE FUNCTION fail_reconciliation_return();
            """);
        try
        {
            using var failed = await scenario.Manager.PostAsJsonAsync($"{ReturnPath}/{claim.Id}/settle", new SettleReturnRequest("Cash"));
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            AssertDataEqual(before, await ReadReportAsync(scenario.Manager, Day1, Day1));
            Assert.Equal(claim.Status, (await ReadAsync<ReturnDto>(scenario.Manager, $"{ReturnPath}/{claim.Id}")).Status);
            Assert.False(await db.OrderReturns.AnyAsync(row => row.OrderId == sale.Id));
            Assert.False(await db.Set<CashRefundPayout>().AnyAsync(row => row.RefundId == claim.Id));
            Assert.False(await db.CustomerLoyaltyPoints.AnyAsync(row => row.RefundCaseId == claim.Id));
            Assert.Equal(10, await db.Customers.Where(row => row.Id == customer.Id).Select(row => row.LoyaltyPoints).SingleAsync());
            Assert.Equal(SerialImeiStatus.Sold, await db.SerialImeis.Where(row => row.Id == imei).Select(row => row.Status).SingleAsync());
            Assert.Equal(0m, (await ReadAsync<PosShiftDto>(scenario.Manager, $"{ShiftPath}/{payingShift.Id}")).CashRefundTotal);
            Assert.Equal(0, await DemoVoucherUsesAsync(scenario));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_reconciliation_return ON order_returns; DROP FUNCTION fail_reconciliation_return();");
        }

        await SettleCashAsync(scenario.Manager, claim.Id);
        var caseLine = await InspectAsync(scenario, claim.Id, "Defective");
        var line = await db.OrderReturns.Where(row => row.CaseLineId == caseLine).Select(row => row.Id).SingleAsync();
        Assert.Equal(SerialImeiStatus.Defective, await db.SerialImeis.Where(row => row.Id == imei).Select(row => row.Status).SingleAsync());
        Assert.False(await db.InventoryMovements.AnyAsync(row => row.OrderReturnId == line && row.MovementType == InventoryMovementType.CustomerReturn));
        var after = await ReadReportAsync(scenario.Manager, Day1, Day1);
        var reject = Checkout(sellingShift.Id, product.Id, customer: customer.Id, serial: imei);
        using var resell = await scenario.Seller.PostAsJsonAsync(CheckoutPath, reject);
        Assert.Equal(HttpStatusCode.Conflict, resell.StatusCode);
        Assert.False(await db.Orders.AnyAsync(row => row.CheckoutKey == reject.CheckoutKey));
        AssertDataEqual(after, await ReadReportAsync(scenario.Manager, Day1, Day1));
        Assert.Equal(600_000m, after.Summary.NetCostOfGoodsSold);
        Assert.Equal(-600_000m, after.Summary.NetGrossProfit);
        Assert.Equal(0, await DemoVoucherUsesAsync(scenario));
        Write("rollback-and-defective", new { Order = sale.Id, Case = claim.Id, ImeiId = imei, RefundLine = line,
            Failure = 500, RejectedCheckout = 409, PayoutTurn = "committed after the trigger was removed", after.Summary });
    }

    private static async Task<Guid> ImportDeviceAsync(Scenario scenario, Guid product, int number, decimal cost = 600_000)
    {
        var imei = "490999900000" + number.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        using var response = await scenario.Admin.PostAsJsonAsync("/api/inventory/serials/batch-import", new BatchImportSerialImeiRequest
        {
            ProductId = product, DefaultUnitCost = cost, DefaultWarrantyMonths = 12, Reason = "Reconciliation fixture",
            Items = [new() { Imei = imei }]
        });
        await AssertStatusAsync(response, "/api/inventory/serials/batch-import", HttpStatusCode.Created);
        await using var db = scenario.Api.Database.CreateContext();
        return await db.SerialImeis.Where(row => row.ProductId == product && row.Imei == imei).Select(row => row.Id).SingleAsync();
    }

    private static Task<WarrantyCaseDto> ReceiveAsync(HttpClient manager, Guid claim, OrderDto sale, Guid imei) =>
        PostAsync<WarrantyCaseDto>(manager, $"/api/warranties/{claim}/receive", ReceiveRequest(sale, imei));

    private static ReceiveWarrantyRequest ReceiveRequest(OrderDto sale, Guid imei) =>
        new(sale.Id, Assert.Single(sale.Items).Id, imei, sale.CustomerId);

    private static async Task<int> DemoVoucherUsesAsync(Scenario scenario)
    {
        await using var db = scenario.Api.Database.CreateContext();
        return await db.Vouchers.Where(row => row.Code == "DEMO-OCT26").Select(row => row.UsedCount).SingleAsync();
    }
}
