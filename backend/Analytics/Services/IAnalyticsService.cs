using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics.Services;

public interface IAnalyticsService
{
    Task<AdminDashboardDto> GetDashboardAsync(string period, CancellationToken cancellationToken = default);
}
