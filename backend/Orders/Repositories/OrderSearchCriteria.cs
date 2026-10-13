using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Repositories;

public enum OrderSortField
{
    CreatedAt,
    TotalAmount
}

// Already-validated, normalized values. From/To are UTC; From is inclusive and To is exclusive.
public sealed record OrderSearchCriteria(
    OrderStatus? Status,
    Guid? CustomerId,
    DateTime? From,
    DateTime? To,
    string? Keyword,
    OrderSortField SortField,
    bool Descending,
    int Page,
    int PageSize);
