namespace TechStore.Api.Customers.Repositories;

public sealed record VoucherSearchCriteria(string? Query, bool? IsActive, int Page, int PageSize);
