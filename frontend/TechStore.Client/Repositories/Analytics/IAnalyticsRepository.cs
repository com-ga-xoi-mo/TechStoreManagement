using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Repositories.Analytics;

public interface IAnalyticsRepository
{
    Task<DashboardReadResult> GetDashboardAsync(
        DashboardQuery query,
        CancellationToken cancellationToken = default);
}
