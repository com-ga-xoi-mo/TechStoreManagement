using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Analytics;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.UnitTests;

public sealed partial class FinancialReportLiveReconciliationPostgresHttpTests(ITestOutputHelper output)
{
    private static readonly DateOnly Day1 = new(2025, 10, 28);
    private const string ReportPath = "/api/analytics/financial-report";
    private const string ReceiptPath = "/api/inventory/purchase-receipts";
    private const string CheckoutPath = "/api/pos/checkout";
    private const string ShiftPath = "/api/pos/shifts";
    private const string ReturnPath = "/api/returns";

    [PostgresFact]
    public async Task Report_BeforeActivity_ChecksRoleMatrixAndPreservesTheM3Fixture()
    {
        await using var scenario = await Scenario.StartAsync();
        await using var db = scenario.Api.Database.CreateContext();
        Assert.True(db.Database.IsNpgsql());
        Assert.StartsWith("techstore_category_test_", db.Database.GetDbConnection().Database);
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        const string seedPrefix = "DEMO-202610-";
        var m3Orders = await db.Orders.AsNoTracking().Where(order => order.OrderCode.StartsWith(seedPrefix))
            .OrderBy(order => order.OrderCode)
            .Select(order => new { order.OrderCode, order.Status, order.PaymentMethod, order.PaidAt, order.CompletedAt })
            .ToArrayAsync();
        // The October 2026 fixture must be the unchanged manifest: same codes, statuses, channels and event times.
        Assert.Equal(DemoSeedManifest.Orders.OrderBy(seed => seed.OrderCode, StringComparer.Ordinal)
                .Select(seed => (seed.OrderCode, seed.Status, seed.PaymentMethod, seed.PaidAt, seed.CompletedAt)),
            m3Orders.Select(order => (order.OrderCode, order.Status, order.PaymentMethod, order.PaidAt, order.CompletedAt)));
        var m3OrderCount = m3Orders.Length;

        var range = Range(Day1, Day1);
        var before = await ReadReportAsync(scenario.Manager, Day1, Day1);
        Assert.Equal(EmptyTotals, before.Summary);
        Assert.Equal(EmptyTotals, Assert.Single(before.Daily).Totals);
        Assert.Empty(before.CashByShift);
        AssertDataEqual(before, await ReadReportAsync(scenario.Admin, Day1, Day1));
        await IdentityPostgresApi.AssertSafeFailureAsync(await scenario.Seller.GetAsync(range), HttpStatusCode.Forbidden);
        await IdentityPostgresApi.AssertSafeFailureAsync(await scenario.Warehouse.GetAsync(range), HttpStatusCode.Forbidden);
        using var guest = NewClient(scenario.Host);
        await IdentityPostgresApi.AssertSafeFailureAsync(await guest.GetAsync(range), HttpStatusCode.Unauthorized);
        guest.DefaultRequestHeaders.Authorization = new("Bearer", "invalid.jwt.signature");
        await IdentityPostgresApi.AssertSafeFailureAsync(await guest.GetAsync(range), HttpStatusCode.Unauthorized);
        await IdentityPostgresApi.AssertSafeFailureAsync(await scenario.Manager.GetAsync(Range(Day1, Day1.AddDays(-1))), HttpStatusCode.BadRequest);

        Write("baseline", new { before.AsOf, before.StartDate, before.EndDate, before.Summary, M3OctoberOrders = m3OrderCount,
            Admin = 200, Manager = 200, SalesStaff = 403, WarehouseStaff = 403, MissingOrInvalidJwt = 401, ReversedDates = 400 });
    }

    [PostgresFact]
    public async Task CashLifecycle_ReceiptsRefundsRestockAndDefect_MatchDailyAndPeriodExpectations()
    {
        await using var scenario = await Scenario.StartAsync();
        var product = await CreateProductAsync(scenario);
        var customer = await CreateCustomerAsync(scenario);
        var before = await ReadReportAsync(scenario.Manager, Day1, Day1);
        Assert.Equal(EmptyTotals, before.Summary);
        await using var db = scenario.Api.Database.CreateContext();
        Assert.False(await db.InventoryMovements.AnyAsync(move => move.InventoryStock.ProductId == product.Id));
        var receiptRequest = await ReceiptAsync(scenario, product.Id, 2);
        var receipt = await PostAsync<PurchaseReceiptDto>(scenario.Warehouse, ReceiptPath, receiptRequest, HttpStatusCode.Created);
        Assert.Equal(PurchaseOrderStatus.Received, receipt.Status);
        Assert.Equal(1_200_000m, receipt.TotalCost);
        var duplicateReceipt = await PostAsync<PurchaseReceiptDto>(scenario.Warehouse, ReceiptPath, receiptRequest);
        Assert.Equal(receipt.Id, duplicateReceipt.Id);
        Assert.Equal(2, (await StockAsync(scenario, product.Id)).Quantity);
        var purchaseMovement = await db.InventoryMovements.AsNoTracking().SingleAsync(move =>
            move.InventoryStock.ProductId == product.Id && move.MovementType == InventoryMovementType.PurchaseReceipt);
        Assert.Equal((2, 2), (purchaseMovement.QuantityChange, purchaseMovement.QuantityAfter));
        AssertDataEqual(before, await ReadReportAsync(scenario.Manager, Day1, Day1));

        scenario.Clock.Now = At(28, 2);
        var sellingShift = await OpenShiftAsync(scenario.Seller, 2_000_000);
        var checkout = Checkout(sellingShift.Id, product.Id, 2, customer.Id, 200_000);
        var sale = await PostAsync<OrderDto>(scenario.Seller, CheckoutPath, checkout, HttpStatusCode.Created);
        Assert.Equal(sale.Id, (await PostAsync<OrderDto>(scenario.Seller, CheckoutPath, checkout)).Id);
        var saved = await db.Orders.AsNoTracking().Include(order => order.Items).SingleAsync(order => order.Id == sale.Id);
        var line = Assert.Single(saved.Items);
        Assert.Equal((2_000_000m, 200_000m, 180_000m, 1_980_000m),
            (saved.Subtotal, saved.DiscountAmount, saved.TaxAmount, saved.TotalAmount));
        Assert.Equal((2_000_000m, 20_000m), (saved.CashTendered, saved.ChangeGiven));
        Assert.Equal((2, 1_000_000m, 200_000m, 180_000m, 600_000m),
            (line.Quantity, line.UnitPrice, line.OrderDiscountAmount, line.TaxAmount, line.UnitCost));
        Assert.Equal((scenario.Clock.Now.UtcDateTime, scenario.Clock.Now.UtcDateTime), (saved.CompletedAt, saved.PaidAt));
        Assert.Equal(0, (await StockAsync(scenario, product.Id)).Quantity);
        Assert.Equal(18, (await db.CustomerLoyaltyPoints.AsNoTracking().SingleAsync(point => point.OrderId == sale.Id)).PointsChange);
        Assert.Equal(18, await db.Customers.Where(row => row.Id == customer.Id).Select(row => row.LoyaltyPoints).SingleAsync());
        Assert.Equal(1_980_000m, (await ReadAsync<PosShiftDto>(scenario.Seller, $"{ShiftPath}/{sellingShift.Id}")).CashSalesTotal);

        var delivered = new FinancialReportTotalsResponse(1, 2, 0, 1_800_000, 180_000, 1_980_000,
            1_200_000, 0, 0, 0, 0, 0, 1_980_000, 0, 0, 0);
        var refundDay = EmptyTotals with { RefundPaidAmount = 990_000, RefundedSalesExcludingTax = 900_000,
            RefundTaxAmount = 90_000, CashRefundPaidAmount = 990_000 };
        var restockDay = EmptyTotals with { RestockedCost = 600_000 };
        Assert.Equal(delivered, (await ReadReportAsync(scenario.Manager, Day1, Day1)).Summary);

        scenario.Clock.Now = At(29, 16, 55);
        var payingShift = await OpenShiftAsync(scenario.Manager, 3_000_000);
        var first = await RefundOneAsync(1, 9);
        var beforeRestock = await ReadReportAsync(scenario.Manager, Day1, Day1.AddDays(1));
        Assert.Equal(-300_000m, beforeRestock.Summary.NetGrossProfit);
        Assert.Equal(refundDay, beforeRestock.Daily[1].Totals);
        Assert.Equal(0, (await StockAsync(scenario, product.Id)).Quantity);
        Assert.Equal(OrderStatus.PartiallyRefunded, await db.Orders.Where(o => o.Id == sale.Id).Select(o => o.Status).SingleAsync());

        scenario.Clock.Now = At(29, 17, 5);
        var stocked = await InspectAsync(scenario, first.CaseId, "InStock");
        Assert.Equal(first.CaseLineId, stocked);
        var restock = await db.InventoryMovements.AsNoTracking().SingleAsync(move => move.OrderReturnId == first.LineId && move.MovementType == InventoryMovementType.CustomerReturn);
        Assert.Equal((1, 1, scenario.Clock.Now.UtcDateTime), (restock.QuantityChange, restock.QuantityAfter, restock.CreatedAt));
        var afterRestock = await ReadReportAsync(scenario.Manager, Day1, Day1.AddDays(2));
        Assert.Equal(300_000m, afterRestock.Summary.NetGrossProfit);
        Assert.Equal(restockDay, afterRestock.Daily[2].Totals);

        scenario.Clock.Now = At(31, 2);
        var last = await RefundOneAsync(2, 0);
        Assert.Equal(last.CaseLineId, await InspectAsync(scenario, last.CaseId, "Defective"));
        Assert.False(await db.InventoryMovements.AnyAsync(move => move.OrderReturnId == last.LineId && move.MovementType == InventoryMovementType.CustomerReturn));
        var remaining = await StockAsync(scenario, product.Id);
        Assert.Equal((1, 0), (remaining.Quantity, remaining.ReservedQuantity));
        Assert.Equal(OrderStatus.Refunded, await db.Orders.Where(o => o.Id == sale.Id).Select(o => o.Status).SingleAsync());

        await AssertClosingAsync(scenario.Seller, sellingShift.Id, 3_980_000);
        await AssertClosingAsync(scenario.Manager, payingShift.Id, 1_020_000);

        var period = await ReadReportAsync(scenario.Manager, Day1, Day1.AddDays(3));
        FinancialReportTotalsResponse[] expectedDays = [delivered, refundDay, restockDay, refundDay];
        Assert.Equal(expectedDays, period.Daily.Select(day => day.Totals));
        var expectedPeriod = delivered with { RefundPaidAmount = 1_980_000, RefundedSalesExcludingTax = 1_800_000,
            RestockedCost = 600_000, RefundTaxAmount = 180_000, CashRefundPaidAmount = 1_980_000 };
        Assert.Equal(expectedPeriod, period.Summary);
        var actualDays = period.Daily.Select(day => Amounts(day.Totals)).ToArray();
        var columns = Amounts(period.Summary);
        Assert.Equal(columns, Enumerable.Range(0, columns.Length).Select(column => (decimal?)actualDays.Sum(row => row[column]!.Value)));
        foreach (var day in period.Daily)
            Assert.Equal(day.Totals, (await ReadReportAsync(scenario.Manager, day.Date, day.Date)).Summary);
        Assert.Equal(EmptyTotals, (await ReadReportAsync(scenario.Manager, Day1.AddDays(4), Day1.AddDays(4))).Summary);
        var ranked = Assert.Single(period.TopProductsByQuantity);
        Assert.Equal((product.Id, 2L, 1_800_000m), (ranked.ProductId, ranked.Quantity, ranked.SalesExcludingTax));
        Assert.Equal(period.TopProductsByQuantity, period.TopProductsByRevenue);
        Assert.Equal(saved.CashierUserId, Assert.Single(period.CashierPerformance).CashierUserId);
        Assert.Equal(new FinancialReportCashShiftResponse(sellingShift.Id, 1_980_000, 0),
            Assert.Single(period.CashByShift, row => row.PosSessionId == sellingShift.Id));
        Assert.Equal(new FinancialReportCashShiftResponse(payingShift.Id, 0, 1_980_000),
            Assert.Single(period.CashByShift, row => row.PosSessionId == payingShift.Id));
        Assert.Equal(2, period.CashByShift.Count);
        Assert.Equal(1, await db.Orders.CountAsync(order => order.CheckoutKey == checkout.CheckoutKey));
        Assert.Equal(2, await db.OrderReturns.CountAsync(row => row.OrderId == sale.Id));
        Assert.Equal(3, await db.CustomerLoyaltyPoints.CountAsync(point => point.CustomerId == customer.Id));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(move => move.OrderItemId == line.Id && move.MovementType == InventoryMovementType.Sale));

        Write("cash-lifecycle", new { Receipt = receipt.Id, PurchaseMovement = purchaseMovement.Id, Order = sale.Id, SaleLine = line.Id,
            First = new { first.CaseId, first.CaseLineId, first.LineId }, Last = new { last.CaseId, last.CaseLineId, last.LineId },
            Restock = restock.Id, SellerShift = sellingShift.Id, ManagerShift = payingShift.Id,
            ActualDays = period.Daily, ActualPeriod = period.Summary, period.CashByShift, period.AsOf });

        async Task<(Guid CaseId, Guid CaseLineId, Guid LineId)> RefundOneAsync(int refundCount, int pointsBalance)
        {
            var request = ReturnFor(sale);
            var claim = await PostAsync<ReturnDto>(scenario.Seller, ReturnPath, request);
            Assert.Equal(claim.Id, (await PostAsync<ReturnDto>(scenario.Seller, ReturnPath, request)).Id);
            var paid = await SettleCashAsync(scenario.Manager, claim.Id);
            Assert.Equal(paid.RefundedAt, (await SettleCashAsync(scenario.Manager, claim.Id)).RefundedAt);
            var lines = await db.OrderReturns.AsNoTracking().Where(row => row.OrderId == sale.Id).OrderBy(row => row.RefundedAt).ToArrayAsync();
            Assert.Equal(refundCount, lines.Length);
            var latest = lines[^1];
            Assert.Equal((990_000m, 900_000m, 90_000m, 100_000m),
                (latest.RefundAmount, latest.NetAmount, latest.TaxAmount, latest.DiscountAmount));
            Assert.Equal((payingShift.Id, scenario.Clock.Now.UtcDateTime), (latest.RefundPosSessionId, latest.RefundedAt));
            var cash = await db.Set<CashRefundPayout>().AsNoTracking().SingleAsync(row => row.RefundId == claim.Id);
            Assert.Equal((payingShift.Id, 990_000m), (cash.ShiftId, cash.Amount));
            var loyalty = await db.CustomerLoyaltyPoints.AsNoTracking().SingleAsync(row => row.RefundCaseId == claim.Id);
            Assert.Equal((-9, pointsBalance), (loyalty.PointsChange, loyalty.BalanceAfter));
            Assert.Equal(pointsBalance, await db.Customers.Where(row => row.Id == customer.Id).Select(row => row.LoyaltyPoints).SingleAsync());
            return (claim.Id, latest.CaseLineId!.Value, latest.Id);
        }
    }

    private static readonly FinancialReportTotalsResponse EmptyTotals = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static decimal?[] Amounts(FinancialReportTotalsResponse row) =>
        [row.CompletedOrderCount, row.Quantity, row.AllocationMismatchOrderCount, row.SalesExcludingTax, row.SalesTax,
            row.PaymentReceivedAmount, row.CostOfGoodsSold, row.SalesGrossProfit, row.RefundPaidAmount,
            row.RefundedSalesExcludingTax, row.RestockedCost, row.WarrantyCost, row.RefundTaxAmount, row.CashReceivedAmount,
            row.CashRefundPaidAmount, row.VietQrReceivedAmount, row.VietQrRefundPaidAmount, row.NetSalesExcludingTax,
            row.NetCostOfGoodsSold, row.NetGrossProfit];

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2025, 10, day, hour, minute, 0, TimeSpan.Zero);

    private static string Range(DateOnly from, DateOnly to) => string.Create(CultureInfo.InvariantCulture,
        $"{ReportPath}?period=custom&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    private static async Task<FinancialReportResponse> ReadReportAsync(HttpClient manager, DateOnly from, DateOnly to)
    {
        var report = await ReadAsync<FinancialReportResponse>(manager, Range(from, to));
        Assert.Equal(("custom", "UTC+07:00", from, to), (report.Period, report.TimeZone, report.StartDate, report.EndDate));
        Assert.Equal(DateTimeKind.Utc, report.AsOf.Kind);
        Assert.Equal(Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays), report.Daily.Select(day => day.Date));
        return report;
    }

    private static void AssertDataEqual(FinancialReportResponse before, FinancialReportResponse after)
    {
        Assert.Equal(before.Summary, after.Summary);
        Assert.Equal(before.Daily, after.Daily);
        Assert.Equal(before.CashByShift, after.CashByShift);
        Assert.Equal(before.TopProductsByQuantity, after.TopProductsByQuantity);
        Assert.Equal(before.TopProductsByRevenue, after.TopProductsByRevenue);
        Assert.Equal(before.CashierPerformance, after.CashierPerformance);
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        await AssertStatusAsync(response, route, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string route, object request, HttpStatusCode? expected = null)
    {
        using var response = await client.PostAsJsonAsync(route, request);
        await AssertStatusAsync(response, route, expected);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, string route, HttpStatusCode? expected = null)
    {
        if (expected is { } status ? response.StatusCode != status : !response.IsSuccessStatusCode)
            Assert.Fail($"{route} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<ProductDto> CreateProductAsync(Scenario scenario, bool tracked = false)
    {
        var key = Guid.NewGuid().ToString("N")[..12];
        var category = await PostAsync<CategoryDto>(scenario.Admin, "/api/categories",
            new CreateCategoryRequest { Name = "Reconciliation " + key, Slug = "reconcile-" + key, IsActive = true });
        var product = await PostAsync<ProductDto>(scenario.Admin, "/api/products", new CreateProductRequest
        {
            CategoryId = category.Id, Name = "Reconciliation fixture", Sku = "RECON-" + key, Brand = "Fixture",
            BasePrice = 1_000_000, CostPrice = 600_000, IsSerialTracked = tracked
        });
        using var activate = await scenario.Admin.PatchAsJsonAsync($"/api/products/{product.Id}/status", new UpdateProductStatusRequest { IsActive = true });
        await AssertStatusAsync(activate, "/api/products/{id}/status");
        return product;
    }

    private static async Task<CreatePurchaseReceiptRequest> ReceiptAsync(Scenario scenario, Guid product, int quantity)
    {
        await using var db = scenario.Api.Database.CreateContext();
        return new()
        {
            ClientRequestId = Guid.NewGuid(), SupplierId = await db.Suppliers.Where(row => row.IsActive).Select(row => row.Id).FirstAsync(),
            Lines = [new() { ProductId = product, Quantity = quantity, UnitCost = 600_000 }]
        };
    }

    private static async Task<InventoryStock> StockAsync(Scenario scenario, Guid product)
    {
        await using var db = scenario.Api.Database.CreateContext();
        return await db.InventoryStocks.AsNoTracking().SingleAsync(row => row.ProductId == product && row.VariantId == null);
    }

    private static Task<CustomerDto> CreateCustomerAsync(Scenario scenario) => PostAsync<CustomerDto>(scenario.Seller, "/api/customers",
        new CreateCustomerRequest { FullName = "Reconciliation fixture", Phone = "0900086421" });

    private static PosCheckoutRequest Checkout(Guid shift, Guid product, int quantity = 1,
        Guid? customer = null, decimal discount = 0, Guid? serial = null) => new()
    {
        CheckoutKey = Guid.NewGuid(), PosSessionId = shift, CustomerId = customer, OrderDiscountAmount = discount, CashTendered = 2_000_000,
        Items = [new() { ProductId = product, Quantity = quantity, SerialImeiId = serial }]
    };

    private static Task<PosShiftDto> OpenShiftAsync(HttpClient client, decimal opening) =>
        PostAsync<PosShiftDto>(client, ShiftPath, new OpenShiftRequest { OpeningBalance = opening }, HttpStatusCode.Created);

    private static async Task AssertClosingAsync(HttpClient client, Guid shift, decimal expected)
    {
        var closed = await PostAsync<PosShiftDto>(client, $"{ShiftPath}/{shift}/close", new CloseShiftRequest { ClosingBalance = expected });
        Assert.Equal((expected, expected, 0m), (closed.ExpectedClosingBalance, closed.ClosingBalance, closed.Discrepancy));
        Assert.Equal("Closed", closed.Status);
    }

    private static CreateReturnRequest ReturnFor(OrderDto order)
    {
        var line = Assert.Single(order.Items);
        return new(Guid.NewGuid(), order.Id, "Reconciliation return", [new(line.Id, 1, line.SerialImeiId)]);
    }

    private static Task<ReturnDto> SettleCashAsync(HttpClient client, Guid claim) =>
        PostAsync<ReturnDto>(client, $"{ReturnPath}/{claim}/settle", new SettleReturnRequest("Cash"));

    private static async Task<Guid> InspectAsync(Scenario scenario, Guid claim, string disposition)
    {
        var work = await ReadAsync<List<ReturnInspectionDto>>(scenario.Warehouse, "/api/inventory/returns");
        var line = Assert.Single(work, row => row.RequestId == claim);
        var path = $"/api/inventory/returns/{line.ReturnLineId}/inspections";
        var operation = new InspectReturnRequest(Guid.NewGuid(), 1, disposition);
        var first = await PostAsync<ReturnInspectionDto>(scenario.Warehouse, path, operation);
        var replay = await PostAsync<ReturnInspectionDto>(scenario.Warehouse, path, operation);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(replay));
        return line.ReturnLineId;
    }

    private void Write(string label, object result) => output.WriteLine($"{label} {JsonSerializer.Serialize(result)}");

    private static HttpClient NewClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });

    private sealed class ScenarioClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = At(28, 1);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Scenario(IdentityPostgresApi api, ScenarioClock clock, WebApplicationFactory<Program> host) : IAsyncDisposable
    {
        public IdentityPostgresApi Api { get; } = api;
        public ScenarioClock Clock { get; } = clock;
        public WebApplicationFactory<Program> Host { get; } = host;
        public HttpClient Admin { get; } = NewClient(host);
        public HttpClient Manager { get; } = NewClient(host);
        public HttpClient Seller { get; } = NewClient(host);
        public HttpClient Warehouse { get; } = NewClient(host);

        public static async Task<Scenario> StartAsync(Action<IServiceCollection>? configure = null)
        {
            var api = await IdentityPostgresApi.CreateAsync();
            var clock = new ScenarioClock();
            var host = api.Factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Pricing:VatRatePercent", "10");
                builder.UseSetting("Pricing:SalesStaffMaxDiscountPercent", "10");
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<TimeProvider>(clock);
                    configure?.Invoke(services);
                });
            });
            var scenario = new Scenario(api, clock, host);
            try
            {
                await scenario.LoginAsync(scenario.Admin, "admin", "Admin@123", RoleType.Admin);
                await scenario.LoginAsync(scenario.Manager, "manager1", "Manager@123", RoleType.Manager);
                await scenario.LoginAsync(scenario.Seller, "cashier1", "Cashier@123", RoleType.SalesStaff);
                await scenario.LoginAsync(scenario.Warehouse, "warehouse1", "Warehouse@123", RoleType.WarehouseStaff);
                return scenario;
            }
            catch
            {
                await scenario.DisposeAsync();
                throw;
            }
        }

        public async Task LoginAsync(HttpClient target, string username, string password, RoleType role)
        {
            // Issue JWTs with the real identity clock while the transaction host uses historical event times.
            using var identity = Api.Factory.NewClient();
            Assert.Equal(role, (await IdentityPostgresApi.LoginAsync(identity, username, password)).User.Role);
            target.DefaultRequestHeaders.Authorization = identity.DefaultRequestHeaders.Authorization;
        }

        public async ValueTask DisposeAsync()
        {
            Admin.Dispose();
            Manager.Dispose();
            Seller.Dispose();
            Warehouse.Dispose();
            try { await Host.DisposeAsync(); }
            finally { await Api.DisposeAsync(); }
        }
    }
}
