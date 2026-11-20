namespace TechStore.Client.Models.Analytics;

public sealed record DashboardQuery(DashboardPeriod Period, (DateOnly From, DateOnly To)? Range = null);
