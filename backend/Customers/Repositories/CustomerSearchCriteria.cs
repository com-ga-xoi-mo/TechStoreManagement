using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Repositories;

public sealed record CustomerSearchCriteria(
    string? Query,
    CustomerTier? Tier,
    bool? IsActive,
    int Page,
    int PageSize);

public sealed record CustomerLoyaltyPointSearchCriteria(Guid CustomerId, int Page, int PageSize);
