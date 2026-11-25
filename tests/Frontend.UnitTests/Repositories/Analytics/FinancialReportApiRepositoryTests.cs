using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Development;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Repositories.Analytics;

public sealed class FinancialReportApiRepositoryTests
{
    internal static FinancialReportRange SampleRange => new(new(2024, 10, 12), new(2024, 10, 14));

    [Fact]
    public async Task GetFinancialReportAsync_UsesLiveDiAndIsoGetWithCancellation()
    {
        var handler = new RecordingHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        });
        using var http = new HttpClient(handler);
        using var services = new ServiceCollection().AddSingleton<IApiClient>(new ApiClient(http, new()))
            .AddDashboardAnalytics(new DashboardPreviewOptions(false)).BuildServiceProvider();
        var repository = services.GetRequiredService<IFinancialReportRepository>();
        Assert.IsType<AnalyticsApiRepository>(repository);
        Assert.IsType<AnalyticsApiRepository>(services.GetRequiredService<IAnalyticsRepository>());

        using var cancellation = new CancellationTokenSource();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
            var pending = repository.GetFinancialReportAsync(SampleRange, cancellation.Token);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/analytics/financial-report?period=custom&from=2024-10-12&to=2024-10-14",
                request.RequestUri!.PathAndQuery);
            Assert.Null(request.Body);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.True(request.CancellationToken.IsCancellationRequested);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public async Task GetFinancialReportAsync_MapsSalesDesignJson()
    {
        var snapshot = await ReadAsync(SalesJson);
        Assert.Equal(SampleRange, snapshot.Range);
        Assert.Equal(new DateTimeOffset(2024, 10, 15, 2, 0, 0, TimeSpan.Zero), snapshot.AsOf);
        Assert.Equal(480m, snapshot.Summary.SalesExcludingTax);
        Assert.Equal(36m, snapshot.Summary.SalesTax);
        Assert.Equal(370m, snapshot.Summary.PaymentReceivedAmount);
        Assert.Null(snapshot.Summary.CostOfGoodsSold);
        Assert.Null(snapshot.Summary.SalesGrossProfit);
        Assert.Null(snapshot.Summary.RefundTaxAmount);
        Assert.Null(snapshot.CashByShift);
        Assert.Equal(new DateOnly(2024, 10, 13), snapshot.Daily[1].Date);
        Assert.Equal(0m, snapshot.Daily[1].Totals.CostOfGoodsSold);
        Assert.Equal(new[] { "Sample A", "Sample B", "Sample C" }, snapshot.TopProductsByQuantity.Select(row => row.ProductName));
        Assert.Equal(new[] { "Sample A", "Sample B", "Sample C" }, snapshot.TopProductsByRevenue.Select(row => row.ProductName));
        Assert.Equal(new[] { "Cashier 1", "Cashier 2" }, snapshot.CashierPerformance.Select(row => row.CashierName));
    }

    [Fact]
    public async Task GetFinancialReportAsync_PreservesChangeTwoAmountsAndOrdering()
    {
        var root = JsonNode.Parse(SalesJson)!;
        var totals = root["summary"]!;
        totals["refundTaxAmount"] = 20.25m;
        totals["warrantyCost"] = 40m;
        totals["netGrossProfit"] = -100.75m;
        totals["cashReceivedAmount"] = 300m;
        totals["cashRefundPaidAmount"] = 85m;
        totals["vietQrReceivedAmount"] = null;
        totals["vietQrRefundPaidAmount"] = 10m;
        root["daily"]![0]!["totals"]!["refundTaxAmount"] = 20.25m;
        root["cashByShift"] = JsonNode.Parse("""
            [{"posSessionId":"00000000-0000-0000-0000-000000000040",
              "cashReceivedAmount":300,"cashRefundPaidAmount":85,"cashNetMovement":215}]
            """);
        var rank = root["topProductsByRevenue"]!.AsArray();
        var first = rank[0];
        rank.RemoveAt(0);
        rank.Add(first);
        var snapshot = await ReadAsync(root.ToJsonString());
        Assert.Equal(20.25m, snapshot.Summary.RefundTaxAmount);
        Assert.Equal(20.25m, snapshot.Daily[0].Totals.RefundTaxAmount);
        Assert.Equal(40m, snapshot.Summary.WarrantyCost);
        Assert.Equal(-100.75m, snapshot.Summary.NetGrossProfit);
        Assert.Equal(300m, snapshot.Summary.CashReceivedAmount);
        Assert.Equal(85m, snapshot.Summary.CashRefundPaidAmount);
        Assert.Null(snapshot.Summary.VietQrReceivedAmount);
        Assert.Equal(10m, snapshot.Summary.VietQrRefundPaidAmount);
        Assert.Equal(215m, Assert.Single(snapshot.CashByShift!).CashNetMovement);
        Assert.Equal(new[] { "Sample B", "Sample C", "Sample A" }, snapshot.TopProductsByRevenue.Select(row => row.ProductName));

        root["cashByShift"] = new JsonArray();
        Assert.Empty((await ReadAsync(root.ToJsonString())).CashByShift!);
    }

    [Theory]
    [InlineData(false, "costOfGoodsSold", true)]
    [InlineData(true, "costOfGoodsSold", true)]
    [InlineData(false, "salesExcludingTax", true)]
    [InlineData(true, "salesExcludingTax", true)]
    [InlineData(false, "salesExcludingTax", false)]
    [InlineData(true, "salesExcludingTax", false)]
    public async Task GetFinancialReportAsync_RejectsMissingRequiredOrNullNonNullableTotals(bool daily, string field, bool remove)
    {
        var root = JsonNode.Parse(SalesJson)!;
        var totals = (daily ? root["daily"]![1]!["totals"]! : root["summary"]!).AsObject();
        if (remove) totals.Remove(field);
        else totals[field] = null;
        Assert.Equal(ApiErrorKind.InvalidResponse,
            (await Assert.ThrowsAsync<ApiException>(() => ReadAsync(root.ToJsonString()))).Kind);
    }

    [Theory]
    [InlineData("startDate", "2024-10-11")]
    [InlineData("asOf", "2024-10-15T02:00:00")]
    [InlineData("timeZone", "UTC")]
    [InlineData("period", "today")]
    public async Task GetFinancialReportAsync_RejectsInvalidMetadata(string field, string value)
    {
        var root = JsonNode.Parse(SalesJson)!;
        root[field] = value;
        Assert.Equal(ApiErrorKind.InvalidResponse,
            (await Assert.ThrowsAsync<ApiException>(() => ReadAsync(root.ToJsonString()))).Kind);
    }

    [Fact]
    public async Task GetFinancialReportAsync_RejectsMissingDate()
    {
        var root = JsonNode.Parse(SalesJson)!;
        root["daily"]!.AsArray().RemoveAt(1);
        Assert.Equal(ApiErrorKind.InvalidResponse,
            (await Assert.ThrowsAsync<ApiException>(() => ReadAsync(root.ToJsonString()))).Kind);
    }

    [Theory]
    [InlineData("""[{"cashReceivedAmount":300,"cashRefundPaidAmount":0,"cashNetMovement":300}]""")]
    [InlineData("""[{"posSessionId":"00000000-0000-0000-0000-000000000040","cashReceivedAmount":"300","cashRefundPaidAmount":0,"cashNetMovement":300}]""")]
    public async Task GetFinancialReportAsync_RejectsInvalidCashByShiftJson(string cashByShiftJson)
    {
        var root = JsonNode.Parse(SalesJson)!;
        root["cashByShift"] = JsonNode.Parse(cashByShiftJson);
        Assert.Equal(ApiErrorKind.InvalidResponse,
            (await Assert.ThrowsAsync<ApiException>(() => ReadAsync(root.ToJsonString()))).Kind);
    }

    internal static async Task<FinancialReportSnapshot> ReadAsync(string json)
    {
        using var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        using var http = new HttpClient(handler);
        return await new AnalyticsApiRepository(new ApiClient(http, new())).GetFinancialReportAsync(SampleRange);
    }

    internal const string SalesJson = """
        {
          "period": "custom",
          "startDate": "2024-10-12",
          "endDate": "2024-10-14",
          "timeZone": "UTC+07:00",
          "asOf": "2024-10-15T02:00:00Z",
          "summary": {
            "completedOrderCount": 3, "quantity": 5, "allocationMismatchOrderCount": 1,
            "salesExcludingTax": 480, "salesTax": 36, "paymentReceivedAmount": 370,
            "costOfGoodsSold": null, "salesGrossProfit": null,
            "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
            "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
          },
          "daily": [
            {
              "date": "2024-10-12",
              "totals": {
                "completedOrderCount": 1, "quantity": 2, "allocationMismatchOrderCount": 0,
                "salesExcludingTax": 180, "salesTax": 18, "paymentReceivedAmount": 248,
                "costOfGoodsSold": 120, "salesGrossProfit": 60,
                "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
                "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
              }
            },
            {
              "date": "2024-10-13",
              "totals": {
                "completedOrderCount": 0, "quantity": 0, "allocationMismatchOrderCount": 0,
                "salesExcludingTax": 0, "salesTax": 0, "paymentReceivedAmount": 0,
                "costOfGoodsSold": 0, "salesGrossProfit": 0,
                "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
                "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
              }
            },
            {
              "date": "2024-10-14",
              "totals": {
                "completedOrderCount": 2, "quantity": 3, "allocationMismatchOrderCount": 1,
                "salesExcludingTax": 300, "salesTax": 18, "paymentReceivedAmount": 122,
                "costOfGoodsSold": null, "salesGrossProfit": null,
                "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
                "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
              }
            }
          ],
          "topProductsByQuantity": [
            { "productId": "00000000-0000-0000-0000-000000000001", "productName": "Sample A", "quantity": 2, "salesExcludingTax": 180 },
            { "productId": "00000000-0000-0000-0000-000000000002", "productName": "Sample B", "quantity": 2, "salesExcludingTax": 180 },
            { "productId": "00000000-0000-0000-0000-000000000003", "productName": "Sample C", "quantity": 1, "salesExcludingTax": 120 }
          ],
          "topProductsByRevenue": [
            { "productId": "00000000-0000-0000-0000-000000000001", "productName": "Sample A", "quantity": 2, "salesExcludingTax": 180 },
            { "productId": "00000000-0000-0000-0000-000000000002", "productName": "Sample B", "quantity": 2, "salesExcludingTax": 180 },
            { "productId": "00000000-0000-0000-0000-000000000003", "productName": "Sample C", "quantity": 1, "salesExcludingTax": 120 }
          ],
          "cashierPerformance": [
            { "cashierUserId": "10000000-0000-0000-0000-000000000001", "cashierName": "Cashier 1", "completedOrderCount": 2, "quantity": 3, "salesExcludingTax": 300, "costOfGoodsSold": 120, "salesGrossProfit": 180 },
            { "cashierUserId": "10000000-0000-0000-0000-000000000002", "cashierName": "Cashier 2", "completedOrderCount": 1, "quantity": 2, "salesExcludingTax": 180, "costOfGoodsSold": null, "salesGrossProfit": null }
          ]
        }
        """;
}
