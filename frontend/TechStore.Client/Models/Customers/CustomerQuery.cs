namespace TechStore.Client.Models.Customers;

public sealed record CustomerQuery
{
    public string? Query { get; init; }
    public string? Tier { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
