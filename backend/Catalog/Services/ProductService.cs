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
    IUnitOfWork unitOfWork,
    ISpecDefinitionRepository specDefinitionRepository) : IProductService
{
    private const string SpecNameConflictMessage = "A spec name was created by someone else at the same time. Reload the product and save again.";

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

    public async Task<IReadOnlyList<ProductSpecKeyDto>> GetSpecKeysAsync(Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var rows = await productRepository.GetSpecValueCountsAsync(categoryId, cancellationToken);
        var keys = ProductSpecSuggestionBuilder.Build(rows);
        var definitions = (await specDefinitionRepository.GetByKeysAsync(keys.Select(k => k.Key), cancellationToken))
            .ToDictionary(d => d.Key, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (!definitions.TryGetValue(key.Key, out var definition)) continue;
            key.Label = definition.Label;
            key.Unit = definition.Unit;
            key.ValueKind = definition.ValueKind;
        }
        return keys;
    }

    public async Task<IReadOnlyList<SpecDefinitionDto>> GetSpecDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await specDefinitionRepository.GetAllAsync(cancellationToken);
        return definitions
            .Select(d => new SpecDefinitionDto { Key = d.Key, Label = d.Label, Unit = d.Unit, ValueKind = d.ValueKind })
            .ToList();
    }

    /// <summary>
    /// Database rules of design D13: values of defined keys keep their kind, and names for new keys are free.
    /// Returns the field errors and the definitions to insert with the product.
    /// </summary>
    private async Task<(Dictionary<string, List<string>> Errors, List<SpecDefinition> NewDefinitions)> CheckSpecDefinitionsAsync(
        IDictionary<string, JsonElement>? productSpecs,
        IReadOnlyList<IDictionary<string, JsonElement>?> variantSpecs,
        IDictionary<string, SpecLabelRequest>? labels,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>();
        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }

        var groups = new List<(string Field, IDictionary<string, JsonElement> Specs)>();
        if (productSpecs is not null) groups.Add(("specs", productSpecs));
        for (var i = 0; i < variantSpecs.Count; i++)
        {
            if (variantSpecs[i] is { } specs) groups.Add(($"variants[{i}].specs", specs));
        }

        var keys = groups.SelectMany(g => g.Specs.Keys).ToHashSet(StringComparer.Ordinal);
        var definitions = (await specDefinitionRepository.GetByKeysAsync(keys, cancellationToken))
            .ToDictionary(d => d.Key, StringComparer.Ordinal);

        foreach (var (field, specs) in groups)
        {
            foreach (var (key, value) in specs)
            {
                if (definitions.TryGetValue(key, out var definition) && ProductSpecHelper.KindOf(value) != definition.ValueKind)
                    AddError(field, $"Spec '{key}' must be a {definition.ValueKind} value.");
            }
        }

        var pending = labels?
            .Where(entry => !definitions.ContainsKey(entry.Key))
            .Select(entry => new SpecDefinition
            {
                Key = entry.Key,
                Label = entry.Value.Label!.Trim(),
                Unit = string.IsNullOrWhiteSpace(entry.Value.Unit) ? null : entry.Value.Unit.Trim(),
                ValueKind = entry.Value.ValueKind!,
                CreatedAt = DateTime.UtcNow
            })
            .ToList() ?? [];

        if (pending.Count > 0)
        {
            var taken = await specDefinitionRepository.FindByLabelsAsync(pending.Select(d => d.Label), cancellationToken);
            foreach (var definition in pending)
            {
                var owner = taken.FirstOrDefault(t => string.Equals(t.Label, definition.Label, StringComparison.OrdinalIgnoreCase));
                if (owner is not null)
                    AddError($"specLabels.{definition.Key}", $"The name '{definition.Label}' is already used by spec '{owner.Key}'.");
            }
        }

        return (errors, errors.Count == 0 ? pending : []);
    }

    private static bool IsSpecDefinitionConflict(DuplicateKeyException exception) =>
        exception.ConstraintName?.Contains("spec_definitions", StringComparison.OrdinalIgnoreCase) == true;

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

        var (specErrors, newDefinitions) = await CheckSpecDefinitionsAsync(
            request.Specs,
            request.Variants?.Select(v => (IDictionary<string, JsonElement>?)v?.Specs).ToList() ?? [],
            request.SpecLabels,
            cancellationToken);
        if (specErrors.Count > 0)
        {
            return CreateProductResult.ValidationFailed(specErrors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
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
        foreach (var definition in newDefinitions)
        {
            specDefinitionRepository.Add(definition);
        }

        // 5. Persist with IUnitOfWork.SaveChangesAsync, catching DuplicateKeyException
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException exception) when (IsSpecDefinitionConflict(exception))
        {
            return CreateProductResult.ConflictWithMessage(SpecNameConflictMessage);
        }
        catch (DuplicateKeyException)
        {
            return CreateProductResult.Conflict(requestSkus);
        }

        // 6. Build and return created ProductDto
        var createdDto = ProductDtoMapper.ToDto(product, category.Name);
        return CreateProductResult.Success(createdDto);
    }

    public async Task<ProductCommandResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        // Step A - pure validation and lock
        var validationErrors = UpdateProductValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return ProductCommandResult.ValidationFailed(validationErrors);
        }

        var errors = new Dictionary<string, List<string>>();
        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                list = [];
                errors[field] = list;
            }
            list.Add(message);
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

            var category = await categoryRepository.GetByIdAsync(request.CategoryId!.Value, cancellationToken);
            if (category == null || !category.IsActive)
            {
                AddError("categoryId", $"Category '{request.CategoryId!.Value}' does not exist or is inactive.");
            }

            // Step B - variant item checks against stored data
            var existingById = product.Variants.ToDictionary(v => v.Id);
            if (request.Variants != null)
            {
                for (var i = 0; i < request.Variants.Count; i++)
                {
                    var item = request.Variants[i];
                    if (item == null) continue;

                    if (item.Id.HasValue)
                    {
                        if (!existingById.TryGetValue(item.Id.Value, out var existing))
                        {
                            AddError($"variants[{i}].id", $"Variant '{item.Id.Value}' does not belong to this product.");
                        }
                        else if (!string.IsNullOrWhiteSpace(item.Sku) && !string.Equals(item.Sku.Trim(), existing.Sku, StringComparison.OrdinalIgnoreCase))
                        {
                            AddError($"variants[{i}].sku", "The SKU of an existing variant cannot be changed.");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(item.Sku) && string.Equals(item.Sku.Trim(), product.Sku, StringComparison.OrdinalIgnoreCase))
                        {
                            AddError($"variants[{i}].sku", $"SKU '{item.Sku.Trim()}' is already used by the product.");
                        }
                    }
                }
            }

            var (specErrors, newDefinitions) = await CheckSpecDefinitionsAsync(
                request.Specs,
                request.Variants?.Select(v => (IDictionary<string, JsonElement>?)v?.Specs).ToList() ?? [],
                request.SpecLabels,
                cancellationToken);
            foreach (var (field, messages) in specErrors)
            {
                foreach (var message in messages) AddError(field, message);
            }

            var newCount = request.Variants?.Count(v => v is { Id: null }) ?? 0;
            if (product.Variants.Count + newCount > 0 && !string.IsNullOrWhiteSpace(request.Barcode))
            {
                AddError("barcode", "A product with variants cannot have a product barcode. Put barcodes on the variants.");
            }

            if (errors.Count > 0)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductCommandResult.ValidationFailed(errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
            }

            // Step C - usage guards (409)
            var serialChange = request.IsSerialTracked!.Value != product.IsSerialTracked;
            var firstVariant = product.Variants.Count == 0 && newCount > 0;
            if (serialChange || firstVariant)
            {
                var inUse = await productRepository.IsInUseAsync(product.Id, cancellationToken);
                if (inUse)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    if (serialChange)
                    {
                        return ProductCommandResult.Conflict("Serial tracking cannot be changed because the product already has stock, purchase or order records.");
                    }
                    else
                    {
                        return ProductCommandResult.Conflict("Variants cannot be added because the product already has product-level stock, purchase or order records.");
                    }
                }
            }

            // Step D - SKU conflicts for new variants
            var newSkus = request.Variants?
                .Where(v => v is { Id: null } && !string.IsNullOrWhiteSpace(v.Sku))
                .Select(v => v.Sku!.Trim())
                .ToList() ?? [];

            if (newSkus.Count > 0)
            {
                var conflicts = await productRepository.FindExistingSkusAsync(newSkus, cancellationToken);
                if (conflicts.Count > 0)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return ProductCommandResult.Conflict($"One or more SKUs are already in use: {string.Join(", ", conflicts)}.");
                }
            }

            // Step E - apply product fields
            product.CategoryId = request.CategoryId!.Value;
            product.Name = request.Name!.Trim();
            product.Brand = request.Brand!.Trim();
            product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
            product.BasePrice = request.BasePrice!.Value;
            product.CostPrice = request.CostPrice!.Value;
            product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            product.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            product.Specs = ProductSpecHelper.SerializeSpecs(request.Specs);
            product.IsSerialTracked = request.IsSerialTracked!.Value;
            var now = DateTime.UtcNow;
            product.UpdatedAt = now;

            // Step F - apply variants
            if (request.Variants != null)
            {
                foreach (var item in request.Variants)
                {
                    if (item == null) continue;

                    if (item.Id.HasValue)
                    {
                        var existing = existingById[item.Id.Value];
                        existing.VariantName = item.VariantName!.Trim();
                        existing.Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();
                        existing.Price = item.Price!.Value;
                        existing.Specs = ProductSpecHelper.SerializeSpecs(item.Specs);
                        existing.IsActive = item.IsActive ?? existing.IsActive;
                        existing.UpdatedAt = now;
                    }
                    else
                    {
                        var variant = new ProductVariant
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            VariantName = item.VariantName!.Trim(),
                            Sku = item.Sku!.Trim(),
                            Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim(),
                            Price = item.Price!.Value,
                            Specs = ProductSpecHelper.SerializeSpecs(item.Specs),
                            IsActive = item.IsActive ?? true,
                            CreatedAt = now,
                            UpdatedAt = null
                        };

                        product.Variants.Add(variant);
                        productRepository.AddVariant(variant);
                    }
                }
            }

            foreach (var definition in newDefinitions)
            {
                specDefinitionRepository.Add(definition);
            }

            // Step G - persist
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return ProductCommandResult.Success(ProductDtoMapper.ToDto(product, category!.Name));
        }
        catch (DuplicateKeyException exception)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return ProductCommandResult.Conflict(IsSpecDefinitionConflict(exception)
                ? SpecNameConflictMessage
                : "A SKU in this request is already in use.");
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

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
