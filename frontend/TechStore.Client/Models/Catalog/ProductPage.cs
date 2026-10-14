namespace TechStore.Client.Models.Catalog;

public sealed record ProductPage(
    IReadOnlyList<ProductSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
