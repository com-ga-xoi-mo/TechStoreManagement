using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;

namespace TechStore.UnitTests;

public class FakeProductRepository : IProductRepository
{
    public Dictionary<Guid, Product> Products { get; } = new();
    public List<Product> AddedProducts { get; } = [];
    public HashSet<string> ExistingSkus { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<Guid> InUseProductIds { get; } = [];
    public List<ProductVariant> AddedVariants { get; } = [];
    public List<Product> RemovedProducts { get; } = [];
    public int GetForUpdateCallCount { get; private set; }

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

    public List<SpecValueCount> SpecValueCounts { get; } = [];
    public Guid? LastSpecCategoryId { get; private set; }

    public Task<IReadOnlyList<SpecValueCount>> GetSpecValueCountsAsync(Guid? categoryId, CancellationToken cancellationToken = default)
    {
        LastSpecCategoryId = categoryId;
        return Task.FromResult<IReadOnlyList<SpecValueCount>>(SpecValueCounts.ToList());
    }

    public Task<IReadOnlyList<BrandCount>> GetBrandCountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BrandCount>>(Products.Values
            .GroupBy(p => p.Brand, StringComparer.Ordinal)
            .Select(g => new BrandCount(g.Key, g.Count()))
            .ToList());

    public Task<IReadOnlyList<BrandCount>> GetBrandSpellingsAsync(string brand, Guid? excludeProductId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BrandCount>>(Products.Values
            .Where(p => p.Id != excludeProductId && string.Equals(p.Brand, brand, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Brand, StringComparer.Ordinal)
            .Select(g => new BrandCount(g.Key, g.Count()))
            .ToList());

    public void Add(Product product)
    {
        AddedProducts.Add(product);
        Products[product.Id] = product;
    }

    public Task<Product?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        GetForUpdateCallCount++;
        Products.TryGetValue(id, out var p);
        return Task.FromResult(p);
    }

    public Task<bool> IsInUseAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InUseProductIds.Contains(productId));
    }

    public void AddVariant(ProductVariant variant)
    {
        AddedVariants.Add(variant);
    }

    public void Remove(Product product)
    {
        RemovedProducts.Add(product);
        Products.Remove(product.Id);
    }
}
