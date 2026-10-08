using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;

namespace TechStore.Api.Catalog.Repositories;

public class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query;
        if (!string.IsNullOrWhiteSpace(criteria.SpecFilterJson))
        {
            var filterJson = criteria.SpecFilterJson;
            query = dbContext.Products
                .FromSqlInterpolated($"SELECT * FROM products AS p WHERE (NOT EXISTS (SELECT 1 FROM product_variants AS v WHERE v.product_id = p.id) AND p.specs @> {filterJson}::jsonb) OR EXISTS (SELECT 1 FROM product_variants AS v WHERE v.product_id = p.id AND (p.specs || v.specs) @> {filterJson}::jsonb)")
                .AsNoTracking();
        }
        else
        {
            query = dbContext.Products.AsNoTracking();
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var escaped = EscapeLikePattern(criteria.Keyword);
            var pattern = $"%{escaped}%";

            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                (p.Barcode != null && EF.Functions.ILike(p.Barcode, pattern)) ||
                p.Variants.Any(v =>
                    EF.Functions.ILike(v.Sku, pattern) ||
                    (v.Barcode != null && EF.Functions.ILike(v.Barcode, pattern)) ||
                    EF.Functions.ILike(v.VariantName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Brand))
        {
            var brandEscaped = EscapeLikePattern(criteria.Brand);
            query = query.Where(p => EF.Functions.ILike(p.Brand, brandEscaped));
        }

        if (criteria.CategoryId.HasValue && criteria.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == criteria.CategoryId.Value);
        }

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == criteria.IsActive.Value);
        }

        if (criteria.MinPrice.HasValue)
        {
            var min = criteria.MinPrice.Value;
            query = query.Where(p => (p.Variants.Select(v => (decimal?)v.Price).Max() ?? p.BasePrice) >= min);
        }

        if (criteria.MaxPrice.HasValue)
        {
            var max = criteria.MaxPrice.Value;
            query = query.Where(p => (p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice) <= max);
        }

        query = criteria.SortField switch
        {
            ProductSortField.Name => criteria.Descending
                ? query.OrderByDescending(p => p.Name).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ProductSortField.Price => criteria.Descending
                ? query.OrderByDescending(p => p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice).ThenBy(p => p.Id),
            _ => criteria.Descending
                ? query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
                : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Product?> GetWithVariantsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> FindExistingSkusAsync(
        IEnumerable<string> skus,
        CancellationToken cancellationToken = default)
    {
        var skusLower = skus
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (skusLower.Count == 0)
        {
            return [];
        }

        var conflictingProductSkus = await dbContext.Products
            .Where(p => skusLower.Contains(p.Sku.ToLower()))
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);

        var conflictingVariantSkus = await dbContext.ProductVariants
            .Where(v => skusLower.Contains(v.Sku.ToLower()))
            .Select(v => v.Sku)
            .ToListAsync(cancellationToken);

        return conflictingProductSkus
            .Concat(conflictingVariantSkus)
            .Distinct()
            .ToList();
    }

    public void Add(Product product)
    {
        dbContext.Products.Add(product);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
