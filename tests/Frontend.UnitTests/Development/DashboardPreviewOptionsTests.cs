using Microsoft.Extensions.DependencyInjection;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Development;

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
    public async Task Registration_UsesTransientRepositoryAndUnavailableDoesNotFabricateZero()
    {
        var services = new ServiceCollection()
            .AddDashboardAnalytics(new DashboardPreviewOptions(false))
            .BuildServiceProvider();

        using (services)
        {
            using var firstScope = services.CreateScope();
            using var secondScope = services.CreateScope();
            var first = firstScope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();
            var second = secondScope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();

            Assert.IsType<UnavailableAnalyticsRepository>(first);
            Assert.IsType<UnavailableAnalyticsRepository>(second);
            Assert.NotSame(first, second);
            var result = await first.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today));
            Assert.False(result.IsAvailable);
            Assert.Null(result.Snapshot);
        }
    }

    [Fact]
    public void Registration_EnablesFixtureOnlyWhenConfigured()
    {
        var services = new ServiceCollection()
            .AddDashboardAnalytics(new DashboardPreviewOptions(true))
            .BuildServiceProvider();

        using (services)
        using (var scope = services.CreateScope())
            Assert.IsType<FixtureAnalyticsRepository>(scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>());
    }
}
