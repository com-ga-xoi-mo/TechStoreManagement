using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Repositories.Analytics;

public interface IAnalyticsRepository
{
    Task<DashboardSnapshot> GetDashboardSnapshotAsync(DashboardPeriod period, CancellationToken cancellationToken = default);
}
