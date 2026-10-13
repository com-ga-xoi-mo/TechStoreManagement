using System;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Repositories.Analytics;

public sealed class UnavailableAnalyticsRepository : IAnalyticsRepository
{
    public Task<DashboardSnapshot> GetDashboardSnapshotAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken = default) =>
        Task.FromException<DashboardSnapshot>(new InvalidOperationException(
            "Dashboard data is unavailable outside the explicit preview until an analytics API is integrated."));
}
