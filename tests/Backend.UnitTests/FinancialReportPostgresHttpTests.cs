using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class FinancialReportPostgresHttpTests
{
    private const string Route = "/api/analytics/financial-report";
    private const string FixtureRange = "?period=custom&from=2024-10-12&to=2024-10-14";
    private static readonly DateTime Start = Utc(11, 17);

    [PostgresFact]
    public async Task Report_RequiresCurrentJwtAndFinancialReadRole()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        await using (var db = api.Database.CreateContext())
        {
            var cashier = await db.Users.SingleAsync(user => user.Username == "cashier1");
            var product = AddProducts(db, 1)[0];
            db.Orders.Add(Sale(cashier, OrderStatus.Completed, Start, Start, 120, 0, Line(product, 1, 120, cost: 30)));
            await db.SaveChangesAsync();
        }

        using var client = api.Factory.NewClient();
        await IdentityPostgresApi.AssertSafeFailureAsync(await client.GetAsync(Route + FixtureRange), HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-jwt");
        await IdentityPostgresApi.AssertSafeFailureAsync(await client.GetAsync(Route + FixtureRange),
            HttpStatusCode.Unauthorized, "invalid-jwt");

        foreach (var (user, password) in new[] { ("cashier1", "Cashier@123"), ("warehouse1", "Warehouse@123") })
        {
            await IdentityPostgresApi.LoginAsync(client, user, password);
            await IdentityPostgresApi.AssertSafeFailureAsync(await client.GetAsync(Route + FixtureRange), HttpStatusCode.Forbidden);
        }

        await IdentityPostgresApi.LoginAsync(client);
        var admin = await ReadReportAsync(client, FixtureRange);
        await IdentityPostgresApi.LoginAsync(client, "manager1", "Manager@123");
        var manager = await ReadReportAsync(client, FixtureRange);
        AssertTotals(admin.Summary, 1, 1, 120, 0, 120, 30, 90, 0);
        Assert.Equal(admin.Summary, manager.Summary);
        Assert.Equal(admin.Daily.ToArray(), manager.Daily.ToArray());
        Assert.Equal(admin.CashierPerformance.ToArray(), manager.CashierPerformance.ToArray());
    }

    [PostgresFact]
    public async Task Report_ValidatesCustomRangeBeforeAggregation()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        var clock = new ReportClock(Utc(14, 9));
        using var factory = WithClock(api, clock);
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        await IdentityPostgresApi.LoginAsync(client);

        Assert.Single((await ReadReportAsync(client, "?period=custom&from=2024-10-10&to=2024-10-10")).Daily);
        Assert.Equal(31, (await ReadReportAsync(client, "?period=custom&from=2024-08-01&to=2024-08-31")).Daily.Count);
        Assert.Equal(2, clock.SnapshotReads);
        Assert.Equal(2, clock.AggregateReads);

        var invalid = new (string Query, string Field)[]
        {
            ("?from=2024-10-12&to=2024-10-14", "period"),
            ("?period=&from=2024-10-12&to=2024-10-14", "period"),
            ("?period=today&from=2024-10-12&to=2024-10-14", "period"),
            ("?period=Custom&from=2024-10-12&to=2024-10-14", "period"),
            (FixtureRange + "&period=custom", "period"),
            ("?period=custom&to=2024-10-14", "from"),
            ("?period=custom&from=2024-10-12", "to"),
            ("?period=custom&from=&to=2024-10-14", "from"),
            ("?period=custom&from=2024-10-12&to=", "to"),
            (FixtureRange + "&from=2024-10-12", "from"),
            (FixtureRange + "&to=2024-10-14", "to"),
            ("?period=custom&from=2024-9-02&to=2024-10-14", "from"),
            ("?period=custom&from=2024-10-12&to=2024-10-40", "to"),
            ("?period=custom&from=0001-01-01&to=0001-01-01", "from"),
            ("?period=custom&from=9999-12-31&to=9999-12-31", "to"),
            ("?period=custom&from=2024-10-14&to=2024-10-12", "to"),
            ("?period=custom&from=2024-08-01&to=2024-09-01", "to")
        };

        foreach (var (query, field) in invalid)
            await AssertInvalidAsync(client, query, field);

        Assert.Equal(2, clock.SnapshotReads);
        await AssertInvalidAsync(client, "?period=custom&from=2024-10-14&to=2024-10-15", "to");
        Assert.Equal(3, clock.SnapshotReads);
        Assert.Equal(2, clock.AggregateReads);
    }

    [PostgresFact]
    public async Task Report_ReconcilesDailySalesAndReceiptsWithHandCalculatedFixture()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        await using (var db = api.Database.CreateContext())
        {
            var first = await db.Users.SingleAsync(user => user.Username == "cashier1");
            var second = await db.Users.SingleAsync(user => user.Username == "manager1");
            var products = AddProducts(db, 3);
            db.Orders.AddRange(
                Sale(first, OrderStatus.Completed, Start.AddSeconds(-1), Start.AddSeconds(-1), 900, 0, Line(products[0], 1, 900)),
                Sale(first, OrderStatus.Refunded, Start, Utc(11, 16), 198, 18, Line(products[0], 2, 200, 20, 18, 60)),
                Sale(second, OrderStatus.PartiallyRefunded, Utc(13, 18), Utc(12, 1), 198, 18, Line(products[1], 2, 200, 20, 18, null)),
                Sale(first, OrderStatus.Completed, Utc(14, 17).AddSeconds(-1), Utc(14, 16), 122, 12, Line(products[2], 1, 120)),
                Sale(second, OrderStatus.Processing, null, Utc(12, 2), 50, 0, Line(products[2], 1, 50)),
                Sale(first, OrderStatus.Completed, Utc(14, 17), Utc(14, 17), 650, 0, Line(products[2], 1, 650)),
                Sale(first, OrderStatus.Completed, Utc(16, 9), Utc(16, 8), 122, 12, Line(products[2], 1, 120)));
            await db.SaveChangesAsync();
        }

        using var client = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(client);
        using var response = await client.GetAsync(Route + FixtureRange);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var raw = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var report = raw.RootElement.Deserialize<FinancialReportDto>(JsonSerializerOptions.Web)!;

        Assert.Equal("custom", report.Period);
        Assert.Equal(new DateOnly(2024, 10, 12), report.StartDate);
        Assert.Equal(new DateOnly(2024, 10, 14), report.EndDate);
        Assert.Equal("UTC+07:00", report.TimeZone);
        Assert.EndsWith("Z", raw.RootElement.GetProperty("asOf").GetString());
        Assert.True(report.AsOf > Utc(14, 17));
        Assert.Equal(new[] { report.StartDate, report.StartDate.AddDays(1), report.EndDate },
            report.Daily.Select(day => day.Date));
        AssertTotals(report.Summary, 3, 5, 480, 36, 370, null, null, 1);
        AssertTotals(report.Daily[0].Totals, 1, 2, 180, 18, 248, 120, 60, 0);
        AssertTotals(report.Daily[1].Totals, 0, 0, 0, 0, 0, 0, 0, 0);
        AssertTotals(report.Daily[2].Totals, 2, 3, 300, 18, 122, null, null, 1);
        Assert.Equal(report.Summary.SalesExcludingTax, report.Daily.Sum(day => day.Totals.SalesExcludingTax));
        Assert.Equal(report.Summary.SalesTax, report.Daily.Sum(day => day.Totals.SalesTax));
        Assert.Equal(report.Summary.PaymentReceivedAmount, report.Daily.Sum(day => day.Totals.PaymentReceivedAmount));
        AssertChangeTwoNulls(raw.RootElement.GetProperty("summary"));
        foreach (var day in raw.RootElement.GetProperty("daily").EnumerateArray())
            AssertChangeTwoNulls(day.GetProperty("totals"));

        Assert.Equal(new[] { ProductId(1), ProductId(2), ProductId(3) }, report.TopProductsByQuantity.Select(row => row.ProductId));
        Assert.Equal(report.TopProductsByQuantity.ToArray(), report.TopProductsByRevenue.ToArray());
        Assert.Collection(report.CashierPerformance,
            row => Assert.Equal((2L, 3L, 300m, (decimal?)120, (decimal?)180),
                (row.CompletedOrderCount, row.Quantity, row.SalesExcludingTax, row.CostOfGoodsSold, row.SalesGrossProfit)),
            row => Assert.Equal((1L, 2L, 180m, (decimal?)null, (decimal?)null),
                (row.CompletedOrderCount, row.Quantity, row.SalesExcludingTax, row.CostOfGoodsSold, row.SalesGrossProfit)));

        var nextDay = await ReadReportAsync(client, "?period=custom&from=2024-10-15&to=2024-10-15");
        AssertTotals(nextDay.Summary, 1, 1, 650, 0, 650, 0, 650, 0);
        var zeroCost = await ReadReportAsync(client, "?period=custom&from=2024-10-16&to=2024-10-16");
        AssertTotals(zeroCost.Summary, 1, 1, 120, 0, 122, 0, 120, 1);

        var dashboard = await client.GetFromJsonAsync<AdminDashboardDto>("/api/analytics/dashboard" + FixtureRange);
        Assert.Equal(122, dashboard!.Summary.Revenue);
    }

    [PostgresFact]
    public async Task Report_RanksProductsAndCashiersWithIndependentClocksAndStableTies()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        var asOf = Utc(12, 9);
        var clock = new ReportClock(asOf);
        Guid activeId;
        Guid inactiveId;
        await using (var db = api.Database.CreateContext())
        {
            var active = await db.Users.SingleAsync(user => user.Username == "cashier1");
            var inactive = await db.Users.SingleAsync(user => user.Username == "warehouse1");
            inactive.FullName = active.FullName = "Cùng tên";
            inactive.IsActive = false;
            activeId = active.Id;
            inactiveId = inactive.Id;
            var products = AddProducts(db, 12);
            var variant = new ProductVariant { Product = products[0], Sku = "FR-variant", VariantName = "Blue", Price = 999 };
            var firstLine = Line(products[0], 6, 6, cost: 0);
            firstLine.Variant = variant;
            firstLine.Id = ProductId(101);
            firstLine.ProductName = "Tên cũ";
            var secondLine = Line(products[0], 4, 4, cost: 0);
            secondLine.Id = ProductId(102);
            secondLine.ProductName = "Tên snapshot";
            db.Orders.AddRange(
                Sale(active, OrderStatus.Completed, asOf.AddMinutes(-1), asOf.AddMinutes(-3), 10, 0, firstLine, secondLine),
                Sale(inactive, OrderStatus.Completed, asOf.AddMinutes(-2), asOf.AddMinutes(-2), 100, 0, Line(products[1], 1, 100, cost: null)),
                Sale(active, OrderStatus.Completed, asOf, asOf, 999, 0, Line(products[2], 99, 999)),
                Sale(active, OrderStatus.Completed, asOf.AddSeconds(1), asOf.AddSeconds(1), 888, 0, Line(products[3], 88, 888)));
            foreach (var product in products.Skip(2))
                db.Orders.Add(Sale(active, OrderStatus.Completed, asOf.AddMinutes(-2), asOf.AddMinutes(-2), 15, 0, Line(product, 2, 15)));
            await db.SaveChangesAsync();
        }

        using var factory = WithClock(api, clock);
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        await IdentityPostgresApi.LoginAsync(client);
        var report = await ReadReportAsync(client, "?period=custom&from=2024-10-12&to=2024-10-12");
        Assert.Equal(asOf, report.AsOf);
        AssertTotals(report.Summary, 12, 31, 260, 0, 260, null, null, 0);
        Assert.Equal(Enumerable.Range(3, 9).Prepend(1).Select(ProductId), report.TopProductsByQuantity.Select(row => row.ProductId));
        Assert.Equal(Enumerable.Range(3, 9).Prepend(2).Select(ProductId), report.TopProductsByRevenue.Select(row => row.ProductId));
        Assert.Equal(new FinancialReportProductDto(ProductId(1), "Tên snapshot", 10, 10), report.TopProductsByQuantity[0]);
        Assert.Equal(new[] { activeId, inactiveId }, report.CashierPerformance.Select(row => row.CashierUserId));
        Assert.All(report.CashierPerformance, row => Assert.Equal("Cùng tên", row.CashierName));
        Assert.Equal(11L, report.CashierPerformance[0].CompletedOrderCount);
        Assert.Equal(0m, report.CashierPerformance[0].CostOfGoodsSold);
        Assert.Null(report.CashierPerformance[1].CostOfGoodsSold);

        var empty = await ReadReportAsync(client, "?period=custom&from=2024-10-10&to=2024-10-10");
        Assert.Empty(empty.TopProductsByQuantity);
        Assert.Empty(empty.TopProductsByRevenue);
        Assert.Empty(empty.CashierPerformance);
    }

    private static async Task AssertInvalidAsync(HttpClient client, string query, string field)
    {
        using var response = await client.GetAsync(Route + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(400, problem!.Status);
        Assert.True(problem.Errors.ContainsKey(field), $"Expected an error on {field} for {query}.");
    }

    private static async Task<FinancialReportDto> ReadReportAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(Route + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FinancialReportDto>())!;
    }

    private static void AssertChangeTwoNulls(JsonElement totals)
    {
        foreach (var field in new[] { "refundPaidAmount", "refundedSalesExcludingTax", "restockedCost", "warrantyCost",
                     "netSalesExcludingTax", "netCostOfGoodsSold", "netGrossProfit" })
            Assert.Equal(JsonValueKind.Null, totals.GetProperty(field).ValueKind);
    }

    private static void AssertTotals(FinancialReportTotalsDto actual, long orders, long quantity, decimal revenue,
        decimal tax, decimal receipts, decimal? cost, decimal? profit, long mismatches)
        => Assert.Equal(new FinancialReportTotalsDto(orders, quantity, mismatches, revenue, tax, receipts, cost, profit), actual);

    private static Product[] AddProducts(AppDbContext db, int count)
    {
        var category = new Category { Name = "Financial fixtures", Slug = "financial-fixtures" };
        var products = Enumerable.Range(1, count).Select(number => new Product
        {
            Id = ProductId(number), Category = category, Name = $"Catalog {number}",
            Sku = $"FR-{number}", Brand = "Financial fixtures", BasePrice = 999, CostPrice = 999
        }).ToArray();
        db.AddRange(products);
        return products;
    }

    private static Guid ProductId(int number) => new($"00000000-0000-0000-0000-{number:D12}");

    private static OrderItem Line(Product product, int quantity, decimal price, decimal discount = 0, decimal tax = 0, decimal? cost = 0)
        => new() { Product = product, ProductName = $"Snapshot {product.Sku}", Sku = product.Sku, Quantity = quantity,
            UnitPrice = price / quantity, TotalPrice = price, OrderDiscountAmount = discount, TaxAmount = tax, UnitCost = cost };

    private static Order Sale(User cashier, OrderStatus status, DateTime? completedAt, DateTime paidAt,
        decimal amount, decimal tax, params OrderItem[] items)
        => new() { OrderCode = $"FR-{Guid.NewGuid():N}", CashierUserId = cashier.Id, Status = status,
            CreatedAt = paidAt.AddDays(-1), PaidAt = paidAt, CompletedAt = completedAt,
            Subtotal = items.Sum(item => item.TotalPrice), TotalAmount = amount, TaxAmount = tax, Items = items };

    private static DateTime Utc(int day, int hour) => new(2024, 10, day, hour, 0, 0, DateTimeKind.Utc);

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithClock(IdentityPostgresApi api, ReportClock clock)
        => api.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAnalyticsRepository>();
            services.AddScoped<AnalyticsRepository>();
            services.AddScoped<IAnalyticsRepository>(provider => new ClockedRepository(provider.GetRequiredService<AnalyticsRepository>(), clock));
        }));

    private sealed class ReportClock(DateTime asOf)
    {
        public DateTime AsOf { get; } = asOf;
        public int SnapshotReads { get; set; }
        public int AggregateReads { get; set; }
    }

    private sealed class ClockedRepository(AnalyticsRepository inner, ReportClock clock) : IAnalyticsRepository
    {
        public async Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default)
        {
            await inner.GetSnapshotTimeUtcAsync(cancellationToken);
            clock.SnapshotReads++;
            return clock.AsOf;
        }

        public Task<DashboardAnalyticsReadModel> GetDashboardAsync(DashboardAnalyticsCriteria criteria, CancellationToken cancellationToken = default)
            => inner.GetDashboardAsync(criteria, cancellationToken);

        public Task<FinancialReportReadModel> GetFinancialReportAsync(FinancialReportCriteria criteria, CancellationToken cancellationToken = default)
        {
            clock.AggregateReads++;
            return inner.GetFinancialReportAsync(criteria, cancellationToken);
        }
    }
}
