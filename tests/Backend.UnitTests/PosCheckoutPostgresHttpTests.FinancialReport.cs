using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Analytics;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task FinancialReport_RefundRestockWarrantyAndShiftReplays_ReconcileToHandCalculatedTotals()
    {
        await using var world = await PosWorld.CreateAsync(3);
        // Use the D6 calendar positions in a different year from the shared demo seed.
        var clock = new FinancialWorkflowClock(On(27, 2));
        await using var host = world.Api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(clock)));
        using var seller = CloneClient(world.SalesA.Client);
        using var manager = CloneClient(world.Manager);
        using var warehouse = CloneClient(world.Warehouse);
        await using var db = world.Database.CreateContext();

        var oldShift = await world.OpenShiftAsync(new(seller, world.SalesA.UserId), 0);
        var warrantySale = await PostDto<OrderDto>(seller, "/api/pos/checkout",
            Checkout(world, oldShift.Id, world.DeviceIds[0], Total));
        var repairSale = await PostDto<OrderDto>(seller, "/api/pos/checkout",
            Checkout(world, oldShift.Id, world.DeviceIds[1], Total));
        await PostDto<PosShiftDto>(manager, $"/api/pos/shifts/{oldShift.Id}/close",
            new CloseShiftRequest { ClosingBalance = 2 * Total });
        await db.SerialImeis.Where(device => device.Id == world.DeviceIds[2])
            .ExecuteUpdateAsync(setters => setters.SetProperty(device => device.UnitCost, 500_000m));

        clock.Now = On(28, 2);
        var salesShift = await world.OpenShiftAsync(new(seller, world.SalesA.UserId), 0);
        var stock = await AddQuantityStock(world, db, 0);
        await PostDto<PurchaseReceiptDto>(warehouse, "/api/inventory/purchase-receipts", new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(), SupplierId = await db.Suppliers.Where(s => s.IsActive).Select(s => s.Id).FirstAsync(),
            Lines = [new() { ProductId = stock.ProductId, Quantity = 2, UnitCost = 600_000m }]
        });
        var sale = await PostDto<OrderDto>(seller, "/api/pos/checkout", new PosCheckoutRequest
        {
            PosSessionId = salesShift.Id, CheckoutKey = Guid.NewGuid(), OrderDiscountAmount = 200_000m,
            CashTendered = 2_000_000m, Items = [new() { ProductId = stock.ProductId, Quantity = 2 }]
        });
        Assert.Equal(1_980_000m, sale.TotalAmount);

        clock.Now = On(29, 16, 55);
        var payoutShift = await world.OpenShiftAsync(new(manager, world.ManagerUserId), 2_000_000m);
        var first = await PostDto<ReturnDto>(seller, "/api/returns", ReturnFor(sale, 1));
        var settle = $"/api/returns/{first.Id}/settle";
        var firstPaid = await PostDto<ReturnDto>(manager, settle, new SettleReturnRequest("Cash"));
        var beforePayoutReplay = await ReadReportAsync(manager, 28, 29);
        Assert.Equal(firstPaid.RefundedAt, (await PostDto<ReturnDto>(manager, settle, new SettleReturnRequest("Cash"))).RefundedAt);
        Assert.Equal(beforePayoutReplay.Summary, (await ReadReportAsync(manager, 28, 29)).Summary);
        Assert.Equal(990_000m, beforePayoutReplay.Summary.RefundPaidAmount);
        Assert.Equal(0m, beforePayoutReplay.Summary.RestockedCost);

        clock.Now = On(29, 17, 5);
        var readyToInspect = await warehouse.GetFromJsonAsync<List<ReturnInspectionDto>>("/api/inventory/returns");
        var firstLine = Assert.Single(readyToInspect!, line => line.RequestId == first.Id);
        var inspectPath = $"/api/inventory/returns/{firstLine.ReturnLineId}/inspections";
        var restock = new InspectReturnRequest(Guid.NewGuid(), 1, "InStock");
        await PostDto<ReturnInspectionDto>(warehouse, inspectPath, restock);
        var beforeInspectReplay = await ReadReportAsync(manager, 30, 30);
        await PostDto<ReturnInspectionDto>(warehouse, inspectPath, restock);
        Assert.Equal(beforeInspectReplay.Summary, (await ReadReportAsync(manager, 30, 30)).Summary);
        Assert.Equal(600_000m, beforeInspectReplay.Summary.RestockedCost);

        var repairPath = $"/api/warranties/{Guid.NewGuid()}";
        await PostDto<WarrantyCaseDto>(manager, repairPath + "/receive",
            new ReceiveWarrantyRequest(repairSale.Id, repairSale.Items.Single().Id, world.DeviceIds[1], null));
        using (var handback = await manager.PostAsync(repairPath + "/return", null))
            handback.EnsureSuccessStatusCode();
        Assert.Equal(0m, (await ReadReportAsync(manager, 30, 30)).Summary.WarrantyCost);
        var warrantyPath = $"/api/warranties/{Guid.NewGuid()}";
        await PostDto<WarrantyCaseDto>(manager, warrantyPath + "/receive",
            new ReceiveWarrantyRequest(warrantySale.Id, warrantySale.Items.Single().Id, world.DeviceIds[0], null));
        var replacement = new ReplaceWarrantyDeviceRequest(world.DeviceIds[2]);
        var replaced = await PostDto<WarrantyCaseDto>(manager, warrantyPath + "/replace", replacement);
        var beforeWarrantyReplay = await ReadReportAsync(manager, 30, 30);
        Assert.Equal(replaced, await PostDto<WarrantyCaseDto>(manager, warrantyPath + "/replace", replacement));
        Assert.Equal(beforeWarrantyReplay.Summary, (await ReadReportAsync(manager, 30, 30)).Summary);
        Assert.Equal(500_000m, beforeWarrantyReplay.Summary.WarrantyCost);
        Assert.Equal(600_000m, beforeWarrantyReplay.Summary.NetGrossProfit);

        var qrStock = await AddQuantityStock(world, db, 2);
        await db.Products.Where(product => product.Id == qrStock.ProductId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.BasePrice, 500_000m));
        var qrDraft = new CreateOrderRequest
        {
            PaymentMethod = "VietQr", Items = [new CreateOrderItemRequest { ProductId = qrStock.ProductId, Quantity = 1 }]
        };
        var undelivered = await PostDto<OrderDto>(seller, "/api/orders", qrDraft);
        var pending = await PostDto<OrderDto>(seller, "/api/orders", qrDraft);
        db.VietQrTransactions.AddRange(QrFixture(undelivered, true), QrFixture(pending, false));
        await db.SaveChangesAsync();
        using (var processing = await seller.PatchAsJsonAsync($"/api/orders/{undelivered.Id}/status",
                   new UpdateOrderStatusRequest { TargetStatus = "Processing" }))
            processing.EnsureSuccessStatusCode();

        clock.Now = On(31, 2);
        var denied = await PostDto<ReturnDto>(seller, "/api/returns", ReturnFor(sale, 1));
        var deniedPath = $"/api/returns/{denied.Id}";
        await PostDto<ReturnDto>(manager, deniedPath + "/settle", new SettleReturnRequest("VietQr"));
        Assert.Equal(0m, (await ReadReportAsync(manager, 31, 31)).Summary.RefundPaidAmount);
        await PostDto<ReturnDto>(manager, deniedPath + "/reconcile", new ReconcileReturnRequest("fr-denied", 0, false, null));
        Assert.Equal(0m, (await ReadReportAsync(manager, 31, 31)).Summary.RefundPaidAmount);

        var last = await CompleteBankRefundAsync(sale, 990_000m, "fr-delivered");
        var pendingInspection = await warehouse.GetFromJsonAsync<List<ReturnInspectionDto>>("/api/inventory/returns");
        var lastLine = Assert.Single(pendingInspection!, line => line.RequestId == last.Id);
        await PostDto<ReturnInspectionDto>(warehouse, $"/api/inventory/returns/{lastLine.ReturnLineId}/inspections",
            new InspectReturnRequest(Guid.NewGuid(), 1, "Defective"));
        await CompleteBankRefundAsync(undelivered, 550_000m, "fr-undelivered");
        Assert.Null(await db.Orders.Where(order => order.Id == undelivered.Id).Select(order => order.CompletedAt).SingleAsync());
        var drawer = await PostDto<PosShiftDto>(manager, $"/api/pos/shifts/{payoutShift.Id}/close",
            new CloseShiftRequest { ClosingBalance = 1_010_000m });
        Assert.Equal(1_010_000m, drawer.ExpectedClosingBalance);
        Assert.Equal(0m, drawer.Discrepancy);

        var report = await ReadReportAsync(manager, 28, 31);
        decimal[][] expectedInThousands =
        [
            [1800, 1200, 0, 0, 0, 0, 1800, 1200, 600, 0, 1980, 0, 0, 0],
            [0, 0, 990, 90, 900, 0, -900, 0, -900, 0, 0, 990, 0, 0],
            [0, 0, 0, 0, 0, 600, 0, -600, 600, 500, 0, 0, 550, 0],
            [0, 0, 1540, 140, 900, 0, -900, 0, -900, 0, 0, 0, 0, 1540],
            [1800, 1200, 2530, 230, 1800, 600, 0, 600, -600, 500, 1980, 990, 550, 1540]
        ];
        Assert.Equal(4, report.Daily.Count);
        var actual = report.Daily.Select(day => Amounts(day.Totals)).ToArray();
        for (var i = 0; i < actual.Length; i++)
            Assert.Equal(expectedInThousands[i].Select(value => (decimal?)(value * 1000)), actual[i]);
        var summary = Amounts(report.Summary);
        Assert.Equal(expectedInThousands[^1].Select(value => (decimal?)(value * 1000)), summary);
        Assert.Equal(summary, Enumerable.Range(0, summary.Length).Select(column => (decimal?)actual.Sum(row => row[column]!.Value)));

        Assert.Equal(new[] { salesShift.Id, payoutShift.Id }.Order(), report.CashByShift.Select(row => row.PosSessionId));
        AssertShift(Assert.Single(report.CashByShift, row => row.PosSessionId == salesShift.Id), 1_980_000, 0);
        AssertShift(Assert.Single(report.CashByShift, row => row.PosSessionId == payoutShift.Id), 0, 990_000);
        var cut = await ReadReportAsync(manager, 29, 29);
        AssertShift(Assert.Single(cut.CashByShift), 0, 990_000);
        Assert.Equal(payoutShift.Id, cut.CashByShift[0].PosSessionId);
        Assert.Equal(-900_000m, cut.Summary.NetSalesExcludingTax);
        Assert.Equal(-600_000m, (await ReadReportAsync(manager, 30, 30)).Summary.NetCostOfGoodsSold);

        var legacyClient = await manager.GetFromJsonAsync<FinancialReportDto>(FinancialRange(28, 31));
        Assert.Equal(-600_000m, legacyClient!.Summary.NetGrossProfit);
        Assert.Equal(2_530_000m, legacyClient.Summary.RefundPaidAmount);
        Assert.Equal(2_530_000m, legacyClient.Summary.PaymentReceivedAmount);
        Assert.Equal(1, legacyClient.Summary.CompletedOrderCount);

        HttpClient CloneClient(HttpClient identity)
        {
            var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
            client.DefaultRequestHeaders.Authorization = identity.DefaultRequestHeaders.Authorization;
            return client;
        }

        VietQrTransaction QrFixture(OrderDto order, bool confirmed) => new()
        {
            OrderId = order.Id, TransactionCode = $"FR-{Guid.NewGuid():N}", Amount = order.TotalAmount,
            BankBin = "970422", BankAccountNumber = "1234567890", QrContent = "test",
            CreatedAt = clock.Now.UtcDateTime.AddMinutes(-2), ExpiresAt = clock.Now.UtcDateTime.AddMinutes(13),
            Status = confirmed ? VietQrStatus.Confirmed : VietQrStatus.Pending,
            ConfirmedAt = confirmed ? clock.Now.UtcDateTime.AddMinutes(-1) : null,
            ConfirmedByUserId = confirmed ? world.ManagerUserId : null
        };

        async Task<ReturnDto> CompleteBankRefundAsync(OrderDto order, decimal amount, string reference)
        {
            var requested = await PostDto<ReturnDto>(seller, "/api/returns", ReturnFor(order, 1));
            var path = $"/api/returns/{requested.Id}";
            await PostDto<ReturnDto>(manager, path + "/settle", new SettleReturnRequest("VietQr"));
            var proof = new ReconcileReturnRequest(reference, amount, true, clock.Now);
            var paid = await PostDto<ReturnDto>(manager, path + "/reconcile", proof);
            var beforeReplay = await ReadReportAsync(manager, 31, 31);
            Assert.Equal(paid.RefundedAt, (await PostDto<ReturnDto>(manager, path + "/reconcile", proof)).RefundedAt);
            Assert.Equal(beforeReplay.Summary, (await ReadReportAsync(manager, 31, 31)).Summary);
            return paid;
        }
    }

    [PostgresFact]
    public async Task FinancialReport_InspectionSplitsPaidLine_PreservesRefundAndMarksUnknownRestockCost()
    {
        await using var world = await PosWorld.CreateAsync(1);
        var clock = new FinancialWorkflowClock(On(28, 2));
        await using var host = world.Api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(clock)));
        using var seller = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var manager = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var warehouse = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        seller.DefaultRequestHeaders.Authorization = world.SalesA.Client.DefaultRequestHeaders.Authorization;
        manager.DefaultRequestHeaders.Authorization = world.Manager.DefaultRequestHeaders.Authorization;
        warehouse.DefaultRequestHeaders.Authorization = world.Warehouse.DefaultRequestHeaders.Authorization;
        await using var db = world.Database.CreateContext();
        var stock = await AddQuantityStock(world, db, 4); // no purchase cost snapshot
        var shift = await world.OpenShiftAsync(new(seller, world.SalesA.UserId), 0);
        var sale = await PostDto<OrderDto>(seller, "/api/pos/checkout", new PosCheckoutRequest
        {
            PosSessionId = shift.Id, CheckoutKey = Guid.NewGuid(), CashTendered = 4_400_000,
            Items = [new() { ProductId = stock.ProductId, Quantity = 4 }]
        });
        Assert.Null(await db.OrderItems.Where(item => item.OrderId == sale.Id).Select(item => item.UnitCost).SingleAsync());

        clock.Now = On(29, 16, 55);
        var drawer = await world.OpenShiftAsync(new(manager, world.ManagerUserId), 4_000_000);
        var request = await PostDto<ReturnDto>(seller, "/api/returns", ReturnFor(sale, 3));
        await PostDto<ReturnDto>(manager, $"/api/returns/{request.Id}/settle", new SettleReturnRequest("Cash"));
        var before = await ReadReportAsync(manager, 29, 29);
        Assert.Equal(3_300_000m, before.Summary.RefundPaidAmount);
        Assert.Equal(300_000m, before.Summary.RefundTaxAmount);
        Assert.Equal(3_000_000m, before.Summary.RefundedSalesExcludingTax);
        Assert.Equal(0m, before.Summary.RestockedCost);
        AssertShift(Assert.Single(before.CashByShift), 0, 3_300_000);
        Assert.Equal(drawer.Id, before.CashByShift[0].PosSessionId);

        clock.Now = On(29, 17, 5);
        var queue = await warehouse.GetFromJsonAsync<List<ReturnInspectionDto>>("/api/inventory/returns");
        var line = Assert.Single(queue!, row => row.RequestId == request.Id);
        var path = $"/api/inventory/returns/{line.ReturnLineId}/inspections";
        await PostDto<ReturnInspectionDto>(warehouse, path, new InspectReturnRequest(Guid.NewGuid(), 1, "InStock"));
        await PostDto<ReturnInspectionDto>(warehouse, path, new InspectReturnRequest(Guid.NewGuid(), 1, "Defective"));
        await PostDto<ReturnInspectionDto>(warehouse, path, new InspectReturnRequest(Guid.NewGuid(), 1, "UnderRepair"));
        Assert.Equal(3, await db.OrderReturns.CountAsync(row => row.OrderId == sale.Id));
        var movement = await db.InventoryMovements.SingleAsync(m => m.InventoryStockId == stock.Id
            && m.MovementType == InventoryMovementType.CustomerReturn);
        Assert.Equal(1, movement.QuantityChange);
        var after = await ReadReportAsync(manager, 29, 29);
        Assert.Equal(before.Summary, after.Summary);
        Assert.Equal(before.CashByShift.ToArray(), after.CashByShift.ToArray());
        var next = await ReadReportAsync(manager, 30, 30);
        Assert.Equal(0m, next.Summary.RefundPaidAmount);
        Assert.Null(next.Summary.RestockedCost);
        Assert.Null(next.Summary.NetCostOfGoodsSold);
        Assert.Null(next.Summary.NetGrossProfit);
        Assert.Null((await ReadReportAsync(manager, 29, 30)).Summary.NetGrossProfit);
    }

    [PostgresFact]
    public async Task FinancialReport_ReplacementUnknownAndZeroCost_AppearSeparatelyFromSales()
    {
        await using var world = await PosWorld.CreateAsync(4);
        var clock = new FinancialWorkflowClock(On(27, 2));
        await using var host = world.Api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(clock)));
        using var seller = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var manager = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        seller.DefaultRequestHeaders.Authorization = world.SalesA.Client.DefaultRequestHeaders.Authorization;
        manager.DefaultRequestHeaders.Authorization = world.Manager.DefaultRequestHeaders.Authorization;
        await using var db = world.Database.CreateContext();
        await db.SerialImeis.Where(device => device.Id == world.DeviceIds[2])
            .ExecuteUpdateAsync(setters => setters.SetProperty(device => device.UnitCost, (decimal?)null));
        await db.SerialImeis.Where(device => device.Id == world.DeviceIds[3])
            .ExecuteUpdateAsync(setters => setters.SetProperty(device => device.UnitCost, 0m));
        var shift = await world.OpenShiftAsync(new(seller, world.SalesA.UserId), 0);
        for (var i = 0; i < 2; i++)
        {
            clock.Now = On(27, 3);
            var original = await PostDto<OrderDto>(seller, "/api/pos/checkout", Checkout(world, shift.Id, world.DeviceIds[i], Total));
            clock.Now = On(29 + i, 2);
            var path = $"/api/warranties/{Guid.NewGuid()}";
            await PostDto<WarrantyCaseDto>(manager, path + "/receive",
                new ReceiveWarrantyRequest(original.Id, original.Items.Single().Id, world.DeviceIds[i], null));
            await PostDto<WarrantyCaseDto>(manager, path + "/replace", new ReplaceWarrantyDeviceRequest(world.DeviceIds[i + 2]));
        }

        var report = await ReadReportAsync(manager, 29, 30);
        Assert.Null(report.Summary.WarrantyCost);
        Assert.Null(report.Daily[0].Totals.WarrantyCost);
        Assert.Equal(0m, report.Daily[1].Totals.WarrantyCost);
        Assert.Equal(0m, report.Summary.NetGrossProfit);
        Assert.Equal(0m, report.Summary.RefundPaidAmount);
    }

    private static DateTimeOffset On(int day, int hour, int minute = 0) => new(2025, 10, day, hour, minute, 0, TimeSpan.Zero);

    private static string FinancialRange(int from, int to)
        => $"/api/analytics/financial-report?period=custom&from=2025-10-{from:D2}&to=2025-10-{to:D2}";

    private static async Task<FinancialReportResponse> ReadReportAsync(HttpClient manager, int from, int to)
        => (await manager.GetFromJsonAsync<FinancialReportResponse>(FinancialRange(from, to)))!;

    private static decimal?[] Amounts(FinancialReportTotalsResponse t) =>
        [t.SalesExcludingTax, t.CostOfGoodsSold, t.RefundPaidAmount, t.RefundTaxAmount, t.RefundedSalesExcludingTax,
         t.RestockedCost, t.NetSalesExcludingTax, t.NetCostOfGoodsSold, t.NetGrossProfit, t.WarrantyCost,
         t.CashReceivedAmount, t.CashRefundPaidAmount, t.VietQrReceivedAmount, t.VietQrRefundPaidAmount];

    private static void AssertShift(FinancialReportCashShiftResponse row, decimal received, decimal paid)
        => Assert.Equal(((decimal?)received, (decimal?)paid, (decimal?)(received - paid)),
            (row.CashReceivedAmount, row.CashRefundPaidAmount, row.CashNetMovement));

    private sealed class FinancialWorkflowClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
