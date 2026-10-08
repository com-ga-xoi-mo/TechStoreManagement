using System.Text.Json;

namespace TechStore.Api.Catalog.Services;

public static class ProductSpecHelper
{
    public static Dictionary<string, JsonElement> ParseJsonSpecs(string? specsJson)
    {
        if (string.IsNullOrWhiteSpace(specsJson) || specsJson.Trim() == "{}")
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(specsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var dict = new Dictionary<string, JsonElement>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.Clone();
            }
            return dict;
        }
        catch
        {
            return [];
        }
    }

    public static string SerializeSpecs(IDictionary<string, JsonElement>? specs)
    {
        if (specs == null || specs.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(specs);
    }

    public static Dictionary<string, JsonElement> MergeEffectiveSpecs(
        IDictionary<string, JsonElement>? productSpecs,
        IDictionary<string, JsonElement>? variantSpecs)
    {
        var merged = new Dictionary<string, JsonElement>();

        if (productSpecs != null)
        {
            foreach (var (k, v) in productSpecs)
            {
                merged[k] = v.Clone();
            }
        }

        if (variantSpecs != null)
        {
            foreach (var (k, v) in variantSpecs)
            {
                merged[k] = v.Clone();
            }
        }

        return merged;
    }

    public static (decimal priceFrom, decimal priceTo) CalculateDisplayPrice(
        decimal basePrice,
        IEnumerable<decimal>? variantPrices)
    {
        if (variantPrices == null)
        {
            return (basePrice, basePrice);
        }

        var pricesList = variantPrices as IList<decimal> ?? variantPrices.ToList();
        if (pricesList.Count == 0)
        {
            return (basePrice, basePrice);
        }

        return (pricesList.Min(), pricesList.Max());
    }
}
