using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public sealed record SkuMatchResult(IReadOnlyDictionary<VariantRowItem, Guid> Matches, IReadOnlyList<string> MissingSkus);

public static class VariantSkuMatcher
{
    public static SkuMatchResult Match(IReadOnlyList<VariantRowItem> rows, IReadOnlyList<ProductVariant> saved)
    {
        var bySku = saved.ToDictionary(v => v.Sku.Trim(), v => v.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<VariantRowItem, Guid> matches = []; List<string> missing = [];
        foreach (var row in rows.Where(r => r.Id == null))
            if (bySku.TryGetValue(row.Sku.Trim(), out var id)) matches[row] = id;
            else missing.Add(row.Sku.Trim());
        return new(matches, missing);
    }
}
