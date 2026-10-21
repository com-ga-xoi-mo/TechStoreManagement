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
    }
}
