using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Common.Data;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;

    public ProductService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null)
        {
            return ProductDetailResult.NotFound();
        }

        var prodSpecs = ProductSpecHelper.ParseJsonSpecs(product.Specs);

        var variantDtos = product.Variants
            .OrderBy(v => v.Price)
            .ThenBy(v => v.Sku)
            .Select(v =>
            {
                var vSpecs = ProductSpecHelper.ParseJsonSpecs(v.Specs);
                var effective = ProductSpecHelper.MergeEffectiveSpecs(prodSpecs, vSpecs);

                return new ProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    VariantName = v.VariantName,
                    Sku = v.Sku,
                    Barcode = v.Barcode,
                    Price = v.Price,
                    Specs = vSpecs,
                    EffectiveSpecs = effective,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                };
            })
            .ToList();

        var dto = new ProductDto
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Brand = product.Brand,
            BasePrice = product.BasePrice,
            CostPrice = product.CostPrice,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            Specs = prodSpecs,
            IsSerialTracked = product.IsSerialTracked,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Variants = variantDtos
        };

        return ProductDetailResult.Success(dto);
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Validate spec filters
        var specResult = ProductSpecFilterHelper.Parse(request.Spec);
        if (!specResult.IsValid)
        {
            return ProductSearchResult.Failed(new Dictionary<string, string[]>
            {
                ["spec"] = [specResult.ErrorMessage!]
            });
        }

        // 2. Validate price range
        if (request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice.Value > request.MaxPrice.Value)
        {
            return ProductSearchResult.Failed(new Dictionary<string, string[]>
            {
                ["minPrice"] = ["minPrice cannot be greater than maxPrice."]
            });
        }

        // 3. Validate sorting
        var sortField = "createdat";
        var isDescending = true;

        if (!string.IsNullOrWhiteSpace(request.Sort))
        {
            var parts = request.Sort.Trim().Split(':');
            var field = parts[0].Trim().ToLowerInvariant();
            var dir = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "asc";

            if (field != "name" && field != "price" && field != "createdat")
            {
                return ProductSearchResult.Failed(new Dictionary<string, string[]>
                {
                    ["sort"] = [$"Unknown sort field '{field}'. Allowed values: name, price, createdAt."]
                });
            }

            if (dir != "asc" && dir != "desc")
            {
                return ProductSearchResult.Failed(new Dictionary<string, string[]>
                {
                    ["sort"] = [$"Invalid sort direction '{dir}'. Allowed values: asc, desc."]
                });
            }

            sortField = field;
            isDescending = dir == "desc";
        }

        // 4. Base query (apply spec containment via FromSql if needed)
        IQueryable<Entities.Product> query;
        if (specResult.JsonString != null)
        {
            var filterJson = specResult.JsonString;
            query = _dbContext.Products
                .FromSqlInterpolated($"SELECT * FROM products AS p WHERE (NOT EXISTS (SELECT 1 FROM product_variants AS v WHERE v.product_id = p.id) AND p.specs @> {filterJson}::jsonb) OR EXISTS (SELECT 1 FROM product_variants AS v WHERE v.product_id = p.id AND (p.specs || v.specs) @> {filterJson}::jsonb)")
                .AsNoTracking();
        }
        else
        {
            query = _dbContext.Products.AsNoTracking();
        }

        // 5. Keyword search (q)
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var keyword = request.Q.Trim();
            var escaped = EscapeLikePattern(keyword);
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

        // 6. Brand filter
        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            var brandTrimmed = request.Brand.Trim();
            query = query.Where(p => EF.Functions.ILike(p.Brand, brandTrimmed));
        }

        // 7. Category filter
        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        // 8. IsActive filter
        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        // 9. Price range overlap filter
        if (request.MinPrice.HasValue)
        {
            var min = request.MinPrice.Value;
            query = query.Where(p => (p.Variants.Select(v => (decimal?)v.Price).Max() ?? p.BasePrice) >= min);
        }

        if (request.MaxPrice.HasValue)
        {
            var max = request.MaxPrice.Value;
            query = query.Where(p => (p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice) <= max);
        }

        // 10. Ordering
        query = sortField switch
        {
            "name" => isDescending
                ? query.OrderByDescending(p => p.Name).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "price" => isDescending
                ? query.OrderByDescending(p => p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice).ThenBy(p => p.Id),
            _ => isDescending
                ? query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
                : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
        };

        // 11. Pagination
        var (page, pageSize) = ProductPagingHelper.Normalize(request.Page, request.PageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = ProductPagingHelper.CalculateTotalPages(totalCount, pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Brand = p.Brand,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                PriceFrom = p.Variants.Select(v => (decimal?)v.Price).Min() ?? p.BasePrice,
                PriceTo = p.Variants.Select(v => (decimal?)v.Price).Max() ?? p.BasePrice,
                VariantCount = p.Variants.Count,
                IsSerialTracked = p.IsSerialTracked,
                IsActive = p.IsActive,
                ImageUrl = p.ImageUrl
            })
            .ToListAsync(cancellationToken);

        var resultDto = new PagedResultDto<ProductSummaryDto>(items, page, pageSize, totalCount, totalPages);
        return ProductSearchResult.Success(resultDto);
    }

    public async Task<CreateProductResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Run input validator
        var validationErrors = CreateProductValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return CreateProductResult.ValidationFailed(validationErrors);
        }

        // 2. Check Category existence
        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId!.Value, cancellationToken);

        if (category == null)
        {
            return CreateProductResult.ValidationFailed(new Dictionary<string, string[]>
            {
                ["categoryId"] = [$"Category with ID '{request.CategoryId!.Value}' does not exist."]
            });
        }

        // 3. Check SKU conflicts case-insensitively
        var requestSkus = new List<string> { request.Sku!.Trim() };
        if (request.Variants != null)
        {
            requestSkus.AddRange(request.Variants
                .Where(v => !string.IsNullOrWhiteSpace(v.Sku))
                .Select(v => v.Sku!.Trim()));
        }

        var requestSkusLower = requestSkus.Select(s => s.ToLowerInvariant()).Distinct().ToList();

        var conflictingProductSkus = await _dbContext.Products
            .Where(p => requestSkusLower.Contains(p.Sku.ToLower()))
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);

        var conflictingVariantSkus = await _dbContext.ProductVariants
            .Where(v => requestSkusLower.Contains(v.Sku.ToLower()))
            .Select(v => v.Sku)
            .ToListAsync(cancellationToken);

        var allConflicts = conflictingProductSkus.Concat(conflictingVariantSkus).Distinct().ToList();
        if (allConflicts.Count > 0)
        {
            return CreateProductResult.Conflict(allConflicts);
        }

        // 4. Build product and variant entities
        var now = DateTime.UtcNow;
        var prodSpecsDict = request.Specs ?? new Dictionary<string, JsonElement>();
        var prodSpecsJson = ProductSpecHelper.SerializeSpecs(prodSpecsDict);

        var product = new Entities.Product
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId!.Value,
            Name = request.Name!.Trim(),
            Sku = request.Sku!.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            Brand = request.Brand!.Trim(),
            BasePrice = request.BasePrice!.Value,
            CostPrice = request.CostPrice!.Value,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            Specs = prodSpecsJson,
            IsSerialTracked = request.IsSerialTracked ?? true,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = null
        };

        if (request.Variants != null)
        {
            foreach (var vReq in request.Variants)
            {
                var vSpecsDict = vReq.Specs ?? new Dictionary<string, JsonElement>();
                var vSpecsJson = ProductSpecHelper.SerializeSpecs(vSpecsDict);

                var variant = new Entities.ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    VariantName = vReq.VariantName!.Trim(),
                    Sku = vReq.Sku!.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(vReq.Barcode) ? null : vReq.Barcode.Trim(),
                    Price = vReq.Price!.Value,
                    Specs = vSpecsJson,
                    IsActive = vReq.IsActive ?? true,
                    CreatedAt = now,
                    UpdatedAt = null
                };

                product.Variants.Add(variant);
            }
        }

        _dbContext.Products.Add(product);

        // 5. Persist with SaveChangesAsync, catching 23505 race conditions
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return CreateProductResult.Conflict(requestSkus);
        }

        // 6. Build and return created ProductDto
        var variantDtos = product.Variants
            .OrderBy(v => v.Price)
            .ThenBy(v => v.Sku)
            .Select(v =>
            {
                var vSpecs = ProductSpecHelper.ParseJsonSpecs(v.Specs);
                var effective = ProductSpecHelper.MergeEffectiveSpecs(prodSpecsDict, vSpecs);

                return new ProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    VariantName = v.VariantName,
                    Sku = v.Sku,
                    Barcode = v.Barcode,
                    Price = v.Price,
                    Specs = vSpecs,
                    EffectiveSpecs = effective,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                };
            })
            .ToList();

        var createdDto = new ProductDto
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            CategoryName = category.Name,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Brand = product.Brand,
            BasePrice = product.BasePrice,
            CostPrice = product.CostPrice,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            Specs = prodSpecsDict,
            IsSerialTracked = product.IsSerialTracked,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Variants = variantDtos
        };

        return CreateProductResult.Success(createdDto);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
