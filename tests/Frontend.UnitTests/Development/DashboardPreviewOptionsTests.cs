using Microsoft.Extensions.DependencyInjection;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Development;

public sealed class DashboardPreviewOptionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void Parse_UsesExplicitOptIn(string? value, bool enabled)
    {
        Assert.Equal(enabled, DashboardPreviewOptions.Parse(value).Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData(" 1")]
    public void Parse_RejectsUnsupportedValue(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => DashboardPreviewOptions.Parse(value));
        Assert.Contains(DashboardPreviewOptions.EnvironmentVariable, error.Message);
    }

    [Fact]
    public async Task Registration_UsesTransientLiveApiRepositoryByDefaultAndPropagatesFailure()
    {
        var services = new ServiceCollection()
            .AddSingleton<IApiClient>(new ThrowingApiClient())
            .AddDashboardAnalytics(new DashboardPreviewOptions(false))
            .BuildServiceProvider();

        using (services)
        {
            using var firstScope = services.CreateScope();
            using var secondScope = services.CreateScope();
            var first = firstScope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();
            var second = secondScope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();

            Assert.IsType<AnalyticsApiRepository>(first);
            Assert.IsType<AnalyticsApiRepository>(second);
            Assert.NotSame(first, second);
            await Assert.ThrowsAsync<ApiException>(() => first.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today)));
        }
    }

    [Fact]
    public void Registration_EnablesFixtureOnlyWhenConfigured()
    {
        var services = new ServiceCollection()
            .AddSingleton<IApiClient>(new ThrowingApiClient())
            .AddDashboardAnalytics(new DashboardPreviewOptions(true))
            .BuildServiceProvider();

        using (services)
        using (var scope = services.CreateScope())
            Assert.IsType<FixtureAnalyticsRepository>(scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>());
    }

    private sealed class ThrowingApiClient : IApiClient
    {
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default) =>
            throw new ApiException(ApiErrorKind.Network, "api unavailable");
        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
