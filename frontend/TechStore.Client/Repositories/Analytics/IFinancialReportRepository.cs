using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Repositories.Analytics;

public interface IFinancialReportRepository
{
    Task<FinancialReportSnapshot> GetFinancialReportAsync(FinancialReportRange range,
        CancellationToken cancellationToken = default);
}
