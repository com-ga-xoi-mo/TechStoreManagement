using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Orders;

/// <summary>Applied order list criteria. Defaults mirror the API: newest first, page 1, 20 per page.</summary>
public sealed record OrderQuery
{
    public OrderStatus? Status { get; init; }
    public string? Query { get; init; }
    public string Sort { get; init; } = "createdAt:desc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
