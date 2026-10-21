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
                EF.Functions.ILike(p.Brand, pattern) ||
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

    public async Task<Product?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .FromSqlInterpolated($"SELECT * FROM products WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (product != null)
        {
            await dbContext.Entry(product).Collection(p => p.Variants).LoadAsync(cancellationToken);
        }

        return product;
    }

    public async Task<IReadOnlyList<SpecValueCount>> GetSpecValueCountsAsync(Guid? categoryId, CancellationToken cancellationToken = default)
    {
        // Effective specs exactly as the search filter sees them: product specs for a product without variants,
        // product specs || variant specs for each variant otherwise.
        return await dbContext.Database
            .SqlQuery<SpecValueCount>($"""
                WITH effective AS (
                    SELECT p.id AS product_id, p.specs AS specs
                    FROM products AS p
                    WHERE NOT EXISTS (SELECT 1 FROM product_variants AS v WHERE v.product_id = p.id)
                      AND (CAST({categoryId} AS uuid) IS NULL OR p.category_id = CAST({categoryId} AS uuid))
                    UNION ALL
                    SELECT p.id, p.specs || v.specs
                    FROM products AS p
                    JOIN product_variants AS v ON v.product_id = p.id
                    WHERE CAST({categoryId} AS uuid) IS NULL OR p.category_id = CAST({categoryId} AS uuid)
                )
                SELECT e.key AS "Key", e.value::text AS "ValueJson", COUNT(DISTINCT effective.product_id)::int AS "ProductCount"
                FROM effective
                CROSS JOIN LATERAL jsonb_each(effective.specs) AS e
                WHERE jsonb_typeof(e.value) IN ('number', 'string', 'boolean')
                GROUP BY GROUPING SETS ((e.key, e.value::text), (e.key))
                """)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BrandCount>> GetBrandCountsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Products
            .AsNoTracking()
            .GroupBy(p => p.Brand)
            .Select(g => new BrandCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BrandCount>> GetBrandSpellingsAsync(string brand, Guid? excludeProductId, CancellationToken cancellationToken = default)
    {
        // No wildcards: an escaped ILIKE pattern is an exact match ignoring letter case.
        var pattern = EscapeLikePattern(brand);
        return await dbContext.Products
            .AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Brand, pattern) && (excludeProductId == null || p.Id != excludeProductId))
            .GroupBy(p => p.Brand)
            .Select(g => new BrandCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsInUseAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.InventoryStocks.AnyAsync(x => x.ProductId == productId, cancellationToken))
        {
            return true;
        }

        if (await dbContext.SerialImeis.AnyAsync(x => x.ProductId == productId, cancellationToken))
        {
            return true;
        }

        if (await dbContext.PurchaseOrderItems.AnyAsync(x => x.ProductId == productId, cancellationToken))
        {
            return true;
        }

        if (await dbContext.OrderItems.AnyAsync(x => x.ProductId == productId, cancellationToken))
        {
            return true;
        }

        return false;
    }

    public void AddVariant(ProductVariant variant)
    {
        dbContext.ProductVariants.Add(variant);
    }

    public void Remove(Product product)
    {
        dbContext.Products.Remove(product);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
