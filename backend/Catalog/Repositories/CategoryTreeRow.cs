namespace TechStore.Api.Catalog.Repositories;

public sealed record CategoryTreeRow(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentId,
    bool IsActive,
    int ProductCount,
    int SkuCount);
