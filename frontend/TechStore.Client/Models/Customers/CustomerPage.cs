namespace TechStore.Client.Models.Customers;

public sealed record CustomerPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
