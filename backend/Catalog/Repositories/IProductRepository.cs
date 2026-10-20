using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Repositories;

public interface IProductRepository
{
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

    void Add(Product product);

    void AddVariant(ProductVariant variant);

    void Remove(Product product);
}
