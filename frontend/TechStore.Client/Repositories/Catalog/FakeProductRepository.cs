using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public sealed class FakeProductRepository : IProductRepository
{
    private readonly List<ProductSummary> _products;
    private readonly object _productsGate = new();
    private readonly CatalogPreviewScenario _scenario;
    private readonly ProductDetailPreviewScenario _detailScenario;
    private readonly ProductCreatePreviewScenario _createScenario;
    private readonly ProductEditPreviewScenario _editScenario;
    private readonly Dictionary<string, ProductEditData> _editDataById = new(StringComparer.Ordinal);

    public FakeProductRepository(
        IEnumerable<ProductSummary>? products = null,
        CatalogPreviewScenario scenario = CatalogPreviewScenario.Populated,
        ProductDetailPreviewScenario? detailScenario = null,
        ProductCreatePreviewScenario createScenario = ProductCreatePreviewScenario.Success,
        ProductEditPreviewScenario editScenario = ProductEditPreviewScenario.Success)
    {
        _scenario = scenario;
        _createScenario = createScenario;
        _editScenario = editScenario;
        _detailScenario = detailScenario ?? scenario switch
        {
            CatalogPreviewScenario.Empty => ProductDetailPreviewScenario.Unavailable,
            CatalogPreviewScenario.Delayed => ProductDetailPreviewScenario.Delayed,
            CatalogPreviewScenario.Error => ProductDetailPreviewScenario.Error,
            _ => ProductDetailPreviewScenario.Populated
        };
        _products = (products ?? CreateSyntheticCatalog(scenario)).ToList();
    }

    public async Task<PagedResult<ProductSummary>> SearchProductsAsync(
        ProductListCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        cancellationToken.ThrowIfCancellationRequested();

        if (_scenario == CatalogPreviewScenario.Error)
        {
            throw new InvalidOperationException("Configured Catalog preview error.");
        }

        if (_scenario == CatalogPreviewScenario.Delayed)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        if (criteria.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(criteria), "PageIndex cannot be negative.");
        }

        if (criteria.PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(criteria), "PageSize must be greater than zero.");
        }

        ProductSummary[] products;
        lock (_productsGate)
        {
            products = _products.ToArray();
        }

        IEnumerable<ProductSummary> query = products;

        if (!string.IsNullOrWhiteSpace(criteria.SearchQuery))
        {
            var search = criteria.SearchQuery.Trim();
            query = query.Where(product =>
                product.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (product.ProductSku?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                product.Variants.Any(variant => variant.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.CategoryId))
        {
            query = query.Where(product => string.Equals(
                product.CategoryId,
                criteria.CategoryId,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Brand))
        {
            query = query.Where(product => string.Equals(
                product.Brand,
                criteria.Brand,
                StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.PriceBand != ProductPriceBand.All)
        {
            query = query.Where(product => criteria.PriceBand.Includes(product.StartingPrice));
        }

        if (criteria.StockStatus is { } stockStatus)
        {
            query = query.Where(product => product.Status == stockStatus);
        }

        var ordered = criteria.SortMode switch
        {
            ProductSortMode.Newest => query
                .OrderByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id, StringComparer.Ordinal),
            ProductSortMode.PriceAscending => query
                .OrderBy(product => product.StartingPrice ?? decimal.MaxValue)
                .ThenBy(product => product.Id, StringComparer.Ordinal),
            ProductSortMode.PriceDescending => query
                .OrderByDescending(product => product.StartingPrice ?? decimal.MinValue)
                .ThenBy(product => product.Id, StringComparer.Ordinal),
            ProductSortMode.StockDescending => query
                .OrderByDescending(product => product.AvailableQuantity)
                .ThenBy(product => product.Id, StringComparer.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(criteria), "Unknown product sort mode.")
        };

        var filtered = ordered.ToArray();
        var totalItems = filtered.Length;
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)criteria.PageSize);
        var pageIndex = totalPages == 0
            ? 0
            : Math.Min(criteria.PageIndex, totalPages - 1);
        var items = filtered
            .Skip(pageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArray();

        cancellationToken.ThrowIfCancellationRequested();
        return new PagedResult<ProductSummary>(items, totalItems, pageIndex, criteria.PageSize);
    }

    public async Task<ProductDetail?> GetProductDetailAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        cancellationToken.ThrowIfCancellationRequested();

        if (_detailScenario == ProductDetailPreviewScenario.Error)
        {
            throw new InvalidOperationException("Configured Catalog preview error.");
        }

        if (_detailScenario == ProductDetailPreviewScenario.Delayed)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_detailScenario == ProductDetailPreviewScenario.Unavailable)
        {
            return null;
        }

        ProductSummary? product;
        lock (_productsGate)
        {
            product = _products.FirstOrDefault(item => string.Equals(item.Id, productId, StringComparison.Ordinal));
        }

        return product is null ? null : CreateDetail(product);
    }

    public async Task<ProductEditData?> GetProductForEditAsync(
        ProductEditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ProductId);
        cancellationToken.ThrowIfCancellationRequested();

        if (_editScenario == ProductEditPreviewScenario.LoadError)
        {
            throw new InvalidOperationException("Configured product edit preview load error.");
        }

        if (_editScenario == ProductEditPreviewScenario.DelayedLoad)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_editScenario == ProductEditPreviewScenario.NotFound)
        {
            return null;
        }

        ProductSummary? summary;
        ProductEditData? editData;
        lock (_productsGate)
        {
            summary = _products.FirstOrDefault(item => string.Equals(item.Id, query.ProductId, StringComparison.Ordinal));
            editData = _editDataById.GetValueOrDefault(query.ProductId);
        }

        if (summary is null)
        {
            return null;
        }

        editData ??= CreateEditData(summary);
        return query.IncludeCostFields ? editData : StripCostFields(editData);
    }

    public Task<ProductEditResult> UpdateProductAsync(
        ProductEditDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        cancellationToken.ThrowIfCancellationRequested();

        if (_editScenario == ProductEditPreviewScenario.DelayedSave)
        {
            return UpdateProductDelayedAsync(draft, cancellationToken);
        }

        return Task.FromResult(UpdateProduct(draft));
    }

    private async Task<ProductEditResult> UpdateProductDelayedAsync(
        ProductEditDraft draft,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return UpdateProduct(draft);
    }

    private ProductEditResult UpdateProduct(ProductEditDraft draft)
    {
        if (_editScenario == ProductEditPreviewScenario.SaveError)
        {
            return new ProductEditResult(
                ProductEditResultStatus.Failed,
                Message: "Không thể lưu sản phẩm preview lúc này. Các thay đổi vẫn được giữ trong biểu mẫu.");
        }

        if (_editScenario is ProductEditPreviewScenario.DuplicateSku or ProductEditPreviewScenario.DuplicateBarcode)
        {
            var isSku = _editScenario == ProductEditPreviewScenario.DuplicateSku;
            return new ProductEditResult(
                ProductEditResultStatus.Conflict,
                FieldName: isSku ? nameof(ProductEditDraft.ProductSku) : nameof(ProductEditDraft.Barcode),
                ConflictKind: isSku ? ProductEditConflictKind.DuplicateSku : ProductEditConflictKind.DuplicateBarcode,
                Message: isSku
                    ? "SKU preview bị trùng. Đây chỉ là kết quả mẫu; server mới xác định tính duy nhất thực tế."
                    : "Barcode preview bị trùng. Đây chỉ là kết quả mẫu; server mới xác định tính duy nhất thực tế.");
        }

        lock (_productsGate)
        {
            var current = _products.FirstOrDefault(item => string.Equals(item.Id, draft.ProductId, StringComparison.Ordinal));
            if (current is null)
            {
                return new ProductEditResult(
                    ProductEditResultStatus.Failed,
                    Message: "Không tìm thấy sản phẩm trong dữ liệu preview.");
            }

            var original = _editDataById.GetValueOrDefault(draft.ProductId) ?? CreateEditData(current);
            if (_editScenario == ProductEditPreviewScenario.ConcurrentUpdate)
            {
                return Conflict(ProductEditConflictKind.ConcurrentUpdate,
                    "Dữ liệu preview đã thay đổi trong lúc chỉnh sửa. Hãy tải lại trước khi lưu.");
            }

            if (_editScenario == ProductEditPreviewScenario.VariantHasTransactions && HasProtectedVariantChange(original, draft))
            {
                return Conflict(ProductEditConflictKind.VariantHasTransactions,
                    "Preview từ chối thay đổi biến thể có lịch sử giao dịch. Danh mục đã lưu chưa thay đổi.");
            }

            var duplicate = FindDuplicateIdentifier(current.Id, draft);
            if (duplicate is not null)
            {
                return duplicate;
            }

            var originalVariants = original.Variants.ToDictionary(variant => variant.Id, StringComparer.Ordinal);
            var variants = draft.Variants.Select((variant, index) =>
            {
                var variantId = string.IsNullOrWhiteSpace(variant.Id)
                    ? $"preview-edit-{Guid.NewGuid():N}-{index + 1}"
                    : variant.Id;
                var prior = originalVariants.GetValueOrDefault(variantId);
                return new ProductEditVariant(
                    Id: variantId,
                    Name: variant.Name,
                    Sku: variant.Sku,
                    Barcode: variant.Barcode,
                    StorageLabel: variant.StorageLabel,
                    ColorLabel: variant.ColorLabel,
                    SwatchColor: variant.SwatchColor,
                    SellingPrice: variant.SellingPrice,
                    CostPrice: variant.CostPrice ?? prior?.CostPrice,
                    AvailableQuantity: prior?.AvailableQuantity ?? 0,
                    Specifications: variant.Specifications.ToArray());
            }).ToArray();

            var updated = original with
            {
                Name = draft.Name,
                Brand = draft.Brand,
                CategoryId = draft.CategoryId,
                CategoryName = CategoryName(draft.CategoryId),
                ProductSku = draft.ProductSku,
                Barcode = draft.Barcode,
                ModelId = draft.ModelId,
                ImageUrl = draft.ImageUrl,
                BasePrice = draft.BasePrice,
                CostPrice = draft.CostPrice ?? original.CostPrice,
                IsSerialTracked = original.IsSerialTracked,
                Specifications = draft.Specifications.ToArray(),
                Variants = variants,
                IsPreviewData = true
            };

            var summaryVariants = variants.Select(variant =>
            {
                var prior = current.Variants.FirstOrDefault(item => item.Id == variant.Id);
                return new ProductVariantSummary(
                    Id: variant.Id,
                    Name: variant.Name,
                    Sku: variant.Sku,
                    SellingPrice: variant.SellingPrice,
                    AvailableQuantity: variant.AvailableQuantity,
                    SwatchLabel: variant.ColorLabel ?? prior?.SwatchLabel,
                    SwatchColor: variant.SwatchColor ?? prior?.SwatchColor,
                    Barcode: variant.Barcode);
            }).ToArray();
            var updatedSummary = current with
            {
                Name = updated.Name,
                Brand = updated.Brand,
                CategoryId = updated.CategoryId,
                CategoryName = updated.CategoryName,
                ProductSku = updated.ProductSku,
                Barcode = updated.Barcode,
                ImageUrl = updated.ImageUrl,
                Variants = summaryVariants
            };

            var indexOfCurrent = _products.FindIndex(item => item.Id == current.Id);
            _products[indexOfCurrent] = updatedSummary;
            _editDataById[current.Id] = updated;
            return new ProductEditResult(ProductEditResultStatus.Saved, updated);
        }
    }

    private ProductEditResult? FindDuplicateIdentifier(string currentProductId, ProductEditDraft draft)
    {
        var otherProducts = _products.Where(product => product.Id != currentProductId).ToArray();
        var existingSkus = otherProducts
            .SelectMany(product => product.Variants.Select(variant => variant.Sku).Append(product.Sku ?? string.Empty))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateSkus = draft.Variants.Select(variant => variant.Sku).Append(draft.ProductSku).ToArray();
        if (candidateSkus.Any(existingSkus.Contains) ||
            candidateSkus.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            return Conflict(ProductEditConflictKind.DuplicateSku,
                "SKU preview bị trùng. Đây chỉ là kết quả mẫu; server mới xác định tính duy nhất thực tế.",
                nameof(ProductEditDraft.ProductSku));
        }

        var existingBarcodes = otherProducts
            .SelectMany(product => product.Variants.Select(variant => variant.Barcode).Append(product.Barcode))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateBarcodes = draft.Variants.Select(variant => variant.Barcode)
            .Append(draft.Barcode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        if (candidateBarcodes.Any(existingBarcodes.Contains) ||
            candidateBarcodes.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            return Conflict(ProductEditConflictKind.DuplicateBarcode,
                "Barcode preview bị trùng. Đây chỉ là kết quả mẫu; server mới xác định tính duy nhất thực tế.",
                nameof(ProductEditDraft.Barcode));
        }

        return null;
    }

    private static bool HasProtectedVariantChange(ProductEditData original, ProductEditDraft draft)
    {
        var submittedById = draft.Variants
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Id))
            .ToDictionary(variant => variant.Id!, StringComparer.Ordinal);
        return original.Variants.Any(variant =>
            !submittedById.TryGetValue(variant.Id, out var submitted) ||
            !string.Equals(variant.Sku, submitted.Sku, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(variant.Barcode, submitted.Barcode, StringComparison.OrdinalIgnoreCase));
    }

    private static ProductEditResult Conflict(
        ProductEditConflictKind kind,
        string message,
        string? fieldName = null) =>
        new(ProductEditResultStatus.Conflict, FieldName: fieldName, ConflictKind: kind, Message: message);

    private static ProductEditData StripCostFields(ProductEditData data) => data with
    {
        CostPrice = null,
        Variants = data.Variants.Select(variant => variant with { CostPrice = null }).ToArray()
    };

    private static ProductEditData CreateEditData(ProductSummary product)
    {
        var detail = CreateDetail(product);
        var detailVariants = detail.Variants.ToDictionary(variant => variant.Id, StringComparer.Ordinal);
        var variants = product.Variants.Select(variant =>
        {
            detailVariants.TryGetValue(variant.Id, out var detailVariant);
            return new ProductEditVariant(
                Id: variant.Id,
                Name: variant.Name,
                Sku: variant.Sku,
                Barcode: variant.Barcode,
                StorageLabel: detailVariant?.StorageLabel,
                ColorLabel: variant.SwatchLabel,
                SwatchColor: variant.SwatchColor,
                SellingPrice: variant.SellingPrice,
                CostPrice: detailVariant?.AverageCost,
                AvailableQuantity: variant.AvailableQuantity,
                Specifications: detailVariant?.Specifications
                    .Select(specification => new ProductSpecificationEntry(specification.Name, specification.Value))
                    .ToArray() ?? Array.Empty<ProductSpecificationEntry>());
        }).ToArray();

        return new ProductEditData(
            Id: product.Id,
            Name: product.Name,
            Brand: product.Brand,
            CategoryId: product.CategoryId,
            CategoryName: product.CategoryName,
            ProductSku: product.ProductSku ?? $"MODEL-{product.Id.ToUpperInvariant()}",
            Barcode: product.Barcode,
            ModelId: product.Id == "iphone-15-pro-max" ? "A3106" : product.ProductSku,
            ImageUrl: product.ImageUrl,
            BasePrice: product.StartingPrice,
            CostPrice: variants.Select(variant => variant.CostPrice).FirstOrDefault(cost => cost.HasValue),
            IsSerialTracked: string.Equals(product.CategoryId, "phones", StringComparison.OrdinalIgnoreCase),
            AvailableQuantity: product.AvailableQuantity,
            Specifications: detail.Specifications
                .Select(specification => new ProductSpecificationEntry(specification.Name, specification.Value))
                .ToArray(),
            Variants: variants,
            IsPreviewData: true);
    }

    public Task<ProductCreateResult> CreateProductAsync(
        ProductCreateDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        cancellationToken.ThrowIfCancellationRequested();

        if (_createScenario == ProductCreatePreviewScenario.Delayed)
        {
            return CreateDelayedProductAsync(draft, cancellationToken);
        }

        return Task.FromResult(CreateProduct(draft));
    }

    private async Task<ProductCreateResult> CreateDelayedProductAsync(
        ProductCreateDraft draft,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CreateProduct(draft);
    }

    private ProductCreateResult CreateProduct(ProductCreateDraft draft)
    {
        if (_createScenario == ProductCreatePreviewScenario.Error)
        {
            return new ProductCreateResult(
                ProductCreateResultStatus.Failed,
                Message: "Không thể tạo sản phẩm mẫu lúc này. Dữ liệu vẫn được giữ lại.");
        }

        if (_createScenario == ProductCreatePreviewScenario.DuplicateSku)
        {
            return new ProductCreateResult(
                ProductCreateResultStatus.Conflict,
                FieldName: nameof(ProductCreateDraft.ProductSku),
                Message: "SKU mẫu đang bị trùng. Đây chỉ là kết quả preview; server mới xác định tính duy nhất thực tế.");
        }

        if (_createScenario == ProductCreatePreviewScenario.DuplicateBarcode)
        {
            return new ProductCreateResult(
                ProductCreateResultStatus.Conflict,
                FieldName: nameof(ProductCreateDraft.Barcode),
                Message: "Barcode mẫu đang bị trùng. Đây chỉ là kết quả preview; server mới xác định tính duy nhất thực tế.");
        }

        lock (_productsGate)
        {
            var allSkus = _products
                .SelectMany(product => product.Variants.Select(variant => variant.Sku).Append(product.Sku ?? string.Empty));
            if (allSkus.Contains(draft.ProductSku, StringComparer.OrdinalIgnoreCase))
            {
                return new ProductCreateResult(
                    ProductCreateResultStatus.Conflict,
                    FieldName: nameof(ProductCreateDraft.ProductSku),
                    Message: "SKU mẫu đang bị trùng. Đây chỉ là kết quả preview; server mới xác định tính duy nhất thực tế.");
            }

            var duplicateVariantIndex = draft.Variants
                .Select((variant, index) => (variant, index))
                .FirstOrDefault(item => allSkus.Contains(item.variant.Sku, StringComparer.OrdinalIgnoreCase));
            if (duplicateVariantIndex.variant is not null)
            {
                return new ProductCreateResult(
                    ProductCreateResultStatus.Conflict,
                    FieldName: $"Variants[{duplicateVariantIndex.index}].Sku",
                    Message: "SKU biến thể mẫu đang bị trùng. Đây chỉ là kết quả preview; server mới xác định tính duy nhất thực tế.");
            }

            var repeatedSku = draft.Variants
                .Select((variant, index) => (variant, index))
                .GroupBy(item => item.variant.Sku, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (repeatedSku is not null)
            {
                return new ProductCreateResult(
                    ProductCreateResultStatus.Conflict,
                    FieldName: $"Variants[{repeatedSku.Last().index}].Sku",
                    Message: "Các SKU biến thể mẫu phải khác nhau. Server vẫn là nơi xác định tính duy nhất thực tế.");
            }

            if (!string.IsNullOrWhiteSpace(draft.Barcode) && _products.Any(product =>
                    string.Equals(product.Barcode, draft.Barcode, StringComparison.OrdinalIgnoreCase) ||
                    product.Variants.Any(variant => string.Equals(variant.Barcode, draft.Barcode, StringComparison.OrdinalIgnoreCase))))
            {
                return new ProductCreateResult(
                    ProductCreateResultStatus.Conflict,
                    FieldName: nameof(ProductCreateDraft.Barcode),
                    Message: "Barcode mẫu đang bị trùng. Đây chỉ là kết quả preview; server mới xác định tính duy nhất thực tế.");
            }

            var duplicateVariantBarcode = draft.Variants
                .Select((variant, index) => (variant, index))
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.variant.Barcode) && _products.Any(product =>
                    string.Equals(product.Barcode, item.variant.Barcode, StringComparison.OrdinalIgnoreCase) ||
                    product.Variants.Any(existing => string.Equals(existing.Barcode, item.variant.Barcode, StringComparison.OrdinalIgnoreCase))));
            if (duplicateVariantBarcode.variant is not null)
            {
                return new ProductCreateResult(
                    ProductCreateResultStatus.Conflict,
                    FieldName: $"Variants[{duplicateVariantBarcode.index}].Barcode",
                    Message: "Barcode biến thể mẫu đang bị trùng. Đây chỉ là kết quả preview; server mới xác định tính duy nhất thực tế.");
            }

            var variants = draft.Variants.Select((variant, index) => new ProductVariantSummary(
                Id: $"preview-{Guid.NewGuid():N}-{index + 1}",
                Name: variant.Name,
                Sku: variant.Sku,
                SellingPrice: variant.Price,
                AvailableQuantity: 0,
                Barcode: variant.Barcode)).ToArray();

            var product = new ProductSummary(
                Id: $"preview-{Guid.NewGuid():N}",
                Name: draft.Name,
                Brand: draft.Brand,
                CategoryId: draft.CategoryId,
                CategoryName: CategoryName(draft.CategoryId),
                ImageUrl: draft.ImageUrl,
                Status: ProductStatus.OutOfStock,
                Variants: variants,
                CreatedAt: DateTimeOffset.UtcNow,
                ProductSku: draft.ProductSku,
                Barcode: draft.Barcode);

            _products.Add(product);
            return new ProductCreateResult(ProductCreateResultStatus.Created, product);
        }
    }

    private static string CategoryName(string categoryId) => categoryId switch
    {
        "phones" => "Điện thoại",
        "laptops" => "Laptop",
        "tablets" => "Tablet",
        "accessories" => "Phụ kiện",
        _ => categoryId
    };

    private static ProductDetail CreateDetail(ProductSummary product)
    {
        var isIphonePreview = product.Id == "iphone-15-pro-max";
        var productSpecifications = isIphonePreview
            ? new[]
            {
                new ProductSpecification("Vi xử lý (CPU)", "Apple A17 Pro (3nm, 6-core)"),
                new ProductSpecification("Màn hình", "6.7\" Super Retina XDR OLED, 120Hz ProMotion"),
                new ProductSpecification("Pin", "4.422 mAh, sạc nhanh 20W")
            }
            : new[]
            {
                new ProductSpecification("Thương hiệu", product.Brand),
                new ProductSpecification("Danh mục", product.CategoryName)
            };

        var variants = product.Variants.Select(variant =>
        {
            var averageCost = isIphonePreview ? variant.SellingPrice - 4_490_000m : (decimal?)null;
            var status = product.Status == ProductStatus.Discontinued
                ? ProductStatus.Discontinued
                : variant.AvailableQuantity <= 0
                    ? ProductStatus.OutOfStock
                    : product.Status;

            return new ProductDetailVariant(
                Id: variant.Id,
                Name: variant.Name,
                Sku: variant.Sku,
                StorageLabel: FindStorageLabel(variant.Name),
                ColorLabel: variant.SwatchLabel,
                SwatchColor: variant.SwatchColor,
                SellingPrice: variant.SellingPrice,
                AvailableQuantity: variant.AvailableQuantity,
                Status: status,
                Specifications: [new ProductSpecification("Cấu hình", variant.Name)],
                AverageCost: averageCost,
                EstimatedMargin: isIphonePreview ? 4_490_000m : null);
        }).ToArray();

        return new ProductDetail(
            Id: product.Id,
            Name: product.Name,
            Brand: product.Brand,
            ModelId: isIphonePreview ? "A3106" : null,
            WarrantyMonths: isIphonePreview ? 12 : null,
            ImageUrl: product.ImageUrl,
            Status: product.Status,
            Specifications: productSpecifications,
            Variants: variants,
            IsPreviewData: true);
    }

    private static string? FindStorageLabel(string variantName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            variantName,
            @"\b\d+(?:\.\d+)?\s*(?:GB|TB)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant() : null;
    }

    private static IReadOnlyList<ProductSummary> CreateSyntheticCatalog(CatalogPreviewScenario scenario)
    {
        if (scenario == CatalogPreviewScenario.Empty)
        {
            return Array.Empty<ProductSummary>();
        }

        var brands = new[] { "Apple", "Samsung", "Asus", "Dell", "Xiaomi" };
        (string Id, string Name)[] categories =
        [
            ("phones", "Điện thoại"),
            ("laptops", "Laptop"),
            ("tablets", "Tablet"),
            ("accessories", "Phụ kiện")
        ];
        var products = new List<ProductSummary>();

        for (var i = 1; i <= 20; i++)
        {
            var brand = brands[(i - 1) % brands.Length];
            var category = categories[(i - 1) % categories.Length];
            var status = i % 7 == 0
                ? ProductStatus.Discontinued
                : i % 5 == 0
                    ? ProductStatus.OutOfStock
                    : i % 4 == 0
                        ? ProductStatus.LowStock
                        : ProductStatus.InStock;
            var available = status switch
            {
                ProductStatus.InStock => 5 + i,
                ProductStatus.LowStock => 2,
                _ => 0
            };
            var startingPrice = 7_000_000m + ((i - 1) * 1_000_000m);
            var createdAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero).AddDays(i);
            var variants = new[]
            {
                new ProductVariantSummary(
                    $"variant-{i}-blue",
                    "Titan Xanh 256GB",
                    $"SKU-{i:D2}-BLUE",
                    startingPrice,
                    available,
                    "Titan Xanh",
                    "#27384A"),
                new ProductVariantSummary(
                    $"variant-{i}-natural",
                    "Titan Tự nhiên 512GB",
                    $"SKU-{i:D2}-NATURAL",
                    startingPrice + 2_000_000m,
                    available,
                    "Titan Tự nhiên",
                    "#A49E93")
            };

            products.Add(new ProductSummary(
                $"product-{i:D2}",
                $"{brand} Device {i:D2}",
                brand,
                category.Id,
                category.Name,
                $"ms-appx:///Assets/Catalog/{category.Id}.png",
                status,
                variants,
                createdAt));
        }

        products.Add(new ProductSummary(
            "iphone-15-pro-max",
            "iPhone 15 Pro Max 256GB",
            "Apple",
            "phones",
            "Điện thoại",
            "ms-appx:///Assets/Catalog/phones.png",
            ProductStatus.InStock,
            [
                new ProductVariantSummary(
                    "iphone-15-pro-max-natural",
                    "Titan Tự nhiên 256GB",
                    "IP15PM-256-NAT",
                    34_990_000m,
                    12,
                    "Titan Tự nhiên",
                    "#A49E93"),
                new ProductVariantSummary(
                    "iphone-15-pro-max-blue",
                    "Titan Xanh 256GB",
                    "IP15PM-256-BLUE",
                    34_990_000m,
                    8,
                    "Titan Xanh",
                    "#27384A")
            ],
            new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)));

        var missingImageVariants = products[0].Variants
            .Select(variant => variant with
            {
                Id = $"missing-{variant.Id}",
                Sku = $"MISSING-{variant.Sku}"
            })
            .ToArray();
        products.Add(products[0] with
        {
            Id = "missing-image-fixture",
            Name = "Missing image fixture",
            ImageUrl = null,
            Variants = missingImageVariants
        });

        products.Add(products[0] with
        {
            Id = "failed-image-fixture",
            Name = "Failed image fixture",
            ImageUrl = "ms-appx:///Assets/Catalog/not-present.png",
            Variants = missingImageVariants.Select(variant => variant with
            {
                Id = $"failed-{variant.Id}",
                Sku = $"FAILED-{variant.Sku}"
            }).ToArray()
        });

        if (scenario == CatalogPreviewScenario.MissingImage)
        {
            return [products[^2]];
        }

        return products;
    }
}
