namespace TechStore.Client.Models.Catalog;

public sealed record ProductSummary(
    Guid Id,
    string Name,
    string Sku,
    string Brand,
    Guid CategoryId,
    string CategoryName,
    decimal PriceFrom,
    decimal PriceTo,
    int VariantCount,
    bool IsSerialTracked,
    bool IsActive,
    string? ImageUrl);
