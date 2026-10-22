using TechStore.Client.Models.Catalog;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Catalog;

public static class ProductDraftValidator
{
    public static DraftValidationResult Validate(ProductDraft draft, bool isCreate)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);

        // 1. CategoryId
        if (draft.CategoryId is null || draft.CategoryId.Value == Guid.Empty)
        {
            fieldErrors["categoryId"] = "Hãy chọn danh mục.";
        }

        // 2. Name
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            fieldErrors["name"] = "Tên sản phẩm là bắt buộc.";
        }
        else if (draft.Name.Length > 200)
        {
            fieldErrors["name"] = "Tên sản phẩm không được vượt quá 200 ký tự.";
        }

        // 3. Brand
        if (string.IsNullOrWhiteSpace(draft.Brand))
        {
            fieldErrors["brand"] = "Thương hiệu là bắt buộc.";
        }
        else if (draft.Brand.Length > 100)
        {
            fieldErrors["brand"] = "Thương hiệu không được vượt quá 100 ký tự.";
        }

        // 4. SKU (only when isCreate)
        string? productSkuTrimmed = null;
        if (isCreate)
        {
            if (string.IsNullOrWhiteSpace(draft.Sku))
            {
                fieldErrors["sku"] = "Mã SKU là bắt buộc.";
            }
            else
            {
                productSkuTrimmed = draft.Sku.Trim();
                if (productSkuTrimmed.Length > 64)
                {
                    fieldErrors["sku"] = "Mã SKU không được vượt quá 64 ký tự.";
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(draft.Sku))
        {
            productSkuTrimmed = draft.Sku.Trim();
        }

        // 5. Barcode
        if (!string.IsNullOrEmpty(draft.Barcode) && draft.Barcode.Length > 64)
        {
            fieldErrors["barcode"] = "Barcode không được vượt quá 64 ký tự.";
        }

        // 6. Description
        if (!string.IsNullOrEmpty(draft.Description) && draft.Description.Length > 2000)
        {
            fieldErrors["description"] = "Mô tả không được vượt quá 2000 ký tự.";
        }

        // 7. ImageUrl
        if (!string.IsNullOrEmpty(draft.ImageUrl) && draft.ImageUrl.Length > 2048)
        {
            fieldErrors["imageUrl"] = "URL ảnh không được vượt quá 2048 ký tự.";
        }

        // 8. BasePrice & CostPrice
        decimal basePriceValue = 0m;
        if (!ProductInputParser.TryParseMoney(draft.BasePrice, out basePriceValue))
        {
            fieldErrors["basePrice"] = "Giá bán không hợp lệ.";
        }

        decimal costPriceValue = 0m;
        if (!ProductInputParser.TryParseMoney(draft.CostPrice, out costPriceValue))
        {
            fieldErrors["costPrice"] = "Giá vốn dự kiến không hợp lệ.";
        }

        // 9. Product specs
        var productSpecResult = ProductInputParser.ParseSpecs(draft.Specs ?? []);
        var productSpecRowErrors = productSpecResult.RowErrors;
        if (!productSpecResult.IsValid)
        {
            fieldErrors["specs"] = "Có thông số chưa hợp lệ.";
        }

        // 10. Variants
        var variants = draft.Variants ?? [];
        var variantSpecRowErrors = new List<IReadOnlyList<string?>>(variants.Count);
        var seenNewVariantSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < variants.Count; i++)
        {
            var variant = variants[i];
            var prefix = $"variants[{i}]";

            if (string.IsNullOrWhiteSpace(variant.VariantName))
            {
                fieldErrors[$"{prefix}.variantName"] = "Tên biến thể là bắt buộc.";
            }
            else if (variant.VariantName.Length > 200)
            {
                fieldErrors[$"{prefix}.variantName"] = "Tên biến thể không được vượt quá 200 ký tự.";
            }

            if (!ProductInputParser.TryParseMoney(variant.Price, out _))
            {
                fieldErrors[$"{prefix}.price"] = "Giá bán không hợp lệ.";
            }

            if (!string.IsNullOrEmpty(variant.Barcode) && variant.Barcode.Length > 64)
            {
                fieldErrors[$"{prefix}.barcode"] = "Barcode biến thể không được vượt quá 64 ký tự.";
            }

            if (variant.Id is null)
            {
                var variantSku = variant.Sku?.Trim();
                if (string.IsNullOrWhiteSpace(variantSku))
                {
                    fieldErrors[$"{prefix}.sku"] = "Mã SKU biến thể là bắt buộc.";
                }
                else if (variantSku.Length > 64)
                {
                    fieldErrors[$"{prefix}.sku"] = "Mã SKU biến thể không được vượt quá 64 ký tự.";
                }
                else if (isCreate && !string.IsNullOrWhiteSpace(productSkuTrimmed)
                    && string.Equals(variantSku, productSkuTrimmed, StringComparison.OrdinalIgnoreCase))
                {
                    fieldErrors[$"{prefix}.sku"] = "SKU không được trùng với SKU sản phẩm.";
                }
                else if (!seenNewVariantSkus.Add(variantSku))
                {
                    fieldErrors[$"{prefix}.sku"] = "SKU trùng với một biến thể khác.";
                }
            }

            var variantSpecResult = ProductInputParser.ParseSpecs(variant.Specs ?? []);
            variantSpecRowErrors.Add(variantSpecResult.RowErrors);
            if (!variantSpecResult.IsValid)
            {
                fieldErrors[$"{prefix}.specs"] = "Có thông số chưa hợp lệ.";
            }
        }

        var isValid = fieldErrors.Count == 0
            && productSpecRowErrors.All(e => e is null)
            && variantSpecRowErrors.All(r => r.All(e => e is null));

        CreateProductRequest? createRequest = null;
        UpdateProductRequest? updateRequest = null;

        if (isValid)
        {
            string? barcodeToSend = variants.Count > 0
                ? null
                : (string.IsNullOrWhiteSpace(draft.Barcode) ? null : draft.Barcode.Trim());

            string? descriptionToSend = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim();
            string? imageUrlToSend = string.IsNullOrWhiteSpace(draft.ImageUrl) ? null : draft.ImageUrl.Trim();

            if (isCreate)
            {
                createRequest = new CreateProductRequest
                {
                    CategoryId = draft.CategoryId!.Value,
                    Name = draft.Name.Trim(),
                    Sku = draft.Sku.Trim(),
                    Brand = draft.Brand.Trim(),
                    BasePrice = basePriceValue,
                    CostPrice = costPriceValue,
                    Barcode = barcodeToSend,
                    Description = descriptionToSend,
                    ImageUrl = imageUrlToSend,
                    IsSerialTracked = draft.IsSerialTracked,
                    Specs = productSpecResult.Values,
                    SpecLabels = BuildSpecLabels(draft.SpecLabels),
                    Variants = variants.Select(v => new CreateProductVariantRequest
                    {
                        VariantName = v.VariantName.Trim(),
                        Sku = v.Sku.Trim(),
                        Barcode = string.IsNullOrWhiteSpace(v.Barcode) ? null : v.Barcode.Trim(),
                        Price = ProductInputParser.TryParseMoney(v.Price, out var vp) ? vp : 0m,
                        Specs = ProductInputParser.ParseSpecs(v.Specs ?? []).Values,
                        IsActive = v.IsActive
                    }).ToList()
                };
            }
            else
            {
                updateRequest = new UpdateProductRequest
                {
                    CategoryId = draft.CategoryId!.Value,
                    Name = draft.Name.Trim(),
                    Brand = draft.Brand.Trim(),
                    BasePrice = basePriceValue,
                    CostPrice = costPriceValue,
                    Barcode = barcodeToSend,
                    Description = descriptionToSend,
                    ImageUrl = imageUrlToSend,
                    IsSerialTracked = draft.IsSerialTracked,
                    Specs = productSpecResult.Values,
                    SpecLabels = BuildSpecLabels(draft.SpecLabels),
                    Variants = variants.Select(v => new UpdateProductVariantRequest
                    {
                        Id = v.Id,
                        VariantName = v.VariantName.Trim(),
                        Sku = v.Id is null ? v.Sku.Trim() : null,
                        Barcode = string.IsNullOrWhiteSpace(v.Barcode) ? null : v.Barcode.Trim(),
                        Price = ProductInputParser.TryParseMoney(v.Price, out var vp) ? vp : 0m,
                        Specs = ProductInputParser.ParseSpecs(v.Specs ?? []).Values,
                        IsActive = v.IsActive
                    }).ToList()
                };
            }
        }

        return new DraftValidationResult(
            fieldErrors,
            productSpecRowErrors,
            variantSpecRowErrors,
            createRequest,
            updateRequest);
    }

    /// <summary>Names of new spec keys (first name per key wins); null when there is none.</summary>
    private static Dictionary<string, SpecLabelRequest>? BuildSpecLabels(IReadOnlyList<SpecLabelDraft>? labels)
    {
        if (labels is null || labels.Count == 0) return null;
        var result = new Dictionary<string, SpecLabelRequest>(StringComparer.Ordinal);
        foreach (var label in labels)
        {
            if (result.ContainsKey(label.Key)) continue;
            result[label.Key] = new SpecLabelRequest
            {
                Label = label.Label.Trim(),
                Unit = string.IsNullOrWhiteSpace(label.Unit) ? null : label.Unit.Trim(),
                ValueKind = label.Kind switch
                {
                    SpecValueKind.Number => "number",
                    SpecValueKind.Boolean => "boolean",
                    _ => "string"
                }
            };
        }
        return result;
    }
}

public sealed record DraftValidationResult(
    IReadOnlyDictionary<string, string> FieldErrors,
    IReadOnlyList<string?> ProductSpecRowErrors,
    IReadOnlyList<IReadOnlyList<string?>> VariantSpecRowErrors,
    CreateProductRequest? Create,
    UpdateProductRequest? Update)
{
    public bool IsValid => FieldErrors.Count == 0
        && ProductSpecRowErrors.All(e => e is null)
        && VariantSpecRowErrors.All(r => r.All(e => e is null));
}
