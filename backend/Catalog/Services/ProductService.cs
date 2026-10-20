using System.Text.Json;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public class ProductService(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork) : IProductService
{
    public async Task<ProductDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetWithVariantsAsync(id, cancellationToken);
        if (product == null)
        {
            return ProductDetailResult.NotFound();
        }

        var dto = ProductDtoMapper.ToDto(product, product.Category?.Name ?? string.Empty);
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
        var sortField = ProductSortField.CreatedAt;
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

            sortField = field switch
            {
                "name" => ProductSortField.Name,
                "price" => ProductSortField.Price,
                _ => ProductSortField.CreatedAt
            };
            isDescending = dir == "desc";
        }

        // 4. Normalize paging
        var (page, pageSize) = ProductPagingHelper.Normalize(request.Page, request.PageSize);

        // 5. Build criteria
        var keyword = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();
        var brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim();
        var categoryId = (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
            ? request.CategoryId.Value
            : (Guid?)null;

        var criteria = new ProductSearchCriteria(
            Keyword: keyword,
            Brand: brand,
            CategoryId: categoryId,
            IsActive: request.IsActive,
            MinPrice: request.MinPrice,
            MaxPrice: request.MaxPrice,
            SpecFilterJson: specResult.JsonString,
            SortField: sortField,
            Descending: isDescending,
            Page: page,
            PageSize: pageSize);

        // 6. Query repository
        var (items, totalCount) = await productRepository.SearchAsync(criteria, cancellationToken);
        var totalPages = ProductPagingHelper.CalculateTotalPages(totalCount, pageSize);

        // 7. Map entities to ProductSummaryDto
        var summaryDtos = items.Select(p =>
        {
            var (priceFrom, priceTo) = ProductSpecHelper.CalculateDisplayPrice(
                p.BasePrice,
                p.Variants.Select(v => v.Price));

            return new ProductSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Brand = p.Brand,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name ?? string.Empty,
                PriceFrom = priceFrom,
                PriceTo = priceTo,
                VariantCount = p.Variants.Count,
                IsSerialTracked = p.IsSerialTracked,
                IsActive = p.IsActive,
                ImageUrl = p.ImageUrl
            };
        }).ToList();

        var resultDto = new PagedResultDto<ProductSummaryDto>(summaryDtos, page, pageSize, totalCount, totalPages);
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

        // 2. Check Category existence via ICategoryRepository
        var category = await categoryRepository.GetByIdAsync(request.CategoryId!.Value, cancellationToken);
        if (category == null)
        {
            return CreateProductResult.ValidationFailed(new Dictionary<string, string[]>
            {
                ["categoryId"] = [$"Category with ID '{request.CategoryId!.Value}' does not exist."]
            });
        }

        // 3. Check SKU conflicts case-insensitively via IProductRepository
        var requestSkus = new List<string> { request.Sku!.Trim() };
        if (request.Variants != null)
        {
            requestSkus.AddRange(request.Variants
                .Where(v => !string.IsNullOrWhiteSpace(v.Sku))
                .Select(v => v.Sku!.Trim()));
        }

        var allConflicts = await productRepository.FindExistingSkusAsync(requestSkus, cancellationToken);
        if (allConflicts.Count > 0)
        {
            return CreateProductResult.Conflict(allConflicts);
        }

        // 4. Build product and variant entities (IsActive always true)
        var now = DateTime.UtcNow;
        var prodSpecsDict = request.Specs ?? new Dictionary<string, JsonElement>();
        var prodSpecsJson = ProductSpecHelper.SerializeSpecs(prodSpecsDict);

        var product = new Product
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
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = null
        };

        if (request.Variants != null)
        {
            foreach (var vReq in request.Variants)
            {
                var vSpecsDict = vReq.Specs ?? new Dictionary<string, JsonElement>();
                var vSpecsJson = ProductSpecHelper.SerializeSpecs(vSpecsDict);

                var variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    VariantName = vReq.VariantName!.Trim(),
                    Sku = vReq.Sku!.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(vReq.Barcode) ? null : vReq.Barcode.Trim(),
                    Price = vReq.Price!.Value,
                    Specs = vSpecsJson,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = null
                };

                product.Variants.Add(variant);
            }
        }

        productRepository.Add(product);

        // 5. Persist with IUnitOfWork.SaveChangesAsync, catching DuplicateKeyException
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return CreateProductResult.Conflict(requestSkus);
        }

        // 6. Build and return created ProductDto
        var createdDto = ProductDtoMapper.ToDto(product, category.Name);
        return CreateProductResult.Success(createdDto);
    }

    public Task<ProductCommandResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public async Task<ProductCommandResult> SetStatusAsync(
        Guid id,
        UpdateProductStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.IsActive == null)
        {
            return ProductCommandResult.ValidationFailed(new Dictionary<string, string[]>
            {
                ["isActive"] = ["isActive is required."]
            });
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await productRepository.GetForUpdateAsync(id, cancellationToken);
            if (product == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductCommandResult.NotFound();
            }

            var category = await categoryRepository.GetByIdAsync(product.CategoryId, cancellationToken);
            var categoryName = category?.Name ?? string.Empty;

            if (product.IsActive == request.IsActive.Value)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductCommandResult.Success(ProductDtoMapper.ToDto(product, categoryName));
            }

            product.IsActive = request.IsActive.Value;
            product.UpdatedAt = DateTime.UtcNow;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return ProductCommandResult.Success(ProductDtoMapper.ToDto(product, categoryName));
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ProductCommandResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await productRepository.GetForUpdateAsync(id, cancellationToken);
            if (product == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductCommandResult.NotFound();
            }

            var inUse = await productRepository.IsInUseAsync(product.Id, cancellationToken);
            if (inUse)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductCommandResult.Conflict("Product is in use by stock, purchase or order records. Deactivate it instead.");
            }

            productRepository.Remove(product);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return ProductCommandResult.Success(null);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
