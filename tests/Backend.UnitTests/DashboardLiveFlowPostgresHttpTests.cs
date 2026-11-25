using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.UnitTests;

public sealed class DashboardLiveFlowPostgresHttpTests(ITestOutputHelper output)
{
    private const string Sku = "DEMO-TAB-IPAD-AIR";
    private const string Imei = "490000000000004";
    private const string Customer = "Demo Customer 01";
    private const decimal OpeningBalance = 2_000_000m;
    private const decimal CashTendered = 20_000_000m;
    private const decimal UnitPrice = 16_990_000m;
    private const decimal LineDiscount = 90_000m;
    private const decimal Subtotal = 16_900_000m;
    private const decimal Vat = 1_690_000m;
    private const decimal Total = 18_590_000m;
    private static readonly DateTimeOffset SoldAt = new(2026, 10, 20, 17, 1, 0, TimeSpan.Zero);
    private static readonly DateOnly SaleDate = new(2026, 10, 21);
    private const string Period = "/api/analytics/dashboard?period=custom&from=2026-10-20&to=2026-10-21";

    [PostgresFact]
    public async Task CashCheckout_AndClosedShift_UpdateManagerDashboardOnVietnamDate()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        var clock = new FlowClock { UtcNow = SoldAt.AddSeconds(-30) };
        await using var host = BuildHost(api, clock);
        using var manager = NewClient(host);
        using var cashier = NewClient(host);
        Assert.Equal(RoleType.Manager, (await IdentityPostgresApi.LoginAsync(manager, "manager1", "Manager@123")).User.Role);
        var seller = (await IdentityPostgresApi.LoginAsync(cashier, "cashier1", "Cashier@123")).User;

        var before = await GetAsync<AdminDashboardDto>(manager, Period);
        Write("before", before);
        AssertSnapshot(before, clock);
        Assert.Equal(0, before.Summary.OrderCount);
        Assert.Equal(0m, before.Summary.Revenue);

        clock.UtcNow = SoldAt.AddSeconds(-15);
        var shift = await OpenShiftAsync(cashier);
        Assert.Equal("Open", shift.Status);
        Assert.Equal(seller.Id, shift.CashierUserId);
        Assert.Equal(0m, shift.CashSalesTotal);
        var (product, serial, customer) = await FindSaleInputsAsync(cashier);
        var initialStock = await ReadStockAsync(api, serial);
        Assert.True(initialStock.Quantity - initialStock.ReservedQuantity >= 1);

        clock.UtcNow = SoldAt;
        var request = Checkout(shift, serial, customer);
        Write("checkout-input", new { product.Sku, serial.Imei, customer.Id, request });
        var order = await PostAsync<PosCheckoutRequest, OrderDto>(cashier, "/api/pos/checkout", request, HttpStatusCode.Created);
        var readBack = await GetAsync<OrderDto>(cashier, $"/api/orders/{order.Id}");
        Assert.Equal(order.OrderCode, readBack.OrderCode);
        Assert.Equal(order.TotalAmount, readBack.TotalAmount);
        Assert.Equal(order.CompletedAt, readBack.CompletedAt);

        await using var db = api.Database.CreateContext();
        var saved = await db.Orders.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == order.Id);
        var line = Assert.Single(saved.Items);
        Assert.Equal(OrderStatus.Completed, saved.Status);
        Assert.Equal(PaymentMethod.Cash, saved.PaymentMethod);
        Assert.Equal(SoldAt.UtcDateTime, saved.CreatedAt);
        Assert.Equal(SoldAt.UtcDateTime, saved.CompletedAt);
        Assert.Equal(saved.CompletedAt, saved.PaidAt);
        Assert.Equal(customer.Id, saved.CustomerId);
        Assert.Equal(seller.Id, saved.CashierUserId);
        Assert.Equal(shift.Id, saved.PosSessionId);
        Assert.Equal(request.CheckoutKey, saved.CheckoutKey);
        Assert.Equal(serial.Id, line.SerialImeiId);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(serial.VariantId, line.VariantId);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(UnitPrice, line.UnitPrice);
        Assert.Equal(LineDiscount, line.DiscountAmount);
        Assert.Equal(Subtotal, saved.Subtotal);
        Assert.Equal(0m, saved.DiscountAmount);
        Assert.Equal(Vat, saved.TaxAmount);
        Assert.Equal(Vat, line.TaxAmount);
        Assert.Equal(Total, saved.TotalAmount);
        Assert.Equal(saved.TotalAmount, readBack.TotalAmount);
        Assert.Equal(CashTendered, saved.CashTendered);
        Assert.Equal(CashTendered - saved.TotalAmount, saved.ChangeGiven);

        var sold = await db.SerialImeis.AsNoTracking().SingleAsync(x => x.Id == serial.Id);
        var stock = await ReadStockAsync(api, serial);
        var sale = await db.InventoryMovements.AsNoTracking().SingleAsync(x => x.OrderItemId == line.Id);
        var credited = await db.PosSessions.AsNoTracking().SingleAsync(x => x.Id == shift.Id);
        Assert.Equal(SerialImeiStatus.Sold, sold.Status);
        Assert.Equal(saved.CompletedAt, sold.WarrantyStartAt);
        Assert.Equal(SoldAt.UtcDateTime.AddMonths(serial.WarrantyMonths), sold.WarrantyEndAt);
        Assert.Equal(14_500_000m, sold.UnitCost);
        Assert.Equal(sold.UnitCost, line.UnitCost);
        Assert.Equal(initialStock.Quantity - 1, stock.Quantity);
        Assert.Equal(initialStock.ReservedQuantity, stock.ReservedQuantity);
        Assert.Equal(InventoryMovementType.Sale, sale.MovementType);
        Assert.Equal(stock.Id, sale.InventoryStockId);
        Assert.Equal(-1, sale.QuantityChange);
        Assert.Equal(stock.Quantity, sale.QuantityAfter);
        Assert.Equal(seller.Id, sale.PerformedByUserId);
        Assert.Equal(saved.TotalAmount, credited.CashSalesTotal);

        clock.UtcNow = SoldAt.AddMinutes(1);
        var close = await PostAsync<CloseShiftRequest, PosShiftDto>(cashier, $"/api/pos/shifts/{shift.Id}/close",
            new() { ClosingBalance = OpeningBalance + Total }, HttpStatusCode.OK);
        var persistedShift = await db.PosSessions.AsNoTracking().SingleAsync(x => x.Id == shift.Id);
        Assert.Equal("Closed", close.Status);
        Assert.Equal(PosSessionStatus.Closed, persistedShift.Status);
        Assert.Equal(seller.Id, persistedShift.ClosedByUserId);
        Assert.Equal(clock.UtcNow.UtcDateTime, persistedShift.ClosedAt);
        Assert.Equal(OpeningBalance + saved.TotalAmount, close.ExpectedClosingBalance);
        Assert.Equal(close.ExpectedClosingBalance, persistedShift.ExpectedClosingBalance);
        Assert.Equal(close.ExpectedClosingBalance, persistedShift.ClosingBalance);
        Assert.Equal(0m, close.Discrepancy);

        clock.UtcNow = SoldAt.AddMinutes(2);
        var after = await GetAsync<AdminDashboardDto>(manager, Period);
        Write("after", after);
        Write("committed", new
        {
            order.Id, order.OrderCode, saved.PaidAt, saved.CompletedAt, saved.Subtotal, saved.DiscountAmount,
            saved.TaxRate, saved.TaxAmount, saved.TotalAmount, saved.CashTendered, saved.ChangeGiven, line.UnitPrice,
            LineDiscount = line.DiscountAmount, line.UnitCost, line.SerialImeiId, sold.Status, sold.WarrantyStartAt,
            sold.WarrantyEndAt, BeforeQuantity = initialStock.Quantity, AfterQuantity = stock.Quantity,
            stock.ReservedQuantity, SaleId = sale.Id, sale.QuantityChange, sale.QuantityAfter, close
        });
        AssertSnapshot(after, clock);
        Assert.True(after.AsOf > saved.CompletedAt);
        Assert.Equal(1, after.Summary.OrderCount - before.Summary.OrderCount);
        Assert.Equal(saved.TotalAmount, after.Summary.Revenue - before.Summary.Revenue);
        Assert.Equal(before.Summary.ActiveProductCount, after.Summary.ActiveProductCount);
        Assert.Equal(await db.InventoryStocks.CountAsync(x => x.Quantity <= x.MinStockAlert), after.Summary.LowStockCount);
        Assert.NotEqual(DateOnly.FromDateTime(saved.CompletedAt!.Value), SaleDate);
        Assert.Equal(SaleDate, DateOnly.FromDateTime(saved.CompletedAt.Value.AddHours(7)));
        Assert.Equal(SaleDate, DateOnly.FromDateTime(saved.CreatedAt.AddHours(7)));
        Assert.Equal(before.DailyRevenue[0], after.DailyRevenue[0]);
        Assert.Equal(saved.TotalAmount, after.DailyRevenue[1].Revenue - before.DailyRevenue[1].Revenue);
        Assert.InRange(after.RecentOrders.Count, 1, 5);
        var recent = Assert.Single(after.RecentOrders, x => x.Id == saved.Id);
        Assert.Equal(saved.OrderCode, recent.OrderCode);
        Assert.Equal(saved.TotalAmount, recent.TotalAmount);
        Assert.Equal("Completed", recent.Status);
        Assert.Equal(saved.CreatedAt, recent.CreatedAt);
    }

    [PostgresFact]
    public async Task CheckoutWithNewKey_ForSoldDevice_LeavesManagerDashboardAndStoresUnchanged()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        var clock = new FlowClock { UtcNow = SoldAt.AddSeconds(-15) };
        await using var host = BuildHost(api, clock);
        using var manager = NewClient(host);
        using var cashier = NewClient(host);
        await IdentityPostgresApi.LoginAsync(manager, "manager1", "Manager@123");
        await IdentityPostgresApi.LoginAsync(cashier, "cashier1", "Cashier@123");
        var shift = await OpenShiftAsync(cashier);
        var (_, serial, customer) = await FindSaleInputsAsync(cashier);

        clock.UtcNow = SoldAt;
        var first = Checkout(shift, serial, customer);
        var order = await PostAsync<PosCheckoutRequest, OrderDto>(cashier, "/api/pos/checkout", first, HttpStatusCode.Created);
        clock.UtcNow = SoldAt.AddMinutes(1);
        var before = await GetAsync<AdminDashboardDto>(manager, Period);
        var committed = await CaptureAsync(api, serial, shift.Id, order.Id);
        Assert.Contains("Sold", committed);
        Assert.Contains("Open", committed);
        Assert.Equal(order.Id, Assert.Single(before.RecentOrders).Id);
        AssertSnapshot(before, clock);

        clock.UtcNow = SoldAt.AddMinutes(2);
        var second = Checkout(shift, serial, customer);
        Assert.NotEqual(first.CheckoutKey, second.CheckoutKey);
        Write("conflict-input", new { serial.Imei, OrderId = order.Id, second });
        using var response = await cashier.PostAsJsonAsync("/api/pos/checkout", second);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Sold", problem.Detail ?? "", StringComparison.OrdinalIgnoreCase);

        await using var db = api.Database.CreateContext();
        Assert.False(await db.Orders.AnyAsync(x => x.CheckoutKey == second.CheckoutKey));
        var rolledBack = await CaptureAsync(api, serial, shift.Id, order.Id);
        Assert.Equal(committed, rolledBack);
        var after = await GetAsync<AdminDashboardDto>(manager, Period);
        AssertSnapshot(after, clock);
        Assert.True(after.AsOf > before.AsOf);
        Assert.Equal(before.Summary, after.Summary);
        Assert.Equal(before.DailyRevenue.ToArray(), after.DailyRevenue.ToArray());
        Assert.Equal(before.RecentOrders.ToArray(), after.RecentOrders.ToArray());
        Assert.Equal(before.LowStockItems.ToArray(), after.LowStockItems.ToArray());
        Write("conflict", new { HttpStatus = (int)response.StatusCode, problem.Detail, BeforeAsOf = before.AsOf, After = after });
        output.WriteLine($"rollback-read-back {rolledBack}");
    }

    private static WebApplicationFactory<Program> BuildHost(IdentityPostgresApi api, FlowClock clock) =>
        api.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Pricing:VatRatePercent", "10");
            builder.UseSetting("Pricing:SalesStaffMaxDiscountPercent", "10");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPosShiftService>();
                services.AddScoped<IPosShiftService>(sp => ActivatorUtilities.CreateInstance<PosShiftService>(sp, clock));
                services.RemoveAll<IPosCheckoutService>();
                services.AddScoped<IPosCheckoutService>(sp => ActivatorUtilities.CreateInstance<PosCheckoutService>(sp, clock));
                services.RemoveAll<IAnalyticsRepository>();
                services.AddScoped<AnalyticsRepository>();
                services.AddScoped<IAnalyticsRepository>(sp => new ClockedAnalytics(sp.GetRequiredService<AnalyticsRepository>(), clock));
            });
        });

    private static HttpClient NewClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<(ProductSummaryDto Product, SerialImeiDto Serial, CustomerSummaryDto Customer)> FindSaleInputsAsync(HttpClient cashier)
    {
        var catalog = await GetAsync<PagedResultDto<ProductSummaryDto>>(cashier, $"/api/products?q={Sku}&isActive=true");
        var product = Assert.Single(catalog.Items, x => x.Sku == Sku);
        Assert.True(product.IsSerialTracked);
        var serials = await GetAsync<PagedResultDto<SerialImeiDto>>(cashier, $"/api/inventory/serials?search={Imei}&status=InStock");
        var serial = Assert.Single(serials.Items, x => x.Imei == Imei);
        Assert.Equal(product.Id, serial.ProductId);
        Assert.Equal(SerialImeiStatus.InStock, serial.Status);
        var customers = await GetAsync<PagedResultDto<CustomerSummaryDto>>(cashier,
            $"/api/customers?q={Uri.EscapeDataString(Customer)}&isActive=true");
        var customer = Assert.Single(customers.Items, x => x.FullName == Customer);
        return (product, serial, customer);
    }

    private static Task<PosShiftDto> OpenShiftAsync(HttpClient cashier) =>
        PostAsync<OpenShiftRequest, PosShiftDto>(cashier, "/api/pos/shifts", new() { OpeningBalance = OpeningBalance }, HttpStatusCode.Created);

    private static PosCheckoutRequest Checkout(PosShiftDto shift, SerialImeiDto serial, CustomerSummaryDto customer) =>
        new()
        {
            PosSessionId = shift.Id, CheckoutKey = Guid.NewGuid(), CustomerId = customer.Id, CashTendered = CashTendered,
            Items = [new() { ProductId = serial.ProductId, VariantId = serial.VariantId, SerialImeiId = serial.Id, Quantity = 1, DiscountAmount = LineDiscount }]
        };

    private static async Task<InventoryStock> ReadStockAsync(IdentityPostgresApi api, SerialImeiDto serial)
    {
        await using var db = api.Database.CreateContext();
        return await db.InventoryStocks.AsNoTracking().SingleAsync(x => x.ProductId == serial.ProductId && x.VariantId == serial.VariantId);
    }

    private static async Task<string> CaptureAsync(IdentityPostgresApi api, SerialImeiDto serial, Guid shiftId, Guid orderId)
    {
        await using var db = api.Database.CreateContext();
        var stock = await ReadStockAsync(api, serial);
        var device = await db.SerialImeis.AsNoTracking().SingleAsync(x => x.Id == serial.Id);
        var shift = await db.PosSessions.AsNoTracking().SingleAsync(x => x.Id == shiftId);
        var order = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId);
        return JsonSerializer.Serialize(new
        {
            stock.Quantity, stock.ReservedQuantity, StockUpdatedAt = stock.UpdatedAt,
            Device = new { Status = device.Status.ToString(), device.WarrantyStartAt, device.WarrantyEndAt, device.UpdatedAt },
            Shift = new { Status = shift.Status.ToString(), shift.CashSalesTotal, shift.VietQrSalesTotal,
                shift.CardSalesTotal, shift.ClosingBalance, shift.ClosedAt, shift.UpdatedAt },
            SavedOrder = new { Status = order.Status.ToString(), order.TotalAmount, order.PaidAt, order.CompletedAt, order.UpdatedAt },
            OrderCount = await db.Orders.CountAsync(),
            Lines = await db.OrderItems.CountAsync(),
            MovementIds = await db.InventoryMovements.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync()
        });
    }

    private static void AssertSnapshot(AdminDashboardDto snapshot, FlowClock clock)
    {
        Assert.Equal("custom", snapshot.Period);
        Assert.Equal("UTC+07:00", snapshot.TimeZone);
        Assert.Equal(SaleDate.AddDays(-1), snapshot.StartDate);
        Assert.Equal(SaleDate, snapshot.EndDate);
        Assert.Equal(clock.UtcNow.UtcDateTime, snapshot.AsOf);
        Assert.Equal(new[] { snapshot.StartDate, snapshot.EndDate }, snapshot.DailyRevenue.Select(x => x.Date));
        Assert.Equal(snapshot.Summary.Revenue, snapshot.DailyRevenue.Sum(x => x.Revenue));
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path) where T : class
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException($"Empty response for {path}.");
    }

    private static async Task<TResponse> PostAsync<TRequest, TResponse>(HttpClient client, string path, TRequest request,
        HttpStatusCode expectedStatus) where TResponse : class
    {
        using var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(expectedStatus, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<TResponse>() ?? throw new InvalidOperationException($"Empty response for {path}.");
    }

    private void Write(string label, object value) => output.WriteLine($"{label} {JsonSerializer.Serialize(value)}");

    private sealed class FlowClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class ClockedAnalytics(IAnalyticsRepository inner, FlowClock clock) : IAnalyticsRepository
    {
        public async Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default)
        {
            await inner.GetSnapshotTimeUtcAsync(cancellationToken);
            return clock.UtcNow.UtcDateTime;
        }

        public Task<DashboardAnalyticsReadModel> GetDashboardAsync(DashboardAnalyticsCriteria criteria,
            CancellationToken cancellationToken = default) => inner.GetDashboardAsync(criteria, cancellationToken);

        public Task<FinancialReportReadModel> GetFinancialReportAsync(FinancialReportCriteria criteria,
            CancellationToken cancellationToken = default) => inner.GetFinancialReportAsync(criteria, cancellationToken);
    }
}
