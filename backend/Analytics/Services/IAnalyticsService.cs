using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics.Services;

public interface IAnalyticsService
{
    Task<DashboardAnalyticsResult> GetDashboardAsync(DashboardAnalyticsQuery query, CancellationToken cancellationToken = default);
    Task<FinancialReportResult> GetFinancialReportAsync(FinancialReportQuery query, CancellationToken cancellationToken = default);
}

public sealed record FinancialReportQuery(DateOnly From, DateOnly To);

public sealed record FinancialReportResult
{
    private FinancialReportResult() { }

    public FinancialReportDto? Report { get; private init; }
    public (string Field, string Message)? Error { get; private init; }

    public static FinancialReportResult Success(FinancialReportDto report) => new() { Report = report };
    public static FinancialReportResult Invalid(string field, string message) => new() { Error = (field, message) };
}

public sealed record DashboardAnalyticsQuery(string Period, (DateOnly From, DateOnly To)? Range = null);

public sealed record DashboardAnalyticsResult
{
    private DashboardAnalyticsResult() { }

    public AdminDashboardDto? Snapshot { get; private init; }
    public (string Field, string Message)? Error { get; private init; }

    public static DashboardAnalyticsResult Success(AdminDashboardDto snapshot) => new() { Snapshot = snapshot };
    public static DashboardAnalyticsResult Invalid(string field, string message) => new() { Error = (field, message) };
}
