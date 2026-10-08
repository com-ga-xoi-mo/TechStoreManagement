using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;

namespace TechStore.UnitTests;

public class FakeProductRepository : IProductRepository
{
    public Dictionary<Guid, Product> Products { get; } = new();
    public List<Product> AddedProducts { get; } = [];
    public HashSet<string> ExistingSkus { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int SearchCallCount { get; private set; }
    public ProductSearchCriteria? LastCriteria { get; private set; }
    public (IReadOnlyList<Product> Items, int TotalCount)? CustomSearchResult { get; set; }

    public Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        SearchCallCount++;
        LastCriteria = criteria;

        if (CustomSearchResult.HasValue)
        {
            return Task.FromResult(CustomSearchResult.Value);
        }

        var items = Products.Values.ToList();
        return Task.FromResult<(IReadOnlyList<Product> Items, int TotalCount)>((items, items.Count));
    }

    public Task<Product?> GetWithVariantsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    public Task<IReadOnlyList<string>> FindExistingSkusAsync(
        IEnumerable<string> skus,
        CancellationToken cancellationToken = default)
    {
        var found = skus
            .Where(s => !string.IsNullOrWhiteSpace(s) && ExistingSkus.Contains(s.Trim()))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(found);
    }

    public void Add(Product product)
    {
        AddedProducts.Add(product);
        Products[product.Id] = product;
    }
}
