using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public static class CreateProductValidator
{
    public static Dictionary<string, string[]> Validate(CreateProductRequest? request)
    {
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

        if (request == null)
        {
            AddError("request", "Request body cannot be null.");
            return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
        }

        // 1. CategoryId
        if (!request.CategoryId.HasValue || request.CategoryId.Value == Guid.Empty)
        {
            AddError("categoryId", "Category ID is required.");
        }

        // 2. Name
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError("name", "Product name is required.");
        }
        else if (request.Name.Length > 200)
        {
            AddError("name", "Product name cannot exceed 200 characters.");
        }

        // 3. SKU
        string? productSkuTrimmed = null;
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            AddError("sku", "Product SKU is required.");
        }
        else
        {
            productSkuTrimmed = request.Sku.Trim();
            if (productSkuTrimmed.Length > 64)
            {
                AddError("sku", "Product SKU cannot exceed 64 characters.");
            }
        }

        // 4. Barcode
        if (!string.IsNullOrEmpty(request.Barcode) && request.Barcode.Length > 64)
        {
            AddError("barcode", "Barcode cannot exceed 64 characters.");
        }

        // 5. Brand
        if (string.IsNullOrWhiteSpace(request.Brand))
        {
            AddError("brand", "Brand is required.");
        }
        else if (request.Brand.Length > 100)
        {
            AddError("brand", "Brand cannot exceed 100 characters.");
        }

        // 6. BasePrice
        if (!request.BasePrice.HasValue)
        {
            AddError("basePrice", "Base price is required.");
        }
        else if (request.BasePrice.Value < 0)
        {
            AddError("basePrice", "Base price must be non-negative.");
        }

        // 7. CostPrice
        if (!request.CostPrice.HasValue)
        {
            AddError("costPrice", "Cost price is required.");
        }
        else if (request.CostPrice.Value < 0)
        {
            AddError("costPrice", "Cost price must be non-negative.");
        }

        // 8. Description
        if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 2000)
        {
            AddError("description", "Description cannot exceed 2000 characters.");
        }

        // 9. ImageUrl
        if (!string.IsNullOrEmpty(request.ImageUrl) && request.ImageUrl.Length > 2048)
        {
            AddError("imageUrl", "Image URL cannot exceed 2048 characters.");
        }

        // 10. Specs
        if (request.Specs != null)
        {
            ProductSpecValidatorHelper.ValidateSpecs(request.Specs, "specs", errors);
        }

        // 11. SKU tracking within the request
        var seenSkus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(productSkuTrimmed))
        {
            seenSkus[productSkuTrimmed] = "sku";
        }

        // 12. Variants
        if (request.Variants != null)
        {
            for (var i = 0; i < request.Variants.Count; i++)
            {
                var variant = request.Variants[i];
                var prefix = $"variants[{i}]";

                if (variant == null)
                {
                    AddError(prefix, $"Variant at index {i} cannot be null.");
                    continue;
                }

                // VariantName
                if (string.IsNullOrWhiteSpace(variant.VariantName))
                {
                    AddError($"{prefix}.variantName", "Variant name is required.");
                }
                else if (variant.VariantName.Length > 200)
                {
                    AddError($"{prefix}.variantName", "Variant name cannot exceed 200 characters.");
                }

                // Variant SKU
                if (string.IsNullOrWhiteSpace(variant.Sku))
                {
                    AddError($"{prefix}.sku", "Variant SKU is required.");
                }
                else
                {
                    var varSkuTrimmed = variant.Sku.Trim();
                    if (varSkuTrimmed.Length > 64)
                    {
                        AddError($"{prefix}.sku", "Variant SKU cannot exceed 64 characters.");
                    }

                    if (seenSkus.TryGetValue(varSkuTrimmed, out var originalField))
                    {
                        AddError($"{prefix}.sku", $"SKU '{variant.Sku}' is already used by '{originalField}' within the request.");
                    }
                    else
                    {
                        seenSkus[varSkuTrimmed] = $"{prefix}.sku";
                    }
                }

                // Variant Barcode
                if (!string.IsNullOrEmpty(variant.Barcode) && variant.Barcode.Length > 64)
                {
                    AddError($"{prefix}.barcode", "Variant barcode cannot exceed 64 characters.");
                }

                // Variant Price
                if (!variant.Price.HasValue)
                {
                    AddError($"{prefix}.price", "Variant price is required.");
                }
                else if (variant.Price.Value < 0)
                {
                    AddError($"{prefix}.price", "Variant price must be non-negative.");
                }

                // Variant Specs
                if (variant.Specs != null)
                {
                    ProductSpecValidatorHelper.ValidateSpecs(variant.Specs, $"{prefix}.specs", errors);
                }
            }
        }

        return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
    }
}
