using System.Globalization;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.Repositories.Analytics;

public sealed class AnalyticsApiRepositoryTests
{
    [Theory]
    [InlineData(DashboardPeriod.Today, "today")]
    [InlineData(DashboardPeriod.Last7Days, "last7Days")]
    [InlineData(DashboardPeriod.ThisMonth, "thisMonth")]
    public async Task GetDashboardAsync_UsesSupportedPeriodAndMapsServerSnapshot(DashboardPeriod period, string key)
    {
        var api = new StubApiClient(_ => MakeDto(key));
        var repository = new AnalyticsApiRepository(api);

        var result = await repository.GetDashboardAsync(new DashboardQuery(period));

        Assert.Equal($"api/analytics/dashboard?period={key}", api.LastPath);
        var snapshot = Assert.IsType<DashboardSnapshot>(result.Snapshot);
        Assert.True(result.IsAvailable);
        Assert.Equal(period, snapshot.Period);
        Assert.Equal(DashboardSourceKind.Live, snapshot.SourceKind);
        Assert.Equal(17_500_000m, snapshot.Summary.Revenue);
        Assert.Equal(42, snapshot.Summary.ProductCount);
        Assert.Null(snapshot.Summary.RevenueChangePercent);
        Assert.Equal(2, snapshot.DailyRevenue.Count);
        Assert.Equal(2, snapshot.LowStockItems.Single().AvailableQuantity);
        Assert.Equal(3, snapshot.LowStockItems.Single().Quantity);
        Assert.Equal(1, snapshot.LowStockItems.Single().ReservedQuantity);
        Assert.Equal("DEMO-ORDER-1", snapshot.RecentOrders.Single().OrderCode);
        Assert.Equal("Nguyễn An", snapshot.RecentOrders.Single().CustomerName);
        Assert.Equal(OrderStatus.Processing, snapshot.RecentOrders.Single().Status);
        Assert.Equal(DateTimeKind.Utc, snapshot.AsOf.UtcDateTime.Kind);
        Assert.Equal(TimeSpan.Zero, snapshot.RecentOrders.Single().CreatedAt.Offset);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenCustomRangeUsesCurrentCulture_SendsIsoDatesAndMapsZeroResult()
    {
        var start = new DateOnly(2026, 10, 18);
        var end = start.AddDays(1);
        var api = new StubApiClient(_ => MakeDto("custom") with
        {
            Summary = new(0m, 0, 0, 0, null, null),
            DailyRevenue = [new(start, 0m), new(end, 0m)],
            LowStockItems = [],
            RecentOrders = []
        });
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
            var result = await new AnalyticsApiRepository(api)
                .GetDashboardAsync(new(DashboardPeriod.Custom, (start, end)));

            Assert.Equal("api/analytics/dashboard?period=custom&from=2026-10-18&to=2026-10-19", api.LastPath);
            var snapshot = Assert.IsType<DashboardSnapshot>(result.Snapshot);
            Assert.Equal(DashboardPeriod.Custom, snapshot.Period);
            Assert.Equal((start, end), (snapshot.StartDate, snapshot.EndDate));
            Assert.Equal(DashboardSourceKind.Live, snapshot.SourceKind);
            Assert.Equal(0m, snapshot.Summary.Revenue);
            Assert.Equal(new[] { start, end }, snapshot.DailyRevenue.Select(row => row.Date));
            Assert.Empty(snapshot.LowStockItems);
            Assert.Empty(snapshot.RecentOrders);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData("last7Days", false)]
    [InlineData("custom", true)]
    public async Task GetDashboardAsync_WhenCustomResponseDoesNotMatchQuery_ReportsInvalidResponse(string period, bool wrongDate)
    {
        var from = new DateOnly(2026, 10, 18);
        var end = wrongDate ? from : from.AddDays(1);
        var repository = new AnalyticsApiRepository(new StubApiClient(_ => MakeDto(period)));

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            repository.GetDashboardAsync(new(DashboardPeriod.Custom, (from, end))));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenDatesDoNotMatchPeriodType_DoesNotCallApi()
    {
        var date = new DateOnly(2026, 10, 18);
        var api = new StubApiClient(_ => MakeDto("custom"));
        var repository = new AnalyticsApiRepository(api);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetDashboardAsync(new(DashboardPeriod.Custom)));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetDashboardAsync(new(DashboardPeriod.Today, (date, date))));
        Assert.Null(api.LastPath);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenDailyValuesDoNotReconcile_ReportsInvalidResponse()
    {
        var repository = new AnalyticsApiRepository(new StubApiClient(_ => MakeDto("last7Days") with
        {
            Summary = new AdminDashboardSummaryDto(17_500_001m, 4, 42, 1, null, null)
        }));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Last7Days)));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenMetadataOrStatusIsUnsupported_ReportsInvalidResponse()
    {
        var invalidZone = new AnalyticsApiRepository(new StubApiClient(_ => MakeDto("today") with { TimeZone = "UTC" }));
        var zoneError = await Assert.ThrowsAsync<ApiException>(() => invalidZone.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today)));
        Assert.Equal(ApiErrorKind.InvalidResponse, zoneError.Kind);

        var invalidStatus = new AnalyticsApiRepository(new StubApiClient(_ => MakeDto("today") with
        {
            RecentOrders = [new AdminDashboardRecentOrderDto(Guid.NewGuid(), "ORDER-1", null, "Product", 1m, "Unknown", Utc(12))]
        }));
        var statusError = await Assert.ThrowsAsync<ApiException>(() => invalidStatus.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today)));
        Assert.Equal(ApiErrorKind.InvalidResponse, statusError.Kind);
    }

    [Fact]
    public async Task GetDashboardAsync_PropagatesApiFailureAndCancellation()
    {
        var failure = new ApiException(ApiErrorKind.Http, "HTTP error", 403);
        var failedRepository = new AnalyticsApiRepository(new StubApiClient(_ => throw failure));
        Assert.Same(failure, await Assert.ThrowsAsync<ApiException>(() => failedRepository.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today))));

        using var source = new CancellationTokenSource();
        source.Cancel();
        var cancelledRepository = new AnalyticsApiRepository(new StubApiClient(_ => MakeDto("today")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledRepository.GetDashboardAsync(
            new DashboardQuery(DashboardPeriod.Today), source.Token));
    }

    private static AdminDashboardDto MakeDto(string period) => new(
        period,
        new DateOnly(2026, 10, 18),
        new DateOnly(2026, 10, 19),
        "UTC+07:00",
        Utc(18),
        new AdminDashboardSummaryDto(17_500_000m, 4, 42, 1, null, null),
        [new AdminDashboardDailyRevenueDto(new DateOnly(2026, 10, 18), 10_000_000m),
         new AdminDashboardDailyRevenueDto(new DateOnly(2026, 10, 19), 7_500_000m)],
        [new AdminDashboardLowStockDto(Guid.NewGuid(), Guid.NewGuid(), "Phone", null, null, 3, 1, 2, 3)],
        [new AdminDashboardRecentOrderDto(Guid.NewGuid(), "DEMO-ORDER-1", "Nguyễn An", "Phone", 17_500_000m, "Processing", Utc(17))]);

    private static DateTime Utc(int day) => new(2026, 10, day, 5, 0, 0, DateTimeKind.Utc);

    private sealed class StubApiClient(Func<string, AdminDashboardDto> response) : IApiClient
    {
        public Task<ApiResponse<TResponse>> PostWithStatusAsync<TRequest, TResponse>(
            string relativePath,
            TRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string? LastPath { get; private set; }

        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPath = relativePath;
            return Task.FromResult((TResponse)(object)response(relativePath));
        }

        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
