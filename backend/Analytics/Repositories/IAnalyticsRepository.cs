namespace TechStore.Api.Analytics.Repositories;

public interface IAnalyticsRepository
{
    /// <summary>Reads the database clock as the first statement in the snapshot transaction.</summary>
    Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default);

    Task<DashboardAnalyticsReadModel> GetDashboardAsync(
        DashboardAnalyticsCriteria criteria,
        CancellationToken cancellationToken = default);
}
