using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Repositories;

public interface IProductRepository
{
    /// <summary>Read-only product with category, variants and images (sorted by SortOrder) for the detail response.</summary>
    Task<Product?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<Product?> GetWithVariantsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> FindExistingSkusAsync(
        IEnumerable<string> skus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the product row (FOR UPDATE) and loads its variants as tracked entities. Requires an open transaction.
    /// </summary>
    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when any inventory stock, serial/IMEI, purchase order line or order line references the product.
    /// </summary>
    Task<bool> IsInUseAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per spec key and per distinct scalar value of the effective specs of all products (optionally one category),
    /// the number of distinct products; a key's own total has a null value.
    /// </summary>
    Task<IReadOnlyList<SpecValueCount>> GetSpecValueCountsAsync(Guid? categoryId, CancellationToken cancellationToken = default);

    /// <summary>Every exact brand spelling with the number of products using it.</summary>
    Task<IReadOnlyList<BrandCount>> GetBrandCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Spellings equal to <paramref name="brand"/> ignoring letter case, without the product being updated.</summary>
    Task<IReadOnlyList<BrandCount>> GetBrandSpellingsAsync(string brand, Guid? excludeProductId, CancellationToken cancellationToken = default);

    void Add(Product product);

    void AddVariant(ProductVariant variant);

    void Remove(Product product);
}
