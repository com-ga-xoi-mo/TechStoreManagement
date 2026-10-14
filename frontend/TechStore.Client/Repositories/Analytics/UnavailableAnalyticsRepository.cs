using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Repositories.Analytics;

/// <summary>Explicitly reports that no Analytics source is configured.</summary>
public sealed class UnavailableAnalyticsRepository : IAnalyticsRepository
{
    public Task<DashboardReadResult> GetDashboardAsync(
        DashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DashboardReadResult.Unavailable);
    }
}
