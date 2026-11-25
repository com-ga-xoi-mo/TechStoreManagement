using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Client.Repositories.Analytics;

namespace TechStore.Client.Services.Development;

public static class DashboardAnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardAnalytics(
        this IServiceCollection services,
        DashboardPreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<AnalyticsApiRepository>();
        services.AddTransient<IFinancialReportRepository>(provider => provider.GetRequiredService<AnalyticsApiRepository>());
        services.AddTransient<FixtureAnalyticsRepository>();
        services.AddTransient<UnavailableAnalyticsRepository>();
        services.AddTransient<IAnalyticsRepository>(provider =>
            options.Enabled
                ? provider.GetRequiredService<FixtureAnalyticsRepository>()
                : provider.GetRequiredService<AnalyticsApiRepository>());

        return services;
    }
}
