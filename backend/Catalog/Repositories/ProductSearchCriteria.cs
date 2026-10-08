namespace TechStore.Api.Catalog.Repositories;

public enum ProductSortField
{
    Name,
    Price,
    CreatedAt
}

public sealed record ProductSearchCriteria(
    string? Keyword,
    string? Brand,
    Guid? CategoryId,
    bool? IsActive,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? SpecFilterJson,
    ProductSortField SortField,
    bool Descending,
    int Page,
    int PageSize);
