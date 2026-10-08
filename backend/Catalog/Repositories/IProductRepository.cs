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

    void Add(Product product);
}
